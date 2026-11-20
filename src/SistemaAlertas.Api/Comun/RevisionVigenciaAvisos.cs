using SistemaAlertas.Application.Avisos;

namespace SistemaAlertas.Api.Comun;

/// <summary>Cada 5 minutos marca como vencidos (no aptos para difusión) los avisos cuya expiración pasó (RF-01).</summary>
public sealed class RevisionVigenciaAvisos(IServiceScopeFactory scopes, ILogger<RevisionVigenciaAvisos> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var reloj = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var vencidos = await scope.ServiceProvider.GetRequiredService<ServicioAvisos>().RevisarVigenciaAsync(ct);
                if (vencidos > 0) logger.LogInformation("{Cantidad} aviso(s) vencido(s) marcados como no aptos para difusión", vencidos);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "No se pudo revisar la vigencia de los avisos");
            }
        } while (await reloj.WaitForNextTickAsync(ct));
    }
}
