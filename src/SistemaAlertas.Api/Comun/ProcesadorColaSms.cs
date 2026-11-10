using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Infrastructure.Difusion;

namespace SistemaAlertas.Api.Comun;

/// <summary>Envía en segundo plano los trabajos SMS que crea la difusión, con el emisor decorado (reintentos, bitácora, modo prueba).</summary>
public sealed class ProcesadorColaSms(ColaSmsEnMemoria cola, IEmisorSms emisor, ILogger<ProcesadorColaSms> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var trabajo in cola.Pendientes.ReadAllAsync(stoppingToken))
        {
            try
            {
                await emisor.EnviarAsync(trabajo.Mensaje, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "No se pudo procesar el trabajo SMS {Trabajo}", trabajo.Id);
            }
        }
    }
}
