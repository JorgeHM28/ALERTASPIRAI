using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Comunidades;

/// <summary>Persona de referencia de una comunidad que recibe las alertas por uno o varios canales.</summary>
public class Contacto : Entidad
{
    public Guid ComunidadId { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string? Cargo { get; private set; }
    public bool Activo { get; private set; } = true;

    /// <summary>De dónde salió el dato (por ejemplo, "Asamblea comunal", "Padrón municipal de Porongo").</summary>
    public string Procedencia { get; private set; } = null!;
    /// <summary>La persona autorizó recibir alertas en estos canales.</summary>
    public bool Autorizado { get; private set; }
    /// <summary>Respaldo de la autorización: número de acta, formulario firmado, etc.</summary>
    public string? ReferenciaAutorizacion { get; private set; }
    public DateOnly? FechaAutorizacion { get; private set; }

    private readonly List<CanalContacto> _canales = [];
    public IReadOnlyCollection<CanalContacto> Canales => _canales;

    /// <summary>Solo un contacto activo, autorizado y con canales puede recibir alertas.</summary>
    public bool Habilitado => Activo && Autorizado && _canales.Count > 0;

    public IReadOnlyList<string> MotivosNoHabilitado =>
    [
        .. Activo ? Array.Empty<string>() : ["inactivo"],
        .. Autorizado ? Array.Empty<string>() : ["sin autorización"],
        .. _canales.Count > 0 ? Array.Empty<string>() : ["sin canales"]
    ];

    private Contacto() { }

    internal Contacto(Guid comunidadId, DatosContacto datos)
    {
        ComunidadId = comunidadId;
        Actualizar(datos);
    }

    public void Actualizar(DatosContacto d)
    {
        if (string.IsNullOrWhiteSpace(d.Nombre)) throw new ExcepcionDominio("El nombre del contacto es obligatorio.");
        if (string.IsNullOrWhiteSpace(d.Procedencia)) throw new ExcepcionDominio("Indica la procedencia del contacto (de dónde se obtuvo el dato).");
        if (d.Autorizado && string.IsNullOrWhiteSpace(d.ReferenciaAutorizacion))
            throw new ExcepcionDominio("Un contacto autorizado necesita la referencia de su autorización (acta, formulario, etc.).");
        if (d.FechaAutorizacion is { } f && f > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            throw new ExcepcionDominio("La fecha de autorización no puede ser futura.");
        Nombre = d.Nombre.Trim();
        Cargo = string.IsNullOrWhiteSpace(d.Cargo) ? null : d.Cargo.Trim();
        Activo = d.Activo;
        Procedencia = d.Procedencia.Trim();
        Autorizado = d.Autorizado;
        ReferenciaAutorizacion = string.IsNullOrWhiteSpace(d.ReferenciaAutorizacion) ? null : d.ReferenciaAutorizacion.Trim();
        FechaAutorizacion = d.FechaAutorizacion;
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

    /// <summary>Foto del contacto para el historial.</summary>
    public InstantaneaContacto Instantanea() => new(Nombre, Cargo, Activo, Procedencia, Autorizado, ReferenciaAutorizacion,
        FechaAutorizacion, _canales.Select(c => $"{c.Tipo}:{c.Destino}{(c.Preferido ? " (preferido)" : "")}").ToList());
}

public sealed record DatosContacto(string Nombre, string? Cargo, bool Activo, string Procedencia, bool Autorizado,
    string? ReferenciaAutorizacion, DateOnly? FechaAutorizacion);

public sealed record InstantaneaContacto(string Nombre, string? Cargo, bool Activo, string Procedencia, bool Autorizado,
    string? ReferenciaAutorizacion, DateOnly? FechaAutorizacion, IReadOnlyList<string> Canales);
