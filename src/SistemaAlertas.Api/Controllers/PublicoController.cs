using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaAlertas.Application.Publico;
namespace SistemaAlertas.Api.Controllers;

[ApiController, Route("api/publico"), AllowAnonymous]
public sealed class PublicoController(ServicioPublico servicio) : ControllerBase
{
    [HttpGet("avisos")] public Task<IEnumerable<AvisoPublico>> Vigentes([FromQuery] bool cancelados = false, CancellationToken ct = default) => servicio.Vigentes(cancelados, ct);
    [HttpGet("avisos/{id:guid}")] public Task<AvisoPublico> Obtener(Guid id, CancellationToken ct) => servicio.Obtener(id, ct);
}
