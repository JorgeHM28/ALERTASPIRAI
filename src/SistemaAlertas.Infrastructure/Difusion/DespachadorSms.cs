using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Infrastructure.Difusion;

/// <summary>
/// Procesa un lote de la cola SMS (RF-04, RNF-02, RNF-08): toma los envíos pendientes cuyo próximo intento ya llegó,
/// los manda al proveedor en paralelo (con un tope de envíos simultáneos) y guarda el resultado de cada uno.
/// Un fallo recuperable queda agendado 60 s después, hasta 2 reintentos; un fallo definitivo termina el envío.
/// Lo llama ProcesadorColaSms cada pocos segundos; es una clase aparte para poder probarla sin el servicio en segundo plano.
/// </summary>
public sealed class DespachadorSms(IAppDbContext db, IEmisorSms emisor, OpcionesSms opciones, TimeProvider reloj,
    ILogger<DespachadorSms>? logger = null)
{
    /// <returns>Cuántos envíos se intentaron en este lote.</returns>
    public async Task<int> ProcesarLoteAsync(CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        var lote = await db.TrabajosDifusion.OfType<EnvioSms>()
            .Where(e => e.Estado == EstadoTrabajo.Pendiente && e.ProximoIntento != null && e.ProximoIntento <= ahora)
            .OrderBy(e => e.ProximoIntento).ThenBy(e => e.CreadoEn)
            .Take(Math.Max(1, opciones.TamanoLote))
            .ToListAsync(ct);
        if (lote.Count == 0) return 0;

        using var cupo = new SemaphoreSlim(Math.Max(1, opciones.EnviosSimultaneos));
        var resultados = await Task.WhenAll(lote.Select(async envio =>
        {
            await cupo.WaitAsync(ct);
            try
            {
                return await emisor.EnviarAsync(new MensajeSms(envio.Destino, envio.Texto, envio.AvisoId, envio.EsPrueba), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Una excepción del proveedor (red, caída) se trata como fallo pasajero: se reintenta.
                return ResultadoEnvioSms.FalloRecuperable($"Error inesperado del proveedor: {ex.Message}");
            }
            finally
            {
                cupo.Release();
            }
        }));

        var fin = reloj.GetUtcNow();
        var espera = TimeSpan.FromSeconds(Math.Max(0, opciones.EsperaReintentoSegundos));
        for (var i = 0; i < lote.Count; i++)
        {
            var r = resultados[i];
            lote[i].RegistrarIntento(new ResultadoIntento(r.Exitoso, r.IdProveedor, r.Error, r.Recuperable, r.Bloqueado),
                fin, opciones.MaxReintentos, espera);
        }
        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Cola SMS: lote de {Cantidad} envíos procesado en {Ms} ms", lote.Count, (fin - ahora).TotalMilliseconds);
        return lote.Count;
    }
}
