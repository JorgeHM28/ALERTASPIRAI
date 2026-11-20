using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Comunidades;

public enum AccionContacto { Alta, Modificacion, Baja }

/// <summary>
/// Historial de RF-12: cada alta, cambio o baja de un contacto, con quién lo hizo y el estado resultante.
/// No tiene clave foránea al contacto, así que se conserva aunque el contacto se elimine.
/// </summary>
public class HistorialContacto : Entidad
{
    public Guid ContactoId { get; private set; }
    public Guid ComunidadId { get; private set; }
    public AccionContacto Accion { get; private set; }
    public Guid UsuarioId { get; private set; }
    /// <summary>Estado del contacto después del cambio (antes, en una baja), en JSON.</summary>
    public string Instantanea { get; private set; } = null!;
    /// <summary>Qué campos cambiaron, en una modificación.</summary>
    public string? Cambios { get; private set; }

    private HistorialContacto() { }

    public HistorialContacto(Contacto contacto, AccionContacto accion, Guid usuarioId, string instantaneaJson, string? cambios = null)
    {
        ContactoId = contacto.Id;
        ComunidadId = contacto.ComunidadId;
        Accion = accion;
        UsuarioId = usuarioId;
        Instantanea = instantaneaJson;
        Cambios = cambios;
    }
}
