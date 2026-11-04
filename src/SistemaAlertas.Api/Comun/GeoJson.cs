using System.Text.Json;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Api.Comun;

/// <summary>Lectura y escritura de GeoJSON (RFC 7946) con NetTopologySuite.</summary>
public static class GeoJson
{
    private static readonly JsonSerializerOptions Opciones = new() { Converters = { new GeoJsonConverterFactory() } };

    /// <summary>Acepta una geometría GeoJSON o un Feature (se usa su geometría).</summary>
    public static Geometry LeerGeometria(JsonElement json)
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

    public static FeatureCollection LeerColeccion(JsonElement json)
    {
        try
        {
            return json.Deserialize<FeatureCollection>(Opciones) ?? throw new ExcepcionDominio("La colección está vacía.");
        }
        catch (JsonException ex)
        {
            throw new ExcepcionDominio($"GeoJSON no válido: {ex.Message}");
        }
    }

    public static JsonElement Escribir(Geometry geometria) => JsonSerializer.SerializeToElement(geometria, Opciones);

    public static JsonElement Escribir(FeatureCollection coleccion) => JsonSerializer.SerializeToElement(coleccion, Opciones);
}
