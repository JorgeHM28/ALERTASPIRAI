using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Comunidades;
using SistemaAlertas.Domain.Comunidades;

namespace SistemaAlertas.Infrastructure.Persistence.Repositorios;

/// <summary>
/// Sujeto real del Proxy: lee los contactos de la base de datos con todos sus datos.
/// </summary>
public sealed class RepositorioContactos(IAppDbContext db) : IRepositorioContactos
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ContactoVista>> ListarPorComunidadAsync(Guid comunidadId, CancellationToken ct = default) =>
        (await db.Contactos.AsNoTracking().Where(c => c.ComunidadId == comunidadId).OrderBy(c => c.Nombre).ToListAsync(ct))
            .Select(AVista).ToList();

    public async Task<IReadOnlyList<ContactoVista>> ListarHabilitadosAsync(IReadOnlyCollection<Guid>? zonaIds, CancellationToken ct = default)
    {
        var q = db.Contactos.AsNoTracking().Where(c => c.Activo && c.Autorizado);
        if (zonaIds is { Count: > 0 })
        {
            var ids = zonaIds.ToArray();
            q = q.Where(c => db.Comunidades.Any(m => m.Id == c.ComunidadId && ids.Contains(m.ZonaId)));
        }
        return (await q.OrderBy(c => c.Nombre).ToListAsync(ct)).Where(c => c.Habilitado).Select(AVista).ToList();
    }

    public async Task<IReadOnlyList<HistorialContactoVista>> HistorialAsync(Guid contactoId, CancellationToken ct = default) =>
        (await db.HistorialContactos.AsNoTracking().Where(h => h.ContactoId == contactoId)
            .OrderByDescending(h => h.CreadoEn).ToListAsync(ct))
        .Select(h => new HistorialContactoVista(h.Id, h.CreadoEn, h.Accion, h.UsuarioId, h.Cambios,
            JsonSerializer.Deserialize<InstantaneaContacto>(h.Instantanea, Json)!))
        .ToList();

    public static ContactoVista AVista(Contacto c) => new(c.Id, c.ComunidadId, c.Nombre, c.Cargo, c.Activo,
        c.Canales.OrderByDescending(k => k.Preferido).Select(k => new CanalVista(k.Tipo, k.Destino, k.Preferido)).ToList(),
        c.Procedencia, c.Autorizado, c.ReferenciaAutorizacion, c.FechaAutorizacion, c.Habilitado, c.MotivosNoHabilitado);
}
