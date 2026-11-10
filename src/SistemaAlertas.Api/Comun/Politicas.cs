using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Api.Comun;

/// <summary>
/// Políticas de autorización de RF-13. Cada endpoint declara un permiso; la política acepta a cualquier usuario
/// que tenga al menos un rol con ese permiso (ver <see cref="MatrizPermisos"/>).
/// </summary>
public static class Politicas
{
    public const string Lectura = nameof(Permiso.Lectura);
    public const string Edicion = nameof(Permiso.Edicion);
    public const string Aprobacion = nameof(Permiso.Aprobacion);
    public const string Administracion = nameof(Permiso.Administracion);

    public static IEnumerable<(string Politica, IReadOnlyList<Rol> Roles)> Todas() =>
        Enum.GetValues<Permiso>().Select(p => (p.ToString(), MatrizPermisos.RolesCon(p)));
}
