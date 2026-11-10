using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Usuarios;

/// <summary>Usuario del sistema. Puede tener varios roles a la vez; sus permisos son la unión de los de cada rol (RF-13).</summary>
public class Usuario : Entidad
{
    public string NombreUsuario { get; private set; } = null!;
    public string NombreCompleto { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string HashContrasena { get; private set; } = null!;
    public bool Activo { get; private set; } = true;
    public DateTimeOffset? UltimoAcceso { get; private set; }

    private readonly List<AsignacionRol> _asignaciones = [];
    /// <summary>Roles vigentes, con quién y cuándo los asignó.</summary>
    public IReadOnlyCollection<AsignacionRol> Asignaciones => _asignaciones;
    public IReadOnlyList<Rol> Roles => _asignaciones.Select(a => a.Rol).Order().ToList();
    public IReadOnlyList<Permiso> Permisos => MatrizPermisos.De(Roles);

    private Usuario() { }

    public Usuario(string nombreUsuario, string nombreCompleto, string email, IEnumerable<Rol> roles, Guid? asignadoPorId)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario)) throw new ExcepcionDominio("El nombre de usuario es obligatorio.");
        NombreUsuario = nombreUsuario.Trim().ToLowerInvariant();
        ActualizarDatos(nombreCompleto, email);
        AsignarRoles(roles, asignadoPorId);
    }

    public void EstablecerHashContrasena(string hash) => HashContrasena = hash;

    public void ActualizarDatos(string nombreCompleto, string email)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto)) throw new ExcepcionDominio("El nombre completo es obligatorio.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ExcepcionDominio("El email no es válido.");
        NombreCompleto = nombreCompleto.Trim();
        Email = email.Trim().ToLowerInvariant();
    }

    /// <summary>Deja exactamente estos roles. Devuelve los que se agregaron y los que se quitaron, para la bitácora.</summary>
    public (IReadOnlyList<Rol> Agregados, IReadOnlyList<Rol> Quitados) AsignarRoles(IEnumerable<Rol>? roles, Guid? asignadoPorId)
    {
        var nuevos = (roles ?? []).Distinct().ToList();
        if (nuevos.Count == 0) throw new ExcepcionDominio("El usuario necesita al menos un rol.");
        if (nuevos.Any(r => !Enum.IsDefined(r))) throw new ExcepcionDominio("Hay un rol que no existe.");

        var actuales = Roles;
        var agregados = nuevos.Except(actuales).Order().ToList();
        var quitados = actuales.Except(nuevos).Order().ToList();
        _asignaciones.RemoveAll(a => quitados.Contains(a.Rol));
        _asignaciones.AddRange(agregados.Select(r => new AsignacionRol(r, asignadoPorId)));
        return (agregados, quitados);
    }

    public bool TieneRol(Rol rol) => _asignaciones.Any(a => a.Rol == rol);

    /// <summary>Devuelve true si el estado cambió.</summary>
    public bool CambiarActivo(bool activo)
    {
        if (Activo == activo) return false;
        Activo = activo;
        return true;
    }

    public void RegistrarAcceso() => UltimoAcceso = DateTimeOffset.UtcNow;
}

/// <summary>Rol vigente de un usuario (objeto de valor).</summary>
public class AsignacionRol
{
    public Rol Rol { get; private set; }
    public DateTimeOffset AsignadoEn { get; private set; }
    /// <summary>Administrador que lo asignó; null si lo creó el sistema (usuario semilla).</summary>
    public Guid? AsignadoPorId { get; private set; }

    private AsignacionRol() { }

    internal AsignacionRol(Rol rol, Guid? asignadoPorId)
    {
        Rol = rol;
        AsignadoEn = DateTimeOffset.UtcNow;
        AsignadoPorId = asignadoPorId;
    }
}
