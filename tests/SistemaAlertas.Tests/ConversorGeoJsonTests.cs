using System.Text.Json;
using NetTopologySuite.Geometries;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Zonas;
using SistemaAlertas.Infrastructure.Geo;

namespace SistemaAlertas.Tests;

/// <summary>Adapter: NetTopologySuite (geometrías PostGIS) a GeoJSON para Leaflet.</summary>
public class ConversorGeoJsonTests
{
    private static readonly string RutaEjemplos = Path.Combine(AppContext.BaseDirectory, "ejemplos");
    private readonly ConversorGeoJson _conversor = new();

    private static Polygon Cuadrado() => new GeometryFactory(new PrecisionModel(), Zona.Srid).CreatePolygon(
    [
        new(-63.20, -17.80), new(-63.10, -17.80), new(-63.10, -17.70), new(-63.20, -17.70), new(-63.20, -17.80)
    ]);

    [Fact]
    public void Convierte_un_poligono_a_geojson_en_orden_longitud_latitud()
    {
        var json = _conversor.AGeoJson(Cuadrado());

        Assert.Equal("Polygon", json.GetProperty("type").GetString());
        var primero = json.GetProperty("coordinates")[0][0];
        Assert.Equal(-63.20, primero[0].GetDouble(), 6);
        Assert.Equal(-17.80, primero[1].GetDouble(), 6);
    }

    [Fact]
    public void La_ida_y_vuelta_conserva_la_geometria()
    {
        var original = Cuadrado();
        var copia = _conversor.LeerGeometria(_conversor.AGeoJson(original));
        Assert.True(original.EqualsExact(copia));
    }

    [Fact]
    public void Lee_la_geometria_de_un_feature()
    {
        var feature = JsonSerializer.SerializeToElement(new
        {
            type = "Feature",
            properties = new { nombre = "Z" },
            geometry = _conversor.AGeoJson(Cuadrado())
        });
        Assert.IsType<Polygon>(_conversor.LeerGeometria(feature));
    }

    [Fact]
    public void Las_zonas_salen_como_featurecollection_con_sus_propiedades()
    {
        var zona = new Zona("Ribera Porongo", null, NivelRiesgo.Alto, Cuadrado(),
            new OrigenInstitucional("SENAMHI", "Informe 12/2026", new DateOnly(2026, 10, 1)));

        var json = _conversor.ColeccionDeZonas([zona]);

        Assert.Equal("FeatureCollection", json.GetProperty("type").GetString());
        var f = json.GetProperty("features")[0];
        Assert.Equal("Ribera Porongo", f.GetProperty("properties").GetProperty("nombre").GetString());
        Assert.Equal("Alto", f.GetProperty("properties").GetProperty("nivelRiesgo").GetString());
        Assert.Equal("2026-10-01", f.GetProperty("properties").GetProperty("fechaDocumento").GetString());
        Assert.Equal("Polygon", f.GetProperty("geometry").GetProperty("type").GetString());
    }

    [Fact]
    public void Lee_el_ejemplo_de_zonas_con_sus_propiedades()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RutaEjemplos, "zonas-pirai.geojson")));
        var features = _conversor.LeerColeccion(doc.RootElement);

        Assert.NotEmpty(features);
        Assert.All(features, f => Assert.NotNull(f.Geometria));
        Assert.All(features, f => Assert.False(string.IsNullOrWhiteSpace(f.Propiedad("nombre") ?? f.Propiedad("name"))));
    }

    [Fact]
    public void Un_geojson_invalido_es_un_error_de_dominio()
    {
        using var doc = JsonDocument.Parse("""{"type":"Polygon","coordinates":"no"}""");
        Assert.Throws<ExcepcionDominio>(() => _conversor.LeerGeometria(doc.RootElement));
    }
}
