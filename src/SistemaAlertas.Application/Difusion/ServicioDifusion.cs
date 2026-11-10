using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Application.Difusion;

/// <param name="TrabajosSms">Trabajos SMS creados (uno por destinatario); el envío lo hace la cola en segundo plano.</param>
public sealed record ResultadoDifusion(Guid VersionId, bool EsPrueba, bool PublicadoEnWeb, string? UrlWeb, int TrabajosSms,
    IReadOnlyList<TareaManual> TareasManuales, IReadOnlyList<IncidenciaDifusion> Incidencias);

public interface IServicioDifusion
{
    Task<ResultadoDifusion> DifundirAsync(Guid versionAprobadaId, CancellationToken ct = default);
}

/// <summary>
/// Patrón Facade (RF-04). Una sola llamada, <see cref="DifundirAsync"/>, coordina todo el subsistema de difusión:
/// publicar en la web, crear los trabajos SMS y crear las tareas manuales de radio, altavoz y redes sociales.
/// Cada canal es independiente: si uno falla (por ejemplo, la web), se registra la incidencia y los demás siguen.
/// </summary>
public sealed class ServicioDifusion(
    IFuenteVersionesAprobadas fuente,
    IPublicadorWeb publicadorWeb,
    IDestinatariosDifusion destinatarios,
    IColaSms colaSms,
    IGestorTareasManuales tareasManuales,
    IRegistroIncidencias incidencias,
    TimeProvider? reloj = null) : IServicioDifusion
{
    public static readonly IReadOnlyList<CanalManual> CanalesManuales = [CanalManual.Radio, CanalManual.Altavoz, CanalManual.RedesSociales];

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<ResultadoDifusion> DifundirAsync(Guid versionAprobadaId, CancellationToken ct = default)
    {
        var version = await fuente.ObtenerAsync(versionAprobadaId, ct)
            ?? throw new KeyNotFoundException("No existe la versión aprobada indicada.");
        var problemas = new List<IncidenciaDifusion>();

        // 1. Web pública.
        PublicacionWeb? publicacion = null;
        await Canal("Web", version, problemas, ct, async () => publicacion = await publicadorWeb.PublicarAsync(version, ct));

        // 2. SMS: un trabajo por destinatario; el envío (con reintentos y bitácora) lo hace la cola.
        var trabajos = 0;
        await Canal("SMS", version, problemas, ct, async () =>
        {
            if (version.ZonaIds.Count == 0)
            {
                await Incidencia(version, "SMS", "El aviso no cubre ninguna zona: no se crearon trabajos SMS.", problemas, ct);
                return;
            }
            var lista = await destinatarios.ObtenerSmsAsync(version.ZonaIds, ct);
            if (lista.Count == 0)
            {
                await Incidencia(version, "SMS", "No hay contactos habilitados con SMS en las zonas del aviso.", problemas, ct);
                return;
            }
            trabajos = await colaSms.EncolarAsync(lista.Select(d => new TrabajoSms(Guid.NewGuid(), version.VersionId, d.ContactoId,
                new MensajeSms(d.Telefono, version.TextoSms, version.AvisoId, version.EsPrueba))).ToList(), ct);
        });

        // 3. Canales manuales.
        IReadOnlyList<TareaManual> tareas = [];
        await Canal("Tareas manuales", version, problemas, ct,
            async () => tareas = await tareasManuales.CrearAsync(version, CanalesManuales, ct));

        return new ResultadoDifusion(version.VersionId, version.EsPrueba, publicacion is not null, publicacion?.Url, trabajos, tareas, problemas);
    }

    /// <summary>Ejecuta un canal; si falla, registra la incidencia y deja seguir a los demás.</summary>
    private async Task Canal(string canal, VersionAprobada version, List<IncidenciaDifusion> problemas, CancellationToken ct, Func<Task> accion)
    {
        try
        {
            await accion();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Incidencia(version, canal, $"Falló: {ex.Message}", problemas, ct);
        }
    }

    private async Task Incidencia(VersionAprobada version, string canal, string detalle, List<IncidenciaDifusion> problemas, CancellationToken ct)
    {
        var incidencia = new IncidenciaDifusion(version.VersionId, canal, detalle, _reloj.GetUtcNow());
        problemas.Add(incidencia);
        try { await incidencias.RegistrarAsync(incidencia, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* la incidencia ya va en el resultado */ }
    }
}
