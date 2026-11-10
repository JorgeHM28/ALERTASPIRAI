using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Infrastructure.Persistence;
using SistemaAlertas.Infrastructure.Seguridad;

namespace SistemaAlertas.Api.Comun;

/// <summary>
/// En cada petición comprueba que el usuario del token siga activo y toma sus roles de la base:
/// desactivar a alguien o quitarle un rol corta su acceso al momento, sin esperar a que venza el token.
/// </summary>
public static class ValidacionSesion
{
    public static async Task AlValidarToken(TokenValidatedContext contexto)
    {
        var id = contexto.Principal?.FindFirstValue(NombresClaim.Id);
        if (!Guid.TryParse(id, out var usuarioId)) { contexto.Fail("Token sin usuario."); return; }

        var db = contexto.HttpContext.RequestServices.GetRequiredService<AlertasDbContext>();
        var usuario = await db.Usuarios.AsNoTracking().Include(u => u.Asignaciones)
            .FirstOrDefaultAsync(u => u.Id == usuarioId, contexto.HttpContext.RequestAborted);
        if (usuario is null || !usuario.Activo) { contexto.Fail("El usuario no existe o está desactivado."); return; }

        var identidad = (ClaimsIdentity)contexto.Principal!.Identity!;
        foreach (var claim in identidad.FindAll(NombresClaim.Rol).ToList()) identidad.RemoveClaim(claim);
        identidad.AddClaims(usuario.Roles.Select(r => new Claim(NombresClaim.Rol, r.ToString())));
    }
}
