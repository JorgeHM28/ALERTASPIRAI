using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Application.Mensajes;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Mensajes;

namespace SistemaAlertas.Infrastructure.Difusion;

/// <summary>
/// Entrega a la difusión una versión aprobada del mensaje (RF-11). Antes, la versión y su aviso recorren la cadena
/// de reglas de difusión (Chain of Responsibility): aviso no descartado, versión aprobada, no reemplazada, vigente y completa.
/// Arma el texto de cada canal con el nivel escrito en palabras (RF-06): el SMS, la radio y el altavoz no tienen color.
/// </summary>
public sealed class FuenteVersionesAprobadas(IAppDbContext db, ReglaDifusion reglas, TimeProvider reloj) : IFuenteVersionesAprobadas
{
    public async Task<VersionAprobada?> ObtenerAsync(Guid versionAprobadaId, CancellationToken ct = default)
    {
        var version = await db.VersionesMensaje.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionAprobadaId, ct);
        if (version is null) return null;
        var aviso = await db.Avisos.AsNoTracking().FirstAsync(a => a.Id == version.AvisoId, ct);

        reglas.Verificar(version, aviso, reloj.GetUtcNow());

        var area = version.Area;
        var zonas = area is null ? []
            : await db.Zonas.AsNoTracking().Where(z => z.Area.Intersects(area)).Select(z => z.Id).ToListAsync(ct);
        var (sms, completo) = TextosMensaje.De(version);
        return new VersionAprobada(version.Id, version.AvisoId, version.Contenido.Titular ?? version.Contenido.Evento,
            completo, sms, zonas, version.EsPrueba);
    }
}

/// <summary>Los SMS salen de la lista de destinos de la versión (RF-07): sin duplicados y solo del alcance.</summary>
public sealed class DestinatariosDesdeLista(ServicioDestinos destinos, IAppDbContext db) : IDestinatariosDifusion
{
    public async Task<IReadOnlyList<DestinatarioSms>> ObtenerSmsAsync(VersionAprobada version, CancellationToken ct = default)
    {
        var lista = await destinos.ObtenerOGenerarAsync(version.VersionId, ct);
        var sms = lista.Destinos.Where(d => d.Canal == Domain.Comunidades.TipoCanal.Sms).ToList();
        var ids = sms.Select(d => d.ContactoId).Distinct().ToList();
        // La lista se guardó al aprobar. Al difundir se vuelve a mirar el contacto: si se desactivó, se le quitó la
        // autorización o se borró (con su comunidad), ya no recibe el SMS (restricción 4 del diagrama ER).
        var habilitados = await db.Contactos.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && c.Activo && c.Autorizado)
            .ToDictionaryAsync(c => c.Id, c => c.Nombre, ct);
        return sms.Where(d => habilitados.ContainsKey(d.ContactoId))
            .Select(d => new DestinatarioSms(d.ContactoId, d.ComunidadId, habilitados[d.ContactoId], d.Direccion)).ToList();
    }
}
