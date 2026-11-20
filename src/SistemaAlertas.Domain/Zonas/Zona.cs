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

    /// <summary>Institución que delimitó la zona (por ejemplo, SENAMHI o la Gobernación de Santa Cruz).</summary>
    public string? FuenteInstitucional { get; private set; }
    /// <summary>Referencia del documento de origen: número de informe, resolución o mapa oficial.</summary>
    public string? ReferenciaDocumento { get; private set; }
    public DateOnly? FechaDocumento { get; private set; }

    private Zona() { }

    public Zona(string nombre, string? descripcion, NivelRiesgo nivelRiesgo, Geometry area, OrigenInstitucional origen)
    {
        Actualizar(nombre, descripcion, nivelRiesgo, area, origen);
    }

    /// <param name="origen">Si es null se conservan los datos institucionales que ya tenía la zona.</param>
    public void Actualizar(string nombre, string? descripcion, NivelRiesgo nivelRiesgo, Geometry area, OrigenInstitucional? origen)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ExcepcionDominio("El nombre de la zona es obligatorio.");
        ValidarArea(area);
        var datos = origen ?? (FuenteInstitucional is not null && ReferenciaDocumento is not null && FechaDocumento is not null
            ? new OrigenInstitucional(FuenteInstitucional, ReferenciaDocumento, FechaDocumento.Value)
            : throw new ExcepcionDominio("Indica la fuente institucional, la referencia y la fecha del documento de origen de la zona."));
        datos.Validar();
        Nombre = nombre.Trim();
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        NivelRiesgo = nivelRiesgo;
        area.SRID = Srid;
        Area = area;
        FuenteInstitucional = datos.Fuente.Trim();
        ReferenciaDocumento = datos.Referencia.Trim();
        FechaDocumento = datos.Fecha;
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

/// <summary>Documento oficial del que sale la delimitación de una zona (RF-09).</summary>
public sealed record OrigenInstitucional(string Fuente, string Referencia, DateOnly Fecha)
{
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Fuente)) throw new ExcepcionDominio("La fuente institucional de la zona es obligatoria.");
        if (string.IsNullOrWhiteSpace(Referencia)) throw new ExcepcionDominio("La referencia del documento de origen es obligatoria.");
        if (Fuente.Trim().Length > 150) throw new ExcepcionDominio("La fuente institucional admite hasta 150 caracteres.");
        if (Referencia.Trim().Length > 200) throw new ExcepcionDominio("La referencia del documento admite hasta 200 caracteres.");
        if (Fecha > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            throw new ExcepcionDominio("La fecha del documento de origen no puede ser futura.");
    }

    /// <summary>Arma el origen solo si llegaron los tres datos; si falta alguno pero no todos, avisa.</summary>
    public static OrigenInstitucional? Desde(string? fuente, string? referencia, DateOnly? fecha)
    {
        if (string.IsNullOrWhiteSpace(fuente) && string.IsNullOrWhiteSpace(referencia) && fecha is null) return null;
        if (string.IsNullOrWhiteSpace(fuente) || string.IsNullOrWhiteSpace(referencia) || fecha is null)
            throw new ExcepcionDominio("Los datos institucionales van juntos: fuente, referencia y fecha del documento.");
        return new OrigenInstitucional(fuente, referencia, fecha.Value);
    }
}
