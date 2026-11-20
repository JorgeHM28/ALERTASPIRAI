using System.Security.Claims;
using SistemaAlertas.Infrastructure.Seguridad;

namespace SistemaAlertas.Api.Comun;

public static class UsuarioActual
{
    public static Guid Id(this ClaimsPrincipal usuario) =>
        Guid.TryParse(usuario.FindFirstValue(NombresClaim.Id), out var id) ? id : throw new UnauthorizedAccessException();
}
