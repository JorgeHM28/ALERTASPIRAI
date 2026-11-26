using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Zonas;
using SistemaAlertas.Infrastructure.Geo;

namespace SistemaAlertas.Infrastructure.Persistence;

/// <summary>
/// Carga los datos reales de la cuenca del Piraí: los 22 municipios como zonas (RF-09) y sus localidades
/// como comunidades (RF-12), sin contactos. Los archivos están en datos/cuenca-pirai (fuentes y criterio en su README)
/// y van incrustados en el ensamblado. Solo corre si la tabla de zonas está vacía.
/// </summary>
public static class SemillaCuencaPirai
{
    public static readonly OrigenInstitucional Origen = new(
        "GeoBolivia (límites municipales, vía geoBoundaries)", "gbOpen BOL ADM3 · municipios 2015", new DateOnly(2015, 1, 1));

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private sealed record ComunidadArchivo(string Nombre, string Municipio, int? Poblacion, double Latitud, double Longitud);

    public static async Task<int> CargarAsync(AlertasDbContext db, CancellationToken ct)
    {
        if (await db.Zonas.AnyAsync(ct)) return 0;

        var conversor = new ConversorGeoJson();
        using var zonasJson = JsonDocument.Parse(Leer("zonas-municipios.geojson"));
        var zonas = new Dictionary<string, Zona>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in conversor.LeerColeccion(zonasJson.RootElement))
        {
            var nombre = f.Propiedad("nombre")!;
            var nivel = Enum.Parse<NivelRiesgo>(f.Propiedad("nivelRiesgo")!, true);
            var zona = new Zona(nombre, f.Propiedad("descripcion"), nivel, f.Geometria!, Origen);
            zonas[nombre] = zona;
            db.Zonas.Add(zona);
        }

        var comunidades = JsonSerializer.Deserialize<List<ComunidadArchivo>>(Leer("comunidades.json"), OpcionesJson) ?? [];
        foreach (var c in comunidades)
        {
            var zona = zonas[c.Municipio];
            var punto = new Point(c.Longitud, c.Latitud) { SRID = Zona.Srid };
            db.Comunidades.Add(new Comunidad(c.Nombre, c.Municipio, c.Poblacion, punto, zona.Id));
        }

        await db.SaveChangesAsync(ct);
        return zonas.Count;
    }

    private static string Leer(string archivo)
    {
        var ensamblado = typeof(SemillaCuencaPirai).Assembly;
        var recurso = ensamblado.GetManifestResourceNames().Single(n => n.EndsWith("CuencaPirai." + archivo, StringComparison.Ordinal));
        using var stream = ensamblado.GetManifestResourceStream(recurso)!;
        using var lector = new StreamReader(stream);
        return lector.ReadToEnd();
    }
}
