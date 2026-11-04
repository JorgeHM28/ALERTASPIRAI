using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Usuarios;

public class Usuario : Entidad
{
    public string NombreUsuario { get; private set; } = null!;
    public string NombreCompleto { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string HashContrasena { get; private set; } = null!;
    public Rol Rol { get; private set; }
    public bool Activo { get; private set; } = true;
    public DateTimeOffset? UltimoAcceso { get; private set; }

    private Usuario() { }

    public Usuario(string nombreUsuario, string nombreCompleto, string email, Rol rol)
    {
        if (string.IsNullOrWhiteSpace(nombreUsuario)) throw new ExcepcionDominio("El nombre de usuario es obligatorio.");
        if (string.IsNullOrWhiteSpace(nombreCompleto)) throw new ExcepcionDominio("El nombre completo es obligatorio.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ExcepcionDominio("El email no es válido.");
        NombreUsuario = nombreUsuario.Trim().ToLowerInvariant();
        NombreCompleto = nombreCompleto.Trim();
        Email = email.Trim().ToLowerInvariant();
        Rol = rol;
    }

    public void EstablecerHashContrasena(string hash) => HashContrasena = hash;

    public void Actualizar(string nombreCompleto, string email, Rol rol, bool activo)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto)) throw new ExcepcionDominio("El nombre completo es obligatorio.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ExcepcionDominio("El email no es válido.");
        NombreCompleto = nombreCompleto.Trim();
        Email = email.Trim().ToLowerInvariant();
        Rol = rol;
        Activo = activo;
    }

    public void RegistrarAcceso() => UltimoAcceso = DateTimeOffset.UtcNow;
}
