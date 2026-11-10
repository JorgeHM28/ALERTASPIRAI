using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Application.Usuarios;

public sealed record UsuarioDto(Guid Id, string NombreUsuario, string NombreCompleto, string Email,
    IReadOnlyList<Rol> Roles, IReadOnlyList<Permiso> Permisos, bool Activo, DateTimeOffset CreadoEn, DateTimeOffset? UltimoAcceso)
{
    public static UsuarioDto De(Usuario u) =>
        new(u.Id, u.NombreUsuario, u.NombreCompleto, u.Email, u.Roles, u.Permisos, u.Activo, u.CreadoEn, u.UltimoAcceso);
}

public sealed record CrearUsuario(string NombreUsuario, string NombreCompleto, string Email, IReadOnlyList<Rol> Roles, string Contrasena);
public sealed record ActualizarUsuario(string NombreCompleto, string Email, IReadOnlyList<Rol> Roles, bool Activo);
public sealed record ResultadoLogin(string Token, DateTimeOffset Expira, UsuarioDto Usuario);
public sealed record RegistroBitacoraDto(Guid Id, DateTimeOffset Fecha, AccionUsuario Accion, Rol? Rol, string? Detalle,
    Guid? AdministradorId, string? Administrador);

/// <summary>Casos de uso de RF-13: autenticación, gestión de usuarios, roles y bitácora de asignaciones.</summary>
public sealed class ServicioUsuarios(IAppDbContext db, IHasherContrasena hasher, IGeneradorToken tokens)
{
    public const int LongitudMinimaContrasena = 8;

    private IQueryable<Usuario> ConRoles => db.Usuarios.Include(u => u.Asignaciones);

    public async Task<ResultadoLogin?> LoginAsync(string nombreUsuario, string contrasena, CancellationToken ct)
    {
        var nombre = (nombreUsuario ?? string.Empty).Trim().ToLowerInvariant();
        var usuario = await ConRoles.FirstOrDefaultAsync(u => u.NombreUsuario == nombre, ct);
        if (usuario is null || !usuario.Activo || !hasher.Verificar(usuario, contrasena ?? string.Empty)) return null;

        usuario.RegistrarAcceso();
        await db.SaveChangesAsync(ct);
        var (token, expira) = tokens.Generar(usuario);
        return new ResultadoLogin(token, expira, UsuarioDto.De(usuario));
    }

    public Task<Usuario?> ObtenerAsync(Guid id, CancellationToken ct) => ConRoles.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<List<Usuario>> ListarAsync(CancellationToken ct) =>
        await ConRoles.AsNoTracking().OrderBy(u => u.NombreUsuario).ToListAsync(ct);

    /// <param name="administradorId">Quién crea el usuario; null para el usuario semilla.</param>
    public async Task<Usuario> CrearAsync(CrearUsuario dto, Guid? administradorId, CancellationToken ct)
    {
        ValidarContrasena(dto.Contrasena);
        var usuario = new Usuario(dto.NombreUsuario, dto.NombreCompleto, dto.Email, dto.Roles ?? [], administradorId);
        if (await db.Usuarios.AnyAsync(u => u.NombreUsuario == usuario.NombreUsuario || u.Email == usuario.Email, ct))
            throw new ExcepcionDominio("Ya existe un usuario con ese nombre de usuario o email.");
        usuario.EstablecerHashContrasena(hasher.Hashear(usuario, dto.Contrasena));
        db.Usuarios.Add(usuario);

        db.BitacoraUsuarios.Add(new RegistroBitacoraUsuario(usuario.Id, administradorId, AccionUsuario.Alta,
            detalle: $"Alta de {usuario.NombreUsuario}."));
        foreach (var rol in usuario.Roles)
            db.BitacoraUsuarios.Add(new RegistroBitacoraUsuario(usuario.Id, administradorId, AccionUsuario.RolAsignado, rol));

        await db.SaveChangesAsync(ct);
        return usuario;
    }

