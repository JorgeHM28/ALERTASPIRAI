using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaAlertas.Application.Usuarios;
using SistemaAlertas.Infrastructure.Seguridad;
namespace SistemaAlertas.Api.Controllers.Web;
public sealed record LoginWeb([Required] string NombreUsuario, [Required] string Contrasena);
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class CuentaController(ServicioUsuarios usuarios) : Controller
{
    [HttpGet("/cuenta/login"), AllowAnonymous] public IActionResult Login() => View();
    [HttpPost("/cuenta/login"), AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginWeb dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(dto);
        var sesion = await usuarios.LoginAsync(dto.NombreUsuario, dto.Contrasena, ct);
        if (sesion is null) { ModelState.AddModelError("", "Usuario o contraseña incorrectos, o cuenta desactivada."); return View(dto); }
        var claims = new List<Claim> { new(NombresClaim.Id, sesion.Usuario.Id.ToString()), new(NombresClaim.Nombre, sesion.Usuario.NombreCompleto) };
        claims.AddRange(sesion.Usuario.Roles.Select(r => new Claim(NombresClaim.Rol, r.ToString())));
        await HttpContext.SignInAsync("Web", new ClaimsPrincipal(new ClaimsIdentity(claims, "Web", NombresClaim.Nombre, NombresClaim.Rol)),
            new AuthenticationProperties { ExpiresUtc = sesion.Expira, IsPersistent = false });
        return Redirect("/gestion/avisos");
    }
    [HttpPost("/cuenta/salir"), ValidateAntiForgeryToken] public async Task<IActionResult> Salir()
    { await HttpContext.SignOutAsync("Web"); return Redirect("/"); }
    [HttpGet("/cuenta/denegado")] public IActionResult Denegado() { Response.StatusCode = 403; return View("Error", "Tu cuenta no tiene permiso para esta operación."); }
}
