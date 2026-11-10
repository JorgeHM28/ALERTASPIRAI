namespace SistemaAlertas.Domain.Usuarios;

/// <summary>Permisos del sistema. Cada endpoint de la API exige uno (RF-13).</summary>
public enum Permiso
{
    /// <summary>Ver datos.</summary>
    Lectura,
    /// <summary>Crear y modificar zonas, comunidades, contactos y avisos.</summary>
    Edicion,
    /// <summary>Aprobar mensajes antes de difundirlos (flujo en Sprint 2).</summary>
    Aprobacion,
    /// <summary>Gestionar usuarios y sus roles.</summary>
    Administracion
}

/// <summary>Qué permisos da cada rol. Un usuario con varios roles suma los permisos de todos.</summary>
public static class MatrizPermisos
{
    public static readonly IReadOnlyDictionary<Rol, Permiso[]> PorRol = new Dictionary<Rol, Permiso[]>
    {
        [Rol.Administrador] = [Permiso.Lectura, Permiso.Edicion, Permiso.Aprobacion, Permiso.Administracion],
        [Rol.Operador] = [Permiso.Lectura, Permiso.Edicion],
        [Rol.Aprobador] = [Permiso.Lectura, Permiso.Aprobacion],
        [Rol.Consulta] = [Permiso.Lectura]
    };

    public static IReadOnlyList<Permiso> De(IEnumerable<Rol> roles) =>
        roles.SelectMany(r => PorRol.GetValueOrDefault(r, [])).Distinct().Order().ToList();

    public static IReadOnlyList<Rol> RolesCon(Permiso permiso) =>
        PorRol.Where(p => p.Value.Contains(permiso)).Select(p => p.Key).ToList();
}