    public async Task<Usuario> ActualizarAsync(Guid id, ActualizarUsuario dto, Guid administradorId, CancellationToken ct)
    {
        var usuario = await ObtenerAsync(id, ct) ?? throw new KeyNotFoundException("Usuario no encontrado.");
        var roles = (dto.Roles ?? []).Distinct().ToList();

        var dejaDeSerAdmin = usuario.TieneRol(Rol.Administrador) && usuario.Activo && (!roles.Contains(Rol.Administrador) || !dto.Activo);
        if (dejaDeSerAdmin)
        {
            if (id == administradorId) throw new ExcepcionDominio("No puedes quitarte tu propio rol de Administrador ni desactivarte.");
            var otrosAdmins = await db.Usuarios.CountAsync(u => u.Id != id && u.Activo
                && u.Asignaciones.Any(a => a.Rol == Rol.Administrador), ct);
            if (otrosAdmins == 0) throw new ExcepcionDominio("Debe quedar al menos un Administrador activo.");
        }

        var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Id != id && u.Email == email, ct))
            throw new ExcepcionDominio("Ya existe otro usuario con ese email.");

        var cambiosDatos = new List<string>();
        if (usuario.NombreCompleto != (dto.NombreCompleto ?? string.Empty).Trim()) cambiosDatos.Add("nombre");
        if (usuario.Email != email) cambiosDatos.Add("email");
        usuario.ActualizarDatos(dto.NombreCompleto ?? string.Empty, email);

        var (agregados, quitados) = usuario.AsignarRoles(roles, administradorId);
        foreach (var rol in agregados)
            db.BitacoraUsuarios.Add(new RegistroBitacoraUsuario(id, administradorId, AccionUsuario.RolAsignado, rol));
        foreach (var rol in quitados)
            db.BitacoraUsuarios.Add(new RegistroBitacoraUsuario(id, administradorId, AccionUsuario.RolRevocado, rol));
        if (usuario.CambiarActivo(dto.Activo))
            db.BitacoraUsuarios.Add(new RegistroBitacoraUsuario(id, administradorId,
                dto.Activo ? AccionUsuario.Activado : AccionUsuario.Desactivado));
        if (cambiosDatos.Count > 0)
            db.BitacoraUsuarios.Add(new RegistroBitacoraUsuario(id, administradorId, AccionUsuario.DatosActualizados,
                detalle: "Cambió: " + string.Join(", ", cambiosDatos) + "."));

        await db.SaveChangesAsync(ct);
        return usuario;
    }

    /// <param name="administradorId">Si lo cambia un administrador por otro usuario, queda en la bitácora.</param>
    public async Task CambiarContrasenaAsync(Guid id, string nueva, Guid? administradorId, CancellationToken ct)
    {
        ValidarContrasena(nueva);
        var usuario = await db.Usuarios.FindAsync([id], ct) ?? throw new KeyNotFoundException("Usuario no encontrado.");
        usuario.EstablecerHashContrasena(hasher.Hashear(usuario, nueva));
        if (administradorId is { } admin && admin != id)
            db.BitacoraUsuarios.Add(new RegistroBitacoraUsuario(id, admin, AccionUsuario.ContrasenaRestablecida));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RegistroBitacoraDto>> BitacoraAsync(Guid usuarioId, CancellationToken ct)
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == usuarioId, ct)) throw new KeyNotFoundException("Usuario no encontrado.");
        var registros = await db.BitacoraUsuarios.AsNoTracking().Where(b => b.UsuarioId == usuarioId)
            .OrderByDescending(b => b.CreadoEn).ToListAsync(ct);
        var ids = registros.Where(r => r.AdministradorId != null).Select(r => r.AdministradorId!.Value).Distinct().ToList();
        var nombres = await db.Usuarios.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.NombreUsuario, ct);
        return registros.Select(r => new RegistroBitacoraDto(r.Id, r.CreadoEn, r.Accion, r.Rol, r.Detalle, r.AdministradorId,
            r.AdministradorId is { } a ? nombres.GetValueOrDefault(a) : "sistema")).ToList();
    }

    /// <summary>Crea el primer Administrador si la base no tiene usuarios.</summary>
    public async Task SembrarAdministradorAsync(string contrasena, CancellationToken ct)
    {
        if (await db.Usuarios.AnyAsync(ct)) return;
        await CrearAsync(new CrearUsuario("admin", "Administrador del sistema", "admin@sat-pirai.local", [Rol.Administrador], contrasena), null, ct);
    }

    private static void ValidarContrasena(string? contrasena)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LongitudMinimaContrasena
            || !contrasena.Any(char.IsLetter) || !contrasena.Any(char.IsDigit))
            throw new ExcepcionDominio($"La contraseña debe tener al menos {LongitudMinimaContrasena} caracteres, con letras y números.");
    }
}
