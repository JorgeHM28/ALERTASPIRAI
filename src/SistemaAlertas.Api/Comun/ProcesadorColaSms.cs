using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Infrastructure.Difusion;

namespace SistemaAlertas.Api.Comun;

/// <summary>
/// Servicio en segundo plano de la cola SMS. Cada pocos segundos (Sms:IntervaloColaSegundos) procesa lotes hasta vaciar
/// lo pendiente. Como la cola está en la base, lo que quedó pendiente antes de un reinicio se retoma solo.
/// </summary>
public sealed class ProcesadorColaSms(IServiceScopeFactory scopes, OpcionesSms opciones, ILogger<ProcesadorColaSms> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(1, opciones.IntervaloColaSegundos));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int procesados;
                do
                {
                    using var scope = scopes.CreateScope();
                    procesados = await scope.ServiceProvider.GetRequiredService<DespachadorEnvios>().ProcesarLoteAsync(stoppingToken);
                } while (procesados > 0 && !stoppingToken.IsCancellationRequested);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falló una vuelta de la cola SMS; se reintenta en la siguiente.");
            }
            try { await Task.Delay(intervalo, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
