using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Zonas;
namespace SistemaAlertas.Api.Controllers;

[ApiController, Route("api/zonas"), Authorize(Politicas.Lectura)]
public sealed class ZonasController(ServicioZonas servicio) : ControllerBase
{
    [HttpGet] public Task<IEnumerable<ZonaDto>> Listar(CancellationToken ct) => servicio.Listar(ct);
    [HttpGet("geojson")] public Task<JsonElement> GeoJsonColeccion(CancellationToken ct) => servicio.GeoJsonColeccion(ct);
    [HttpGet("{id:guid}")] public Task<ZonaDto> Obtener(Guid id, CancellationToken ct) => servicio.Obtener(id, ct);
    [HttpPost, Authorize(Politicas.Edicion), ProducesResponseType<ZonaDto>(201)]
    public async Task<ActionResult<ZonaDto>> Crear(GuardarZona dto, CancellationToken ct)
    { var zona = await servicio.Crear(dto, ct); return CreatedAtAction(nameof(Obtener), new { id = zona.Id }, zona); }
    [HttpPut("{id:guid}"), Authorize(Politicas.Edicion)]
    public Task<ZonaDto> Actualizar(Guid id, GuardarZona dto, CancellationToken ct) => servicio.Actualizar(id, dto, ct);
    [HttpDelete("{id:guid}"), Authorize(Politicas.Edicion), ProducesResponseType(204)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct) { await servicio.Eliminar(id, ct); return NoContent(); }
    [HttpPost("importar"), Authorize(Politicas.Edicion)]
    public Task<ResultadoImportacionZonas> Importar(JsonElement featureCollection, [FromQuery] string? fuenteInstitucional,
        [FromQuery] string? referenciaDocumento, [FromQuery] DateOnly? fechaDocumento, CancellationToken ct) =>
        servicio.Importar(featureCollection, fuenteInstitucional, referenciaDocumento, fechaDocumento, ct);
}
