using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Usuarios;

namespace SistemaAlertas.Api.Controllers;

public sealed record SolicitudLogin(string NombreUsuario, string Contrasena);
public sealed record SolicitudRegistro(string NombreUsuario, string NombreCompleto, string Email, string Contrasena);
public sealed record SolicitudCambioContrasena(string ContrasenaActual, string ContrasenaNueva);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ServicioUsuarios servicio, IAppDbContext db, IHasherContrasena hasher) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitesPeticiones.Acceso)]
    public async Task<ActionResult<ResultadoLogin>> Login(SolicitudLogin solicitud, CancellationToken ct)
    {
        var resultado = await servicio.LoginAsync(solicitud.NombreUsuario, solicitud.Contrasena, ct);
        return resultado is null
            ? Problem(statusCode: 401, title: "Credenciales incorrectas", detail: "Usuario o contraseña incorrectos, o el usuario está desactivado.")
            : resultado;
    }

    /// <summary>Registro propio: crea una cuenta de solo lectura (rol Consulta) y devuelve la sesión iniciada.</summary>
    [HttpPost("registro")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitesPeticiones.Acceso)]
    public async Task<ActionResult<ResultadoLogin>> Registrar(SolicitudRegistro solicitud, CancellationToken ct)
    {
        await servicio.RegistrarConsultaAsync(solicitud.NombreUsuario, solicitud.NombreCompleto, solicitud.Email, solicitud.Contrasena, ct);
        var sesion = await servicio.LoginAsync(solicitud.NombreUsuario, solicitud.Contrasena, ct);
        return StatusCode(201, sesion);
    }

    [HttpGet("yo")]
    [Authorize(Politicas.Lectura)]
    public async Task<ActionResult<UsuarioDto>> Yo(CancellationToken ct)
    {
        var usuario = await servicio.ObtenerAsync(User.Id(), ct);
        return usuario is null ? NotFound() : UsuarioDto.De(usuario);
    }

    [HttpPut("contrasena")]
    [Authorize(Politicas.Lectura)]
    public async Task<IActionResult> CambiarContrasena(SolicitudCambioContrasena solicitud, CancellationToken ct)
    {
        var usuario = await db.Usuarios.FindAsync([User.Id()], ct) ?? throw new KeyNotFoundException("Usuario no encontrado.");
        if (!hasher.Verificar(usuario, solicitud.ContrasenaActual))
            return Problem(statusCode: 400, title: "Datos no válidos", detail: "La contraseña actual no es correcta.");
        await servicio.CambiarContrasenaAsync(usuario.Id, solicitud.ContrasenaNueva, null, ct);
        return NoContent();
    }
}
