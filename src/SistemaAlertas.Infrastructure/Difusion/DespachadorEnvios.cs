using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Application.Difusion.Canales;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Infrastructure.Difusion;

/// <summary>
/// Procesa un lote de la cola de envíos automáticos (RF-04, RNF-02, RNF-08): SMS, WhatsApp a grupos y Email.
/// Toma los pendientes cuyo próximo intento ya llegó, los manda por el canal de cada uno en paralelo (con un tope de
/// envíos simultáneos) y guarda el resultado. Un fallo recuperable queda agendado 60 s después, hasta 2 reintentos;
/// un fallo definitivo termina el envío.
/// RNF-08: antes de cada envío o reintento se mira la versión. Si fue reemplazada, ya no está aprobada o venció,
/// el envío se cancela con el motivo y no sale.
/// Lo llama ProcesadorColaSms cada pocos segundos; es una clase aparte para poder probarla sin el servicio en segundo plano.
/// </summary>
public sealed class DespachadorEnvios(IAppDbContext db, IVersionesDifusion versiones, IEnumerable<IEmisorCanal> canales,
    OpcionesSms opciones, TimeProvider reloj, ILogger<DespachadorEnvios>? logger = null)
{
    private readonly Dictionary<CanalDifusion, IEmisorCanal> _canales = canales.ToDictionary(c => c.Canal);

    /// <returns>Cuántos envíos se procesaron en este lote (enviados, fallidos o cancelados).</returns>
    public async Task<int> ProcesarLoteAsync(CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        var lote = await db.TrabajosDifusion.OfType<EnvioAutomatico>()
            .Where(e => e.Estado == EstadoTrabajo.Pendiente && e.ProximoIntento != null && e.ProximoIntento <= ahora)
            .OrderBy(e => e.ProximoIntento).ThenBy(e => e.CreadoEn)
            .Take(Math.Max(1, opciones.TamanoLote))
            .ToListAsync(ct);
        if (lote.Count == 0) return 0;

        var motivos = await versiones.MotivosParaDetenerAsync(lote.Select(e => e.VersionId).Distinct().ToList(), ahora, ct);
        var cancelados = 0;
        foreach (var envio in lote)
            if (motivos.GetValueOrDefault(envio.VersionId) is { } motivo && envio.Cancelar(motivo, ahora)) cancelados++;
        var porEnviar = lote.Where(e => !e.Terminado).ToList();

        using var cupo = new SemaphoreSlim(Math.Max(1, opciones.EnviosSimultaneos));
        var resultados = await Task.WhenAll(porEnviar.Select(async envio =>
        {
            if (!_canales.TryGetValue(envio.Canal, out var canal))
                return ResultadoEnvioSms.FalloDefinitivo($"El canal {envio.Canal} no está configurado en el servidor.");
            // Un aviso de prueba o simulacro no llega a personas reales por ningún canal.
            if (envio.EsPrueba && canal.EnviaReal)
                return ResultadoEnvioSms.BloqueadoPorPrueba($"Aviso de prueba: no se envía por {canal.Proveedor} a destinatarios reales.");
            await cupo.WaitAsync(ct);
            try
            {
                return await canal.EnviarAsync(new MensajeCanal(envio.Destino, envio.Texto, envio.Asunto, envio.AvisoId, envio.EsPrueba), ct);
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
        for (var i = 0; i < porEnviar.Count; i++)
        {
            var r = resultados[i];
            porEnviar[i].RegistrarIntento(new ResultadoIntento(r.Exitoso, r.IdProveedor, r.Error, r.Recuperable, r.Bloqueado),
                fin, opciones.MaxReintentos, espera);
        }
        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Cola de envíos: {Enviados} procesados y {Cancelados} cancelados en {Ms} ms",
            porEnviar.Count, cancelados, (fin - ahora).TotalMilliseconds);
        return lote.Count;
    }
}
