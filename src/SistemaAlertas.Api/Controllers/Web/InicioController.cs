using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaAlertas.Application.Publico;
namespace SistemaAlertas.Api.Controllers.Web;
[AllowAnonymous, ApiExplorerSettings(IgnoreApi = true)]
public sealed class InicioController(ServicioPublico publico) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(Guid? aviso, CancellationToken ct)
    {
        if (aviso is { } id) return View(new[] { await publico.Obtener(id, ct) }.AsEnumerable());
        return View(await publico.Vigentes(true, ct));
    }
}
