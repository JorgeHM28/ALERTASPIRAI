using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Usuarios;

public enum AccionUsuario { Alta, RolAsignado, RolRevocado, Activado, Desactivado, DatosActualizados, ContrasenaRestablecida }

/// <summary>
/// Bitácora de RF-13: cada alta, asignación o revocación de rol y cambio de estado de un usuario,
/// con el administrador responsable y la fecha. No se edita ni se borra.
/// </summary>
public class RegistroBitacoraUsuario : Entidad
{
    public Guid UsuarioId { get; private set; }
    /// <summary>Administrador que hizo el cambio; null si lo hizo el sistema.</summary>
    public Guid? AdministradorId { get; private set; }
    public AccionUsuario Accion { get; private set; }
    public Rol? Rol { get; private set; }
    public string? Detalle { get; private set; }

    private RegistroBitacoraUsuario() { }

    public RegistroBitacoraUsuario(Guid usuarioId, Guid? administradorId, AccionUsuario accion, Rol? rol = null, string? detalle = null)
    {
        UsuarioId = usuarioId;
        AdministradorId = administradorId;
        Accion = accion;
        Rol = rol;
        Detalle = detalle;
    }
}
