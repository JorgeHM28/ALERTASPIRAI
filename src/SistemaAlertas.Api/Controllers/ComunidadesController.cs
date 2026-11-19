using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Comunidades;
namespace SistemaAlertas.Api.Controllers;

[ApiController, Route("api/comunidades"), Authorize(Politicas.Lectura)]
public sealed class ComunidadesController(ServicioComunidades servicio) : ControllerBase
{
    [HttpGet] public Task<IEnumerable<ComunidadDto>> Listar([FromQuery] Guid? zonaId, [FromQuery] string? buscar, CancellationToken ct) => servicio.Listar(zonaId, buscar, ct);
    [HttpGet("{id:guid}")] public Task<ComunidadDto> Obtener(Guid id, CancellationToken ct) => servicio.Obtener(id, ct);
    [HttpPost, Authorize(Politicas.Edicion), ProducesResponseType<ComunidadDto>(201)]
    public async Task<ActionResult<ComunidadDto>> Crear(GuardarComunidad dto, CancellationToken ct)
    { var c = await servicio.Crear(dto, ct); return CreatedAtAction(nameof(Obtener), new { id = c.Id }, c); }
    [HttpPut("{id:guid}"), Authorize(Politicas.Edicion)]
    public Task<ComunidadDto> Actualizar(Guid id, GuardarComunidad dto, CancellationToken ct) => servicio.Actualizar(id, dto, ct);
    [HttpDelete("{id:guid}"), Authorize(Politicas.Edicion), ProducesResponseType(204)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct) { await servicio.Eliminar(id, ct); return NoContent(); }
    [HttpGet("/api/contactos/{contactoId:guid}")]
    public Task<ContactoDto> ObtenerContacto(Guid contactoId, CancellationToken ct) => servicio.ObtenerContacto(contactoId, ct);
    [HttpPost("{id:guid}/contactos"), Authorize(Politicas.Edicion), ProducesResponseType<ContactoDto>(201)]
    public async Task<ActionResult<ContactoDto>> AgregarContacto(Guid id, GuardarContacto dto, CancellationToken ct)
    { var c = await servicio.AgregarContacto(id, dto, ct); return CreatedAtAction(nameof(ObtenerContacto), new { contactoId = c.Id }, c); }
    [HttpPut("/api/contactos/{contactoId:guid}"), Authorize(Politicas.Edicion)]
    public Task<ContactoDto> ActualizarContacto(Guid contactoId, GuardarContacto dto, CancellationToken ct) => servicio.ActualizarContacto(contactoId, dto, ct);
    [HttpDelete("/api/contactos/{contactoId:guid}"), Authorize(Politicas.Edicion), ProducesResponseType(204)]
    public async Task<IActionResult> EliminarContacto(Guid contactoId, CancellationToken ct) { await servicio.EliminarContacto(contactoId, ct); return NoContent(); }
    [HttpGet("/api/contactos/{contactoId:guid}/historial")]
    public Task<IEnumerable<HistorialContactoDto>> Historial(Guid contactoId, CancellationToken ct) => servicio.Historial(contactoId, ct);
    [HttpGet("/api/destinatarios")]
    public Task<DestinatariosDto> Destinatarios([FromQuery] Guid[]? zonaIds, CancellationToken ct) => servicio.Destinatarios(zonaIds, ct);
}
