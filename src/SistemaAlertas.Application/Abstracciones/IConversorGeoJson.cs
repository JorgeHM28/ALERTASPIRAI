using System.Text.Json;
using NetTopologySuite.Geometries;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Application.Abstracciones;

/// <summary>
/// Patrón Adapter (objetivo). La API y el mapa Leaflet hablan GeoJSON (RFC 7946); la base de datos guarda
/// geometrías SQL Server que EF Core entrega como objetos de NetTopologySuite. Esta interfaz es lo único que conocen
/// los controladores: no dependen del serializador concreto ni de sus opciones.
/// </summary>
public interface IConversorGeoJson
{
    /// <summary>Acepta una geometría GeoJSON o un Feature (se usa su geometría).</summary>
    Geometry LeerGeometria(JsonElement json);

    /// <summary>Lee una FeatureCollection: cada Feature con su geometría y sus propiedades como texto.</summary>
    IReadOnlyList<FeatureGeo> LeerColeccion(JsonElement json);

    JsonElement AGeoJson(Geometry geometria);

    /// <summary>Las zonas como FeatureCollection, con sus datos en las propiedades de cada Feature, lista para Leaflet.</summary>
    JsonElement ColeccionDeZonas(IEnumerable<Zona> zonas);
}

/// <summary>Un Feature GeoJSON ya traducido: geometría y propiedades (null si la propiedad no viene o es null).</summary>
public sealed record FeatureGeo(Geometry? Geometria, IReadOnlyDictionary<string, string?> Propiedades)
{
    public string? Propiedad(string nombre) => Propiedades.GetValueOrDefault(nombre);
}
