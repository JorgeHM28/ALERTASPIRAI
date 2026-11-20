using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Application.Comunidades;

public sealed record CanalVista(TipoCanal Tipo, string Destino, bool Preferido);

/// <param name="DatosOcultos">True si los teléfonos y correos vienen enmascarados para este usuario.</param>
public sealed record ContactoVista(Guid Id, Guid ComunidadId, string Nombre, string? Cargo, bool Activo,
    IReadOnlyList<CanalVista> Canales, string Procedencia, bool Autorizado, string? ReferenciaAutorizacion,
    DateOnly? FechaAutorizacion, bool Habilitado, IReadOnlyList<string> MotivosNoHabilitado, bool DatosOcultos = false);

public sealed record HistorialContactoVista(Guid Id, DateTimeOffset Fecha, AccionContacto Accion, Guid UsuarioId,
    string? Cambios, InstantaneaContacto Instantanea);

/// <summary>
/// Lectura de contactos de RF-12 para mostrarlos. Es el sujeto del patrón Proxy: la API recibe siempre
/// <c>ProxyRepositorioContactos</c>, que deja pasar los números completos solo a quien tiene permiso.
/// </summary>
public interface IRepositorioContactos
{
    /// <summary>Permiso que da acceso a los teléfonos y correos completos: el de quien mantiene los contactos.</summary>
    public const Permiso PermisoVerContactos = Permiso.Edicion;

    Task<IReadOnlyList<ContactoVista>> ListarPorComunidadAsync(Guid comunidadId, CancellationToken ct = default);

    /// <summary>Contactos activos, autorizados y con canal de las zonas indicadas (todas si es null o vacío).</summary>
    Task<IReadOnlyList<ContactoVista>> ListarHabilitadosAsync(IReadOnlyCollection<Guid>? zonaIds, CancellationToken ct = default);

    /// <summary>Historial del contacto, del más reciente al más antiguo. Vacío si no hay registros.</summary>
    Task<IReadOnlyList<HistorialContactoVista>> HistorialAsync(Guid contactoId, CancellationToken ct = default);
}
