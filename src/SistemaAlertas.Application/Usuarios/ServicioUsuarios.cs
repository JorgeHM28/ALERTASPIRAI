using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Application.Usuarios;

public sealed record UsuarioDto(Guid Id, string NombreUsuario, string NombreCompleto, string Email, Rol Rol, bool Activo,
    DateTimeOffset CreadoEn, DateTimeOffset? UltimoAcceso)
{
    public static UsuarioDto De(Usuario u) =>
        new(u.Id, u.NombreUsuario, u.NombreCompleto, u.Email, u.Rol, u.Activo, u.CreadoEn, u.UltimoAcceso);
}

public sealed record CrearUsuario(string NombreUsuario, string NombreCompleto, string Email, Rol Rol, string Contrasena);
public sealed record ActualizarUsuario(string NombreCompleto, string Email, Rol Rol, bool Activo);
public sealed record ResultadoLogin(string Token, DateTimeOffset Expira, UsuarioDto Usuario);

/// <summary>Casos de uso de RF-13: autenticación y gestión de usuarios y roles.</summary>
public sealed class ServicioUsuarios(IAppDbContext db, IHasherContrasena hasher, IGeneradorToken tokens)
{
    public const int LongitudMinimaContrasena = 8;

    public async Task<ResultadoLogin?> LoginAsync(string nombreUsuario, string contrasena, CancellationToken ct)
    {
        var nombre = (nombreUsuario ?? string.Empty).Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombre, ct);
        if (usuario is null || !usuario.Activo || !hasher.Verificar(usuario, contrasena ?? string.Empty)) return null;

        usuario.RegistrarAcceso();
        await db.SaveChangesAsync(ct);
        var (token, expira) = tokens.Generar(usuario);
        return new ResultadoLogin(token, expira, UsuarioDto.De(usuario));
    }

    public async Task<Usuario> CrearAsync(CrearUsuario dto, CancellationToken ct)
    {
        ValidarContrasena(dto.Contrasena);
        var usuario = new Usuario(dto.NombreUsuario, dto.NombreCompleto, dto.Email, dto.Rol);
        if (await db.Usuarios.AnyAsync(u => u.NombreUsuario == usuario.NombreUsuario || u.Email == usuario.Email, ct))
            throw new ExcepcionDominio("Ya existe un usuario con ese nombre de usuario o email.");
        usuario.EstablecerHashContrasena(hasher.Hashear(usuario, dto.Contrasena));
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);
        return usuario;
    }

    public async Task<Usuario> ActualizarAsync(Guid id, ActualizarUsuario dto, Guid usuarioActualId, CancellationToken ct)
    {
        var usuario = await db.Usuarios.FindAsync([id], ct) ?? throw new KeyNotFoundException("Usuario no encontrado.");

        var dejaDeSerAdmin = usuario.Rol == Rol.Administrador && (dto.Rol != Rol.Administrador || !dto.Activo);
        if (dejaDeSerAdmin)
        {
            if (id == usuarioActualId) throw new ExcepcionDominio("No puedes quitarte tu propio rol de Administrador ni desactivarte.");
            var otrosAdmins = await db.Usuarios.CountAsync(u => u.Id != id && u.Rol == Rol.Administrador && u.Activo, ct);
            if (otrosAdmins == 0) throw new ExcepcionDominio("Debe quedar al menos un Administrador activo.");
        }

        var email = dto.Email.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Id != id && u.Email == email, ct))
            throw new ExcepcionDominio("Ya existe otro usuario con ese email.");

        usuario.Actualizar(dto.NombreCompleto, dto.Email, dto.Rol, dto.Activo);
        await db.SaveChangesAsync(ct);
        return usuario;
    }

    public async Task CambiarContrasenaAsync(Guid id, string nueva, CancellationToken ct)
    {
        ValidarContrasena(nueva);
        var usuario = await db.Usuarios.FindAsync([id], ct) ?? throw new KeyNotFoundException("Usuario no encontrado.");
        usuario.EstablecerHashContrasena(hasher.Hashear(usuario, nueva));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Crea el primer Administrador si la base no tiene usuarios.</summary>
    public async Task SembrarAdministradorAsync(string contrasena, CancellationToken ct)
    {
        if (await db.Usuarios.AnyAsync(ct)) return;
        await CrearAsync(new CrearUsuario("admin", "Administrador del sistema", "admin@sat-pirai.local", Rol.Administrador, contrasena), ct);
    }

    private static void ValidarContrasena(string? contrasena)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LongitudMinimaContrasena
            || !contrasena.Any(char.IsLetter) || !contrasena.Any(char.IsDigit))
            throw new ExcepcionDominio($"La contraseña debe tener al menos {LongitudMinimaContrasena} caracteres, con letras y números.");
    }
}
