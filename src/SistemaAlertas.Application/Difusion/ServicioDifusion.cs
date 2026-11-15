using SistemaAlertas.Application.Avisos.Eventos;

namespace SistemaAlertas.Application.Difusion;

/// <param name="TrabajosSms">Trabajos SMS creados (uno por destinatario); el envío lo hace la cola en segundo plano.</param>
public sealed record ResultadoDifusion(Guid VersionId, bool EsPrueba, bool PublicadoEnWeb, string? UrlWeb, int TrabajosSms,
    IReadOnlyList<TareaManual> TareasManuales, IReadOnlyList<IncidenciaDifusion> Incidencias);

public interface IServicioDifusion
{
    Task<ResultadoDifusion> DifundirAsync(Guid versionAprobadaId, CancellationToken ct = default);
}

/// <summary>
/// Patrón Facade (RF-04). Una sola llamada, <see cref="DifundirAsync"/>, coordina todo el subsistema de difusión.
/// Por dentro es el invocador del patrón Command: ejecuta los comandos de canal registrados (web, SMS, tareas manuales)
/// en orden. Cada canal es independiente: si uno falla, se registra la incidencia y los demás siguen.
/// Al terminar publica el evento Difundido (Observer), que pasa el aviso a ese estado (State) y queda en la bitácora.
/// La validación previa (Chain of Responsibility) la hace la fuente de versiones al entregar la versión.
/// </summary>
public sealed class ServicioDifusion(
    IFuenteVersionesAprobadas fuente,
    IEnumerable<IComandoDifusion> comandos,
    IRegistroIncidencias incidencias,
    IPublicadorEventosAvisos? eventos = null,
    TimeProvider? reloj = null) : IServicioDifusion
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<ResultadoDifusion> DifundirAsync(Guid versionAprobadaId, CancellationToken ct = default)
    {
        var version = await fuente.ObtenerAsync(versionAprobadaId, ct)
            ?? throw new KeyNotFoundException("No existe la versión aprobada indicada.");
        var contexto = new ContextoDifusion(version);
        var problemas = new List<IncidenciaDifusion>();

        foreach (var comando in comandos)
        {
            try
            {
                await comando.EjecutarAsync(contexto, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await Incidencia(version, comando.Canal, $"Falló: {ex.Message}", problemas, ct);
            }
            foreach (var detalle in contexto.TomarAnotaciones())
                await Incidencia(version, comando.Canal, detalle, problemas, ct);
        }

        var resultado = new ResultadoDifusion(version.VersionId, version.EsPrueba, contexto.Publicacion is not null,
            contexto.Publicacion?.Url, contexto.TrabajosSms, contexto.Tareas, problemas);

        var llego = resultado.PublicadoEnWeb || resultado.TrabajosSms > 0 || resultado.TareasManuales.Count > 0;
        if (llego && eventos is not null)
            await eventos.PublicarAsync(new EventoAviso(version.AvisoId, TipoEventoAviso.Difundido, null,
                $"Web: {(resultado.PublicadoEnWeb ? "sí" : "no")}, SMS: {resultado.TrabajosSms}, tareas: {resultado.TareasManuales.Count}, incidencias: {problemas.Count}.",
                _reloj.GetUtcNow(), version.VersionId), ct);
        return resultado;
    }

    private async Task Incidencia(VersionAprobada version, string canal, string detalle, List<IncidenciaDifusion> problemas, CancellationToken ct)
    {
        var incidencia = new IncidenciaDifusion(version.VersionId, canal, detalle, _reloj.GetUtcNow());
        problemas.Add(incidencia);
        try { await incidencias.RegistrarAsync(incidencia, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* la incidencia ya va en el resultado */ }
    }
}
