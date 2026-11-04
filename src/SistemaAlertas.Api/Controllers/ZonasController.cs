using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Features;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Api.Controllers;

public sealed record GuardarZona(string Nombre, string? Descripcion, NivelRiesgo NivelRiesgo, JsonElement Geometria);
public sealed record ZonaDto(Guid Id, string Nombre, string? Descripcion, NivelRiesgo NivelRiesgo, double AreaKm2,
    int Comunidades, JsonElement Geometria);
public sealed record ResultadoImportacionZonas(int Creadas, int Actualizadas, IReadOnlyList<string> Errores);

/// <summary>RF-09: zonas de riesgo en GeoJSON.</summary>
[ApiController]
[Route("api/zonas")]
[Authorize(Politicas.Lectura)]
public sealed class ZonasController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
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
        var coleccion = new FeatureCollection();
        foreach (var z in await db.Zonas.AsNoTracking().OrderBy(z => z.Nombre).ToListAsync(ct))
        {
            coleccion.Add(new Feature(z.Area, new AttributesTable
            {
                { "id", z.Id.ToString() }, { "nombre", z.Nombre },
                { "descripcion", z.Descripcion ?? string.Empty }, { "nivelRiesgo", z.NivelRiesgo.ToString() }
            }));
        }
        return GeoJson.Escribir(coleccion);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ZonaDto>> Obtener(Guid id, CancellationToken ct)
    {
        var zona = await db.Zonas.AsNoTracking().FirstOrDefaultAsync(z => z.Id == id, ct);
        if (zona is null) return NotFound();
        return ADto(zona, await db.Comunidades.CountAsync(c => c.ZonaId == id, ct));
    }

    [HttpPost]
    [Authorize(Politicas.Edicion)]
    public async Task<ActionResult<ZonaDto>> Crear(GuardarZona dto, CancellationToken ct)
    {
        await ValidarNombreUnico(dto.Nombre, null, ct);
        var zona = new Zona(dto.Nombre, dto.Descripcion, dto.NivelRiesgo, GeoJson.LeerGeometria(dto.Geometria));
        db.Zonas.Add(zona);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Obtener), new { id = zona.Id }, ADto(zona, 0));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Politicas.Edicion)]
    public async Task<ZonaDto> Actualizar(Guid id, GuardarZona dto, CancellationToken ct)
    {
        var zona = await db.Zonas.FindAsync([id], ct) ?? throw new KeyNotFoundException("Zona no encontrada.");
        await ValidarNombreUnico(dto.Nombre, id, ct);
        zona.Actualizar(dto.Nombre, dto.Descripcion, dto.NivelRiesgo, GeoJson.LeerGeometria(dto.Geometria));
        await db.SaveChangesAsync(ct);
        return ADto(zona, await db.Comunidades.CountAsync(c => c.ZonaId == id, ct));
    }

    [HttpDelete("{id:guid}")]
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
    /// </summary>
    [HttpPost("importar")]
    [Authorize(Politicas.Edicion)]
    public async Task<ResultadoImportacionZonas> Importar(JsonElement featureCollection, CancellationToken ct)
    {
        var coleccion = GeoJson.LeerColeccion(featureCollection);
        var existentes = await db.Zonas.ToDictionaryAsync(z => z.Nombre.ToLower(), ct);
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

                if (existentes.TryGetValue(nombre.Trim().ToLower(), out var zona))
                {
                    zona.Actualizar(nombre, descripcion ?? zona.Descripcion, nivel, f.Geometry);
                    actualizadas++;
                }
                else
                {
                    zona = new Zona(nombre, descripcion, nivel, f.Geometry);
                    db.Zonas.Add(zona);
                    existentes[zona.Nombre.ToLower()] = zona;
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
        var n = (nombre ?? string.Empty).Trim().ToLower();
        if (await db.Zonas.AnyAsync(z => z.Nombre.ToLower() == n && z.Id != excepto, ct))
            throw new ExcepcionDominio($"Ya existe una zona llamada '{nombre}'.");
    }

    private static string? Atributo(IFeature f, string nombre) =>
        f.Attributes is not null && f.Attributes.Exists(nombre) ? f.Attributes[nombre]?.ToString() : null;

    private static ZonaDto ADto(Zona z, int comunidades) =>
        new(z.Id, z.Nombre, z.Descripcion, z.NivelRiesgo, Math.Round(AreaKm2(z), 2), comunidades, GeoJson.Escribir(z.Area));

    /// <summary>Área aproximada en km² (proyección equirectangular local; suficiente para mostrar).</summary>
    private static double AreaKm2(Zona z)
    {
        var latMedia = z.Area.Centroid.Y * Math.PI / 180;
        const double kmPorGrado = 111.32;
        return z.Area.Area * kmPorGrado * kmPorGrado * Math.Cos(latMedia);
    }
}
