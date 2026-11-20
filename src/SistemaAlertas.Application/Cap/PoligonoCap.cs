using System.Globalization;
using NetTopologySuite.Geometries;

namespace SistemaAlertas.Application.Cap;

/// <summary>Conversión entre el formato de polígono CAP ("lat,lon lat,lon ...") y geometrías NetTopologySuite.</summary>
public static class PoligonoCap
{
    private static readonly GeometryFactory Fabrica = new(new PrecisionModel(), 4326);

    public static string? Validar(string poligono)
    {
        if (!IntentarLeer(poligono, out var coords, out var error)) return error;
        if (coords.Length < 4) return "necesita al menos 4 pares de coordenadas.";
        if (!coords[0].Equals2D(coords[^1])) return "el primer y el último punto deben ser iguales.";
        return null;
    }

    public static Polygon ALaGeometria(string poligono)
    {
        if (!IntentarLeer(poligono, out var coords, out var error)) throw new FormatException(error);
        return Fabrica.CreatePolygon(coords);
    }

    /// <summary>Une los polígonos de todas las áreas en una sola geometría (null si no hay ninguno).</summary>
    public static Geometry? UnirAreas(IEnumerable<string> poligonos)
    {
        var lista = poligonos.Select(ALaGeometria).ToArray();
        return lista.Length switch
        {
            0 => null,
            1 => lista[0],
            _ => Fabrica.CreateMultiPolygon(lista)
        };
    }

    public static string DesdeGeometria(Polygon poligono) =>
        string.Join(' ', poligono.ExteriorRing.Coordinates.Select(c =>
            string.Create(CultureInfo.InvariantCulture, $"{c.Y},{c.X}")));

    private static bool IntentarLeer(string texto, out Coordinate[] coords, out string? error)
    {
        coords = [];
        error = null;
        var lista = new List<Coordinate>();
        foreach (var par in (texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = par.Split(',');
            if (partes.Length != 2
                || !double.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            {
                error = $"'{par}' no es un par 'lat,lon' válido.";
                return false;
            }
            if (lat is < -90 or > 90 || lon is < -180 or > 180)
            {
                error = $"'{par}' está fuera del rango WGS84.";
                return false;
            }
            lista.Add(new Coordinate(lon, lat));
        }
        coords = lista.ToArray();
        return true;
    }
}
