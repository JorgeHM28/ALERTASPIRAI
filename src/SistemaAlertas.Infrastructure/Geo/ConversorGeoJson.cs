using System.Text.Json;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Infrastructure.Geo;

/// <summary>
/// Patrón Adapter (adaptador). Adapta NetTopologySuite y su serializador GeoJSON (NetTopologySuite.IO.GeoJSON4STJ,
/// el adaptado) a <see cref="IConversorGeoJson"/>. Traduce las geometrías PostGIS a GeoJSON para el mapa (RF-09)
/// y convierte los errores del serializador en <see cref="ExcepcionDominio"/>.
/// </summary>
public sealed class ConversorGeoJson : IConversorGeoJson
{
    private static readonly JsonSerializerOptions Opciones = new() { Converters = { new GeoJsonConverterFactory() } };

    public Geometry LeerGeometria(JsonElement json)
    {
        try
        {
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("type", out var tipo) && tipo.GetString() == "Feature")
                return json.Deserialize<IFeature>(Opciones)?.Geometry ?? throw new ExcepcionDominio("El Feature no tiene geometría.");
            return json.Deserialize<Geometry>(Opciones) ?? throw new ExcepcionDominio("La geometría está vacía.");
        }
        catch (JsonException ex)
        {
            throw new ExcepcionDominio($"GeoJSON no válido: {ex.Message}");
        }
    }

    public IReadOnlyList<FeatureGeo> LeerColeccion(JsonElement json)
    {
        try
        {
            var coleccion = json.Deserialize<FeatureCollection>(Opciones) ?? throw new ExcepcionDominio("La colección está vacía.");
            return coleccion.Select(f => new FeatureGeo(f.Geometry,
                    (f.Attributes?.GetNames() ?? []).ToDictionary(n => n, n => f.Attributes![n]?.ToString())))
                .ToList();
        }
        catch (JsonException ex)
        {
            throw new ExcepcionDominio($"GeoJSON no válido: {ex.Message}");
        }
    }

    public JsonElement AGeoJson(Geometry geometria) => JsonSerializer.SerializeToElement(geometria, Opciones);

    public JsonElement ColeccionDeZonas(IEnumerable<Zona> zonas)
    {
        var coleccion = new FeatureCollection();
        foreach (var z in zonas)
        {
            coleccion.Add(new Feature(z.Area, new AttributesTable
            {
                { "id", z.Id.ToString() }, { "nombre", z.Nombre },
                { "descripcion", z.Descripcion ?? string.Empty }, { "nivelRiesgo", z.NivelRiesgo.ToString() },
                { "fuenteInstitucional", z.FuenteInstitucional ?? string.Empty },
                { "referenciaDocumento", z.ReferenciaDocumento ?? string.Empty },
                { "fechaDocumento", z.FechaDocumento?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty }
            }));
        }
        return JsonSerializer.SerializeToElement(coleccion, Opciones);
    }
}
