using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Usuarios;
using SistemaAlertas.Infrastructure.Seguridad;

namespace SistemaAlertas.Api.Comun;

/// <summary>Usuario de la petición HTTP: sus permisos salen de los roles del token (RF-13).</summary>
public sealed class UsuarioActualHttp(IHttpContextAccessor accesor) : IUsuarioActual
{
    public bool Autenticado => accesor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool Tiene(Permiso permiso)
    {
        if (!Autenticado) return false;
        var roles = accesor.HttpContext!.User.FindAll(NombresClaim.Rol)
            .Select(c => Enum.TryParse<Rol>(c.Value, out var r) ? r : (Rol?)null)
            .OfType<Rol>();
        return MatrizPermisos.De(roles).Contains(permiso);
    }
}
