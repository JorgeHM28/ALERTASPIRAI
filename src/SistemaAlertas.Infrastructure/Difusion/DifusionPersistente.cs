using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Application.Mensajes;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Infrastructure.Difusion;

// Sprint 3: los canales de RF-04 guardan su trabajo en la base (antes vivían en memoria y se perdían al reiniciar).

/// <summary>
/// Emite el CAP final (RF-04): toma el XML CAP 1.2 que se aprobó, lo vuelve a validar contra el XSD oficial,
/// calcula su SHA-256 y lo archiva tal cual. Un Update o Cancel queda enlazado al CAP emitido de la versión que referencia.
/// </summary>
public sealed class ArchivoCapPersistente(IAppDbContext db, IValidadorCap validador, TimeProvider reloj) : IArchivoCap
{
    public async Task<CapEmitido> EmitirAsync(VersionAprobada v, CancellationToken ct = default)
    {
        var version = await db.VersionesMensaje.AsNoTracking().FirstAsync(x => x.Id == v.VersionId, ct);
        if (string.IsNullOrWhiteSpace(version.XmlCap))
            throw new ExcepcionDominio("La versión aprobada no tiene XML CAP: no se puede emitir.");
        if (await db.CapsEmitidos.AnyAsync(c => c.VersionId == v.VersionId, ct))
            throw new ExcepcionDominio("Esta versión ya tiene su CAP emitido: no se difunde dos veces.");

        var validacion = validador.Validar(version.XmlCap);
        if (!validacion.Valido)
            throw new ExcepcionDominio("El CAP final no es válido contra CAP 1.2 y no se difunde: " + string.Join(" ", validacion.Errores));

        Guid? referencia = null;
        if (version.ReferenciaVersionId is { } refId)
        {
            referencia = await db.CapsEmitidos.Where(c => c.VersionId == refId).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct)
                ?? throw new ExcepcionDominio("La versión referenciada todavía no se difundió: primero hay que emitir su CAP.");
        }

        var cap = new CapEmitido(version.Id, version.AvisoId, version.IdentificadorCap, version.Remitente,
            version.EnviadoCap ?? reloj.GetUtcNow(), version.EstadoCap.ToString(), version.Tipo.ToString(), version.XmlCap,
            Sha256(version.XmlCap), validacion.Valido, referencia, reloj.GetUtcNow());
        db.CapsEmitidos.Add(cap);
        await db.SaveChangesAsync(ct);
        return cap;
    }

    public static string Sha256(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
}

/// <summary>Marca la versión como publicada en la web. La página pública (/api/publico) ya muestra las versiones aprobadas vigentes.</summary>
public sealed class PublicadorWebPersistente(IAppDbContext db, TimeProvider reloj) : IPublicadorWeb
{
    public async Task<PublicacionWeb> PublicarAsync(VersionAprobada v, CancellationToken ct = default)
    {
        var version = await db.VersionesMensaje.FirstAsync(x => x.Id == v.VersionId, ct);
        version.MarcarPublicadaWeb(reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return new PublicacionWeb(v.VersionId, $"/?aviso={v.VersionId}", version.PublicadaWebEn!.Value);
    }
}

/// <summary>La cola SMS es la tabla de trabajos: encolar es guardar los envíos en estado Pendiente.</summary>
public sealed class ColaSmsPersistente(IAppDbContext db) : IColaSms
{
    public async Task<int> EncolarAsync(IReadOnlyCollection<EnvioSms> envios, CancellationToken ct = default)
    {
        db.TrabajosDifusion.AddRange(envios);
        await db.SaveChangesAsync(ct);
        return envios.Count;
    }
}

/// <summary>
/// Tareas de los canales manuales. El texto de cada una lo redacta la estrategia de su canal (Strategy).
/// Radio y redes: una tarea para toda el área. Altavoz: una por comunidad del alcance (RF-07), también las pendientes,
/// porque el altavoz no depende de que la comunidad tenga contactos.
/// </summary>
public sealed class GestorTareasPersistente : IGestorTareasManuales
{
    private readonly IAppDbContext _db;
    private readonly ServicioDestinos _destinos;
    private readonly TimeProvider _reloj;
    private readonly RedactorTareas _redactor;

    public GestorTareasPersistente(IAppDbContext db, ServicioDestinos destinos, TimeProvider reloj, IEnumerable<IRedaccionCanal>? redacciones = null)
    {
        _db = db;
        _destinos = destinos;
        _reloj = reloj;
        _redactor = new RedactorTareas(redacciones);
    }

    public async Task<IReadOnlyList<TareaManual>> CrearAsync(VersionAprobada v, IReadOnlyCollection<CanalDifusion> canales,
        CancellationToken ct = default)
    {
        var ahora = _reloj.GetUtcNow();
        var tareas = new List<TareaManual>();
        List<Guid>? comunidades = null;
        foreach (var canal in canales)
        {
            var texto = _redactor.Redactar(canal, v);
            if (canal == CanalDifusion.Altavoz)
            {
                comunidades ??= await ComunidadesDelAlcance(v.VersionId, ct);
                tareas.AddRange(comunidades.Select(c => new TareaManual(v.VersionId, v.AvisoId, c, canal, texto, v.EsPrueba, ahora)));
            }
            else
            {
                tareas.Add(new TareaManual(v.VersionId, v.AvisoId, null, canal, texto, v.EsPrueba, ahora));
            }
        }
        _db.TrabajosDifusion.AddRange(tareas);
        await _db.SaveChangesAsync(ct);
        return tareas;
    }

    private async Task<List<Guid>> ComunidadesDelAlcance(Guid versionId, CancellationToken ct)
    {
        var lista = await _destinos.ObtenerOGenerarAsync(versionId, ct);
        return lista.Destinos.Select(d => d.ComunidadId).Concat(lista.Pendientes.Select(p => p.ComunidadId)).Distinct().ToList();
    }
}

/// <summary>Deja cada incidencia en el log y en la bitácora del aviso, para verla en el seguimiento (RF-08).</summary>
public sealed class RegistroIncidenciasPersistente(IAppDbContext db, ILogger<RegistroIncidenciasPersistente> logger) : IRegistroIncidencias
{
    public async Task RegistrarAsync(IncidenciaDifusion i, CancellationToken ct = default)
    {
        logger.LogWarning("Incidencia de difusión en {Canal} (versión {VersionId}): {Detalle}", i.Canal, i.VersionId, i.Detalle);
        var avisoId = await db.VersionesMensaje.Where(v => v.Id == i.VersionId).Select(v => v.AvisoId).FirstOrDefaultAsync(ct);
        db.EventosAviso.Add(new RegistroEventoAviso(avisoId, i.VersionId, "Incidencia", null, $"{i.Canal}: {i.Detalle}", i.Fecha));
        await db.SaveChangesAsync(ct);
    }
}
