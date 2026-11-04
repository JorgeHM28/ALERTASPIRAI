using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Usuarios;
using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Api.Controllers;

public sealed record SolicitudRestablecerContrasena(string ContrasenaNueva);
public sealed record RolDto(Rol Rol, string[] Permisos);

/// <summary>RF-13: gestión de usuarios y roles (solo Administrador).</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Politicas.Administracion)]
public sealed class UsuariosController(ServicioUsuarios servicio, IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<UsuarioDto>> Listar(CancellationToken ct) =>
        (await db.Usuarios.AsNoTracking().OrderBy(u => u.NombreUsuario).ToListAsync(ct)).Select(UsuarioDto.De);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UsuarioDto>> Obtener(Guid id, CancellationToken ct)
    {
        var usuario = await db.Usuarios.FindAsync([id], ct);
        return usuario is null ? NotFound() : UsuarioDto.De(usuario);
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Crear(CrearUsuario dto, CancellationToken ct)
    {
        var usuario = await servicio.CrearAsync(dto, ct);
        return CreatedAtAction(nameof(Obtener), new { id = usuario.Id }, UsuarioDto.De(usuario));
    }

    [HttpPut("{id:guid}")]
    public async Task<UsuarioDto> Actualizar(Guid id, ActualizarUsuario dto, CancellationToken ct) =>
        UsuarioDto.De(await servicio.ActualizarAsync(id, dto, User.Id(), ct));

    [HttpPut("{id:guid}/contrasena")]
    public async Task<IActionResult> RestablecerContrasena(Guid id, SolicitudRestablecerContrasena dto, CancellationToken ct)
    {
        await servicio.CambiarContrasenaAsync(id, dto.ContrasenaNueva, ct);
        return NoContent();
    }

    /// <summary>Roles disponibles y qué permisos da cada uno.</summary>
    [HttpGet("roles")]
    [Authorize(Politicas.Lectura)]
    public IEnumerable<RolDto> Roles() => Enum.GetValues<Rol>().Select(r =>
        new RolDto(r, Politicas.Matriz.Where(p => p.Value.Contains(r)).Select(p => p.Key).ToArray()));
}
