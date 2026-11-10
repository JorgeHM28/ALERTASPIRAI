using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SistemaAlertas.Application.Difusion;

namespace SistemaAlertas.Infrastructure.Difusion;

// Implementaciones simples de los canales de RF-04 para el Sprint 2. La web pública, la cola persistente
// y la gestión de tareas con su propia tabla llegan en el Sprint 3 detrás de estas mismas interfaces.

/// <summary>Guarda las publicaciones en memoria; la web pública las leerá en el Sprint 3.</summary>
public sealed class PublicadorWebEnMemoria(TimeProvider reloj) : IPublicadorWeb
{
    private readonly ConcurrentDictionary<Guid, (VersionAprobada Version, PublicacionWeb Publicacion)> _publicadas = new();

    public Task<PublicacionWeb> PublicarAsync(VersionAprobada version, CancellationToken ct = default)
    {
        var publicacion = new PublicacionWeb(version.VersionId, $"/avisos/{version.VersionId}", reloj.GetUtcNow());
        _publicadas[version.VersionId] = (version, publicacion);
        return Task.FromResult(publicacion);
    }

    public IReadOnlyList<PublicacionWeb> Publicadas => _publicadas.Values.Select(p => p.Publicacion).ToList();
}

/// <summary>Cola SMS en memoria (canal sin límite). <c>ProcesadorColaSms</c> la consume y envía con IEmisorSms.</summary>
public sealed class ColaSmsEnMemoria : IColaSms
{
    private readonly Channel<TrabajoSms> _canal = Channel.CreateUnbounded<TrabajoSms>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<TrabajoSms> Pendientes => _canal.Reader;

    public async Task<int> EncolarAsync(IReadOnlyCollection<TrabajoSms> trabajos, CancellationToken ct = default)
    {
        foreach (var t in trabajos) await _canal.Writer.WriteAsync(t, ct);
        return trabajos.Count;
    }
}

public sealed class GestorTareasManualesEnMemoria(TimeProvider reloj) : IGestorTareasManuales
{
    private readonly ConcurrentQueue<TareaManual> _tareas = new();

    public Task<IReadOnlyList<TareaManual>> CrearAsync(VersionAprobada version, IReadOnlyCollection<CanalManual> canales,
        CancellationToken ct = default)
    {
        var prefijo = version.EsPrueba ? "[PRUEBA] " : string.Empty;
        var nuevas = canales.Select(c => new TareaManual(Guid.NewGuid(), version.VersionId, c, prefijo + Instrucciones(c, version),
            EstadoTarea.Pendiente, reloj.GetUtcNow())).ToList();
        foreach (var t in nuevas) _tareas.Enqueue(t);
        return Task.FromResult<IReadOnlyList<TareaManual>>(nuevas);
    }

    public IReadOnlyList<TareaManual> Listar(Guid? versionId = null) =>
        _tareas.Where(t => versionId is null || t.VersionId == versionId).ToList();

    private static string Instrucciones(CanalManual canal, VersionAprobada v) => canal switch
    {
        CanalManual.Radio => $"Leer al aire en las radios comunitarias: {v.TextoCompleto}",
        CanalManual.Altavoz => $"Anunciar por altavoz en las comunidades de las zonas afectadas: {v.TextoSms}",
        CanalManual.RedesSociales => $"Publicar en las redes oficiales: {v.TextoSms}",
        _ => v.TextoSms
    };
}

public sealed class RegistroIncidenciasEnLog(ILogger<RegistroIncidenciasEnLog> logger) : IRegistroIncidencias
{
    public Task RegistrarAsync(IncidenciaDifusion i, CancellationToken ct = default)
    {
        logger.LogWarning("Incidencia de difusión en {Canal} (versión {VersionId}): {Detalle}", i.Canal, i.VersionId, i.Detalle);
        return Task.CompletedTask;
    }
}
