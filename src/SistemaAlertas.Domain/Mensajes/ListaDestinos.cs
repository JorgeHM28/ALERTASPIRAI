using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Comunidades;

namespace SistemaAlertas.Domain.Mensajes;

/// <summary>
/// RF-07: destinos de una versión aprobada, elegidos por comunidad. Solo entran contactos habilitados de las
/// comunidades dentro del alcance del aviso, por los canales aprobados. Un destino (canal + dirección) aparece una
/// sola vez por envío y versión: la clave de la tabla lo garantiza. Las comunidades sin canal quedan como pendientes.
/// </summary>
public class ListaDestinos : Entidad
{
    public Guid VersionId { get; private set; }
    public Guid GeneradaPorId { get; private set; }
    public List<TipoCanal> Canales { get; private set; } = [];

    private readonly List<DestinoEnvio> _destinos = [];
    public IReadOnlyCollection<DestinoEnvio> Destinos => _destinos;

    private readonly List<ComunidadPendiente> _pendientes = [];
    public IReadOnlyCollection<ComunidadPendiente> Pendientes => _pendientes;

    private ListaDestinos() { }

    public ListaDestinos(Guid versionId, IEnumerable<TipoCanal> canales, Guid generadaPorId)
    {
        Canales = canales.Distinct().Order().ToList();
        if (Canales.Count == 0) throw new ExcepcionDominio("Elige al menos un canal aprobado.");
        VersionId = versionId;
        GeneradaPorId = generadaPorId;
    }

    /// <summary>Agrega el destino si no estaba ya (mismo canal y dirección). Devuelve false si era un duplicado.</summary>
    public bool AgregarDestino(Guid comunidadId, Guid contactoId, TipoCanal canal, string direccion)
    {
        if (!Canales.Contains(canal))
            throw new ExcepcionDominio($"El canal {canal} no está entre los canales elegidos para este envío.");
        if (_destinos.Any(d => d.Canal == canal && d.Direccion == direccion)) return false;
        _destinos.Add(new DestinoEnvio(comunidadId, contactoId, canal, direccion));
        return true;
    }

    public void AgregarPendiente(Guid comunidadId, string comunidad, string motivo)
    {
        if (_pendientes.All(p => p.ComunidadId != comunidadId))
            _pendientes.Add(new ComunidadPendiente(comunidadId, comunidad, motivo));
    }
}

public class DestinoEnvio
{
    public Guid ComunidadId { get; private set; }
    public Guid ContactoId { get; private set; }
    public TipoCanal Canal { get; private set; }
    /// <summary>Teléfono, correo o emisora, ya normalizado por <see cref="CanalContacto"/>.</summary>
    public string Direccion { get; private set; } = null!;

    private DestinoEnvio() { }

    public DestinoEnvio(Guid comunidadId, Guid contactoId, TipoCanal canal, string direccion)
    {
        ComunidadId = comunidadId;
        ContactoId = contactoId;
        Canal = canal;
        Direccion = direccion;
    }
}

public class ComunidadPendiente
{
    public Guid ComunidadId { get; private set; }
    public string Comunidad { get; private set; } = null!;
    public string Motivo { get; private set; } = null!;

    private ComunidadPendiente() { }

    public ComunidadPendiente(Guid comunidadId, string comunidad, string motivo)
    {
        ComunidadId = comunidadId;
        Comunidad = comunidad;
        Motivo = motivo;
    }
}
