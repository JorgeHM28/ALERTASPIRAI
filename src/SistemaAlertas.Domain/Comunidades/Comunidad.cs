using NetTopologySuite.Geometries;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Domain.Comunidades;

public class Comunidad : Entidad
{
    public string Nombre { get; private set; } = null!;
    public string? Municipio { get; private set; }
    public int? Poblacion { get; private set; }
    public Point? Ubicacion { get; private set; }
    public Guid ZonaId { get; private set; }
    public Zona? Zona { get; private set; }

    private readonly List<Contacto> _contactos = [];
    public IReadOnlyCollection<Contacto> Contactos => _contactos;

    private Comunidad() { }

    public Comunidad(string nombre, string? municipio, int? poblacion, Point? ubicacion, Guid zonaId)
    {
        Actualizar(nombre, municipio, poblacion, ubicacion, zonaId);
    }

    public void Actualizar(string nombre, string? municipio, int? poblacion, Point? ubicacion, Guid zonaId)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ExcepcionDominio("El nombre de la comunidad es obligatorio.");
        if (poblacion is < 0) throw new ExcepcionDominio("La población no puede ser negativa.");
        if (zonaId == Guid.Empty) throw new ExcepcionDominio("La comunidad debe pertenecer a una zona.");
        Nombre = nombre.Trim();
        Municipio = string.IsNullOrWhiteSpace(municipio) ? null : municipio.Trim();
        Poblacion = poblacion;
        if (ubicacion is not null) ubicacion.SRID = Zona.Srid;
        Ubicacion = ubicacion;
        ZonaId = zonaId;
    }

    public Contacto AgregarContacto(string nombre, string? cargo)
    {
        var contacto = new Contacto(Id, nombre, cargo);
        _contactos.Add(contacto);
        return contacto;
    }
}
