using SistemaAlertas.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Usuarios;
using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Api.Controllers;


/// <summary>RF-13: gestión de usuarios, sus roles (uno o varios) y la bitácora de cambios. Solo Administrador.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Politicas.Administracion)]
public sealed class UsuariosController(ServicioUsuarios servicio) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<UsuarioDto>> Listar(CancellationToken ct) =>
        (await servicio.ListarAsync(ct)).Select(UsuarioDto.De);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UsuarioDto>> Obtener(Guid id, CancellationToken ct)
    {
        var usuario = await servicio.ObtenerAsync(id, ct);
        return usuario is null ? NotFound() : UsuarioDto.De(usuario);
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Crear(CrearUsuario dto, CancellationToken ct)
    {
        var usuario = await servicio.CrearAsync(dto, User.Id(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = usuario.Id }, UsuarioDto.De(usuario));
    }

    /// <summary>Actualiza datos, roles y estado. Cada rol asignado o revocado y cada activación queda en la bitácora.</summary>
    [HttpPut("{id:guid}")]
    public async Task<UsuarioDto> Actualizar(Guid id, ActualizarUsuario dto, CancellationToken ct) =>
        UsuarioDto.De(await servicio.ActualizarAsync(id, dto, User.Id(), ct));

    [HttpPut("{id:guid}/contrasena")]
    public async Task<IActionResult> RestablecerContrasena(Guid id, SolicitudRestablecerContrasena dto, CancellationToken ct)
    {
        await servicio.CambiarContrasenaAsync(id, dto.ContrasenaNueva, User.Id(), ct);
        return NoContent();
    }

    /// <summary>Historial de asignaciones y cambios del usuario: qué se hizo, qué administrador y cuándo.</summary>
    [HttpGet("{id:guid}/bitacora")]
    public Task<IReadOnlyList<RegistroBitacoraDto>> Bitacora(Guid id, CancellationToken ct) => servicio.BitacoraAsync(id, ct);

    /// <summary>Roles disponibles y qué permisos da cada uno.</summary>
    [HttpGet("roles")]
    [Authorize(Politicas.Lectura)]
    public IEnumerable<RolDto> Roles() => MatrizPermisos.PorRol.Select(p => new RolDto(p.Key, p.Value));
}
