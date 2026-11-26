using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Api.Controllers;

/// <param name="FuenteInstitucional">Obligatoria al crear. Al actualizar, si no se envían los tres datos institucionales, se conservan los anteriores.</param>
public sealed record GuardarZona(string Nombre, string? Descripcion, NivelRiesgo NivelRiesgo, JsonElement Geometria,
    string? FuenteInstitucional, string? ReferenciaDocumento, DateOnly? FechaDocumento);
public sealed record ZonaDto(Guid Id, string Nombre, string? Descripcion, NivelRiesgo NivelRiesgo, double AreaKm2,
    int Comunidades, JsonElement Geometria, string? FuenteInstitucional, string? ReferenciaDocumento, DateOnly? FechaDocumento);
public sealed record ResultadoImportacionZonas(int Creadas, int Actualizadas, IReadOnlyList<string> Errores);

/// <summary>RF-09: zonas de riesgo en GeoJSON.</summary>
[ApiController]
[ProducesResponseType(401)]
[ProducesResponseType(403)]
[Route("api/zonas")]
[Authorize(Politicas.Lectura)]
public sealed class ZonasController(IAppDbContext db, IConversorGeoJson geoJson) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ZonaDto>), 200)]
    public async Task<IEnumerable<ZonaDto>> Listar(CancellationToken ct)
    {
        var zonas = await db.Zonas.AsNoTracking().OrderBy(z => z.Nombre).ToListAsync(ct);
        var conteo = await db.Comunidades.GroupBy(c => c.ZonaId).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        return zonas.Select(z => ADto(z, conteo.GetValueOrDefault(z.Id)));
    }

    /// <summary>Todas las zonas como FeatureCollection GeoJSON, lista para Leaflet.</summary>
    [HttpGet("geojson")]
    public async Task<JsonElement> GeoJsonColeccion(CancellationToken ct)
    {
        // Adapter: las geometrías de PostGIS (NetTopologySuite) salen como GeoJSON para Leaflet.
        return geoJson.ColeccionDeZonas(await db.Zonas.AsNoTracking().OrderBy(z => z.Nombre).ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ZonaDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ZonaDto>> Obtener(Guid id, CancellationToken ct)
    {
        var zona = await db.Zonas.AsNoTracking().FirstOrDefaultAsync(z => z.Id == id, ct);
        if (zona is null) return NotFound();
        return ADto(zona, await db.Comunidades.CountAsync(c => c.ZonaId == id, ct));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ZonaDto), 201)]
    [ProducesResponseType(400)]
    [Authorize(Politicas.Edicion)]
    public async Task<ActionResult<ZonaDto>> Crear(GuardarZona dto, CancellationToken ct)
    {
        await ValidarNombreUnico(dto.Nombre, null, ct);
        var geometria = geoJson.LeerGeometria(dto.Geometria);
        Zona.ValidarArea(geometria);
        var origen = OrigenInstitucional.Desde(dto.FuenteInstitucional, dto.ReferenciaDocumento, dto.FechaDocumento)
            ?? throw new ExcepcionDominio("Indica la fuente institucional, la referencia y la fecha del documento de origen de la zona.");
        var zona = new Zona(dto.Nombre, dto.Descripcion, dto.NivelRiesgo, geometria, origen);
        db.Zonas.Add(zona);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Obtener), new { id = zona.Id }, ADto(zona, 0));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ZonaDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [Authorize(Politicas.Edicion)]
    public async Task<ZonaDto> Actualizar(Guid id, GuardarZona dto, CancellationToken ct)
    {
        var zona = await db.Zonas.FindAsync([id], ct) ?? throw new KeyNotFoundException("Zona no encontrada.");
        await ValidarNombreUnico(dto.Nombre, id, ct);
        zona.Actualizar(dto.Nombre, dto.Descripcion, dto.NivelRiesgo, geoJson.LeerGeometria(dto.Geometria),
            OrigenInstitucional.Desde(dto.FuenteInstitucional, dto.ReferenciaDocumento, dto.FechaDocumento));
        await db.SaveChangesAsync(ct);
        return ADto(zona, await db.Comunidades.CountAsync(c => c.ZonaId == id, ct));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [Authorize(Politicas.Edicion)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        var zona = await db.Zonas.FindAsync([id], ct) ?? throw new KeyNotFoundException("Zona no encontrada.");
        var comunidades = await db.Comunidades.CountAsync(c => c.ZonaId == id, ct);
        if (comunidades > 0)
            throw new ExcepcionDominio($"La zona tiene {comunidades} comunidad(es). Muévelas o elimínalas antes de borrar la zona.");
        db.Zonas.Remove(zona);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Importa una FeatureCollection GeoJSON. Cada Feature necesita la propiedad "nombre" (o "name");
    /// "nivelRiesgo" y "descripcion" son opcionales. Si ya existe una zona con ese nombre, se actualiza su geometría.
    /// Los datos institucionales (fuente, referencia y fecha del documento) llegan en la query y valen para todo el archivo;
    /// un Feature puede traer los suyos en las propiedades "fuenteInstitucional", "referenciaDocumento" y "fechaDocumento".
    /// Una zona que ya existía y no recibe datos nuevos conserva los que tenía.
    /// </summary>
    [HttpPost("importar")]
    [Authorize(Politicas.Edicion)]
    public async Task<ResultadoImportacionZonas> Importar(JsonElement featureCollection,
        [FromQuery] string? fuenteInstitucional, [FromQuery] string? referenciaDocumento, [FromQuery] DateOnly? fechaDocumento,
        CancellationToken ct)
    {
        var origenArchivo = OrigenInstitucional.Desde(fuenteInstitucional, referenciaDocumento, fechaDocumento);
        var coleccion = geoJson.LeerColeccion(featureCollection);
        var existentes = await db.Zonas.ToDictionaryAsync(z => z.Nombre, StringComparer.OrdinalIgnoreCase, ct);
        int creadas = 0, actualizadas = 0;
        var errores = new List<string>();

        for (var i = 0; i < coleccion.Count; i++)
        {
            var f = coleccion[i];
            var nombre = Atributo(f, "nombre") ?? Atributo(f, "name");
            try
            {
                if (string.IsNullOrWhiteSpace(nombre)) throw new ExcepcionDominio("falta la propiedad 'nombre'.");
                var nivel = Enum.TryParse<NivelRiesgo>(Atributo(f, "nivelRiesgo"), true, out var n) ? n : NivelRiesgo.Medio;
                var descripcion = Atributo(f, "descripcion");
                var fechaTexto = Atributo(f, "fechaDocumento");
                DateOnly? fecha = string.IsNullOrWhiteSpace(fechaTexto) ? null
                    : DateOnly.TryParse(fechaTexto, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d
                    : throw new ExcepcionDominio($"fechaDocumento '{fechaTexto}' no es una fecha (usa AAAA-MM-DD).");
                var origen = OrigenInstitucional.Desde(Atributo(f, "fuenteInstitucional"), Atributo(f, "referenciaDocumento"), fecha)
                    ?? origenArchivo;

                if (existentes.TryGetValue(nombre.Trim(), out var zona))
                {
                    zona.Actualizar(nombre, descripcion ?? zona.Descripcion, nivel, f.Geometria!, origen);
                    actualizadas++;
                }
                else
                {
                    zona = new Zona(nombre, descripcion, nivel, f.Geometria!,
                        origen ?? throw new ExcepcionDominio("faltan los datos institucionales (fuente, referencia y fecha del documento)."));
                    db.Zonas.Add(zona);
                    existentes[zona.Nombre] = zona;
                    creadas++;
                }
            }
            catch (ExcepcionDominio ex)
            {
                errores.Add($"Feature {i + 1} ({nombre ?? "sin nombre"}): {ex.Message}");
            }
        }

        if (errores.Count > 0)
            throw new ExcepcionDominio("No se importó nada porque hay errores. " + string.Join(" | ", errores));

        await db.SaveChangesAsync(ct);
        return new ResultadoImportacionZonas(creadas, actualizadas, errores);
    }

    private async Task ValidarNombreUnico(string nombre, Guid? excepto, CancellationToken ct)
    {
        var n = (nombre ?? string.Empty).Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // dentro de la consulta: EF lo traduce a lower() de PostgreSQL
        if (await db.Zonas.AnyAsync(z => z.Nombre.ToLower() == n && z.Id != excepto, ct))
#pragma warning restore CA1304, CA1311, CA1862
            throw new ExcepcionDominio($"Ya existe una zona llamada '{nombre}'.");
    }

    private static string? Atributo(FeatureGeo f, string nombre) => f.Propiedad(nombre);

    private ZonaDto ADto(Zona z, int comunidades) =>
        new(z.Id, z.Nombre, z.Descripcion, z.NivelRiesgo, Math.Round(AreaKm2(z), 2), comunidades, geoJson.AGeoJson(z.Area),
            z.FuenteInstitucional, z.ReferenciaDocumento, z.FechaDocumento);

    /// <summary>Área aproximada en km² (proyección equirectangular local; suficiente para mostrar).</summary>
    private static double AreaKm2(Zona z)
    {
        var latMedia = z.Area.Centroid.Y * Math.PI / 180;
        const double kmPorGrado = 111.32;
        return z.Area.Area * kmPorGrado * kmPorGrado * Math.Cos(latMedia);
    }
}
