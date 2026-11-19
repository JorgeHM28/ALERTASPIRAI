using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Infrastructure.Persistence;
using SistemaAlertas.Infrastructure.Seguridad;
namespace SistemaAlertas.Api.Comun;

public static class SesionWeb
{
    public static async Task Validar(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.FindFirstValue(NombresClaim.Id);
        var db = context.HttpContext.RequestServices.GetRequiredService<AlertasDbContext>();
        var usuario = Guid.TryParse(id, out var guid) ? await db.Usuarios.AsNoTracking().Include(u => u.Asignaciones)
            .FirstOrDefaultAsync(u => u.Id == guid, context.HttpContext.RequestAborted) : null;
        if (usuario is null || !usuario.Activo)
        { context.RejectPrincipal(); await context.HttpContext.SignOutAsync("Web"); return; }
        var identidad = (ClaimsIdentity)context.Principal!.Identity!;
        foreach (var claim in identidad.FindAll(NombresClaim.Rol).ToList()) identidad.RemoveClaim(claim);
        identidad.AddClaims(usuario.Roles.Select(r => new Claim(NombresClaim.Rol, r.ToString())));
    }
}
