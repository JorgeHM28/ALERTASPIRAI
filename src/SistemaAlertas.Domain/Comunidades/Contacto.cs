using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Comunidades;

/// <summary>Persona de referencia de una comunidad que recibe las alertas por uno o varios canales.</summary>
public class Contacto : Entidad
{
    public Guid ComunidadId { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string? Cargo { get; private set; }
    public bool Activo { get; private set; } = true;

    private readonly List<CanalContacto> _canales = [];
    public IReadOnlyCollection<CanalContacto> Canales => _canales;

    private Contacto() { }

    internal Contacto(Guid comunidadId, string nombre, string? cargo)
    {
        ComunidadId = comunidadId;
        Actualizar(nombre, cargo, true);
    }

    public void Actualizar(string nombre, string? cargo, bool activo)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ExcepcionDominio("El nombre del contacto es obligatorio.");
        Nombre = nombre.Trim();
        Cargo = string.IsNullOrWhiteSpace(cargo) ? null : cargo.Trim();
        Activo = activo;
    }

    public void ReemplazarCanales(IEnumerable<(TipoCanal Tipo, string Destino, bool Preferido)> canales)
    {
        var nuevos = canales.Select(c => new CanalContacto(c.Tipo, c.Destino, c.Preferido)).ToList();
        if (nuevos.Count == 0) throw new ExcepcionDominio("El contacto necesita al menos un canal.");
        if (nuevos.Count(c => c.Preferido) > 1) throw new ExcepcionDominio("Solo puede haber un canal preferido.");
        if (nuevos.GroupBy(c => (c.Tipo, c.Destino)).Any(g => g.Count() > 1))
            throw new ExcepcionDominio("Hay canales repetidos.");
        _canales.Clear();
        _canales.AddRange(nuevos);
    }
}
