using NetTopologySuite.Geometries;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Zonas;

/// <summary>Zona geográfica de riesgo de inundación (polígono WGS84, SRID 4326).</summary>
public class Zona : Entidad
{
    public const int Srid = 4326;

    public string Nombre { get; private set; } = null!;
    public string? Descripcion { get; private set; }
    public NivelRiesgo NivelRiesgo { get; private set; }
    public Geometry Area { get; private set; } = null!;

    private Zona() { }

    public Zona(string nombre, string? descripcion, NivelRiesgo nivelRiesgo, Geometry area)
    {
        Actualizar(nombre, descripcion, nivelRiesgo, area);
    }

    public void Actualizar(string nombre, string? descripcion, NivelRiesgo nivelRiesgo, Geometry area)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ExcepcionDominio("El nombre de la zona es obligatorio.");
        ValidarArea(area);
        Nombre = nombre.Trim();
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        NivelRiesgo = nivelRiesgo;
        area.SRID = Srid;
        Area = area;
    }

    public static void ValidarArea(Geometry? area)
    {
        if (area is null) throw new ExcepcionDominio("La zona necesita una geometría.");
        if (area is not (Polygon or MultiPolygon))
            throw new ExcepcionDominio($"La geometría debe ser Polygon o MultiPolygon, no {area.GeometryType}.");
        if (area.IsEmpty) throw new ExcepcionDominio("La geometría está vacía.");
        foreach (var c in area.Coordinates)
        {
            if (c.X is < -180 or > 180 || c.Y is < -90 or > 90)
                throw new ExcepcionDominio("Las coordenadas deben estar en WGS84 (longitud, latitud).");
        }
        if (!area.IsValid) throw new ExcepcionDominio("La geometría no es válida (por ejemplo, el polígono se cruza consigo mismo).");
    }
}
