using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Mensajes;

/// <summary>
/// Ciclo de vida de una versión del mensaje (RF-02, RF-11):
/// Borrador → EnRevision → Aprobada → Difundida, o EnRevision → Rechazada (y su corrección es otra versión).
/// </summary>
public enum EstadoVersion { Borrador, EnRevision, Aprobada, Rechazada, Difundida }

/// <summary>
/// Patrón State (RF-11). Cada estado de la versión decide qué acciones permite y a qué estado lleva.
/// Por ejemplo, solo el borrador se edita y solo una versión en revisión se aprueba o rechaza: una versión
/// aprobada no se puede editar porque su estado no lo permite, no porque haya un if en cada método.
/// </summary>
public abstract class EstadoVersionBase
{
    public abstract EstadoVersion Valor { get; }

    /// <summary>True si una versión en este estado puede pasar a los canales de difusión.</summary>
    public virtual bool PermiteDifundir => false;

    public virtual void Editar(VersionMensaje v) => throw NoPermitido("editar");
    public virtual EstadoVersion EnviarARevision(VersionMensaje v, DateTimeOffset ahora) => throw NoPermitido("enviar a revisión");
    public virtual EstadoVersion Aprobar(VersionMensaje v, DateTimeOffset ahora) => throw NoPermitido("aprobar");
    public virtual EstadoVersion Rechazar(VersionMensaje v) => throw NoPermitido("rechazar");
    public virtual void Corregir(VersionMensaje v) => throw NoPermitido("corregir");
    public virtual EstadoVersion Difundir(VersionMensaje v) => throw NoPermitido("difundir");

    protected ExcepcionDominio NoPermitido(string accion) =>
        new($"Una versión en estado {Valor} no se puede {accion}.");

    public static EstadoVersionBase De(EstadoVersion estado) => estado switch
    {
        EstadoVersion.Borrador => EstadoVersionBorrador.Instancia,
        EstadoVersion.EnRevision => EstadoVersionEnRevision.Instancia,
        EstadoVersion.Aprobada => EstadoVersionAprobada.Instancia,
        EstadoVersion.Rechazada => EstadoVersionRechazada.Instancia,
        EstadoVersion.Difundida => EstadoVersionDifundida.Instancia,
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado de versión desconocido.")
    };
}

/// <summary>Se edita libremente. Pasa a revisión solo si está completa; si no, queda como borrador.</summary>
public sealed class EstadoVersionBorrador : EstadoVersionBase
{
    public static readonly EstadoVersionBorrador Instancia = new();
    private EstadoVersionBorrador() { }
    public override EstadoVersion Valor => EstadoVersion.Borrador;

    public override void Editar(VersionMensaje v) { }

    public override EstadoVersion EnviarARevision(VersionMensaje v, DateTimeOffset ahora)
    {
        var faltan = v.Faltantes(ahora);
        if (faltan.Count > 0)
            throw new ExcepcionDominio("La versión está incompleta y queda como borrador: " + string.Join(" ", faltan));
        return EstadoVersion.EnRevision;
    }
}

/// <summary>Esperando al aprobador: no se edita; se aprueba o se rechaza con motivo.</summary>
public sealed class EstadoVersionEnRevision : EstadoVersionBase
{
    public static readonly EstadoVersionEnRevision Instancia = new();
    private EstadoVersionEnRevision() { }
    public override EstadoVersion Valor => EstadoVersion.EnRevision;

    public override void Editar(VersionMensaje v) =>
        throw new ExcepcionDominio("La versión está en revisión: no se edita. Si hay que cambiarla, el aprobador la rechaza con su motivo.");

    public override EstadoVersion Aprobar(VersionMensaje v, DateTimeOffset ahora)
    {
        // Se vuelve a revisar al aprobar: pudo vencer mientras esperaba. Sin instrucciones no se aprueba.
        var faltan = v.Faltantes(ahora);
        if (faltan.Count > 0) throw new ExcepcionDominio("La versión no se puede aprobar: " + string.Join(" ", faltan));
        return EstadoVersion.Aprobada;
    }

    public override EstadoVersion Rechazar(VersionMensaje v) => EstadoVersion.Rechazada;
}

/// <summary>Inmutable. Se puede difundir; corregirla es preparar un Update (otra versión).</summary>
public sealed class EstadoVersionAprobada : EstadoVersionBase
{
    public static readonly EstadoVersionAprobada Instancia = new();
    private EstadoVersionAprobada() { }
    public override EstadoVersion Valor => EstadoVersion.Aprobada;
    public override bool PermiteDifundir => true;

    public override void Editar(VersionMensaje v) =>
        throw new ExcepcionDominio("Una versión aprobada no se puede editar: para corregirla se prepara un Update, que es otra versión.");
    public override void Corregir(VersionMensaje v) =>
        throw new ExcepcionDominio("Una versión aprobada no se corrige: se prepara un Update o un Cancel que la referencia.");
    public override EstadoVersion Difundir(VersionMensaje v) => EstadoVersion.Difundida;
}

/// <summary>Volvió con su motivo. No se edita: la corrección crea una versión nueva en borrador.</summary>
public sealed class EstadoVersionRechazada : EstadoVersionBase
{
    public static readonly EstadoVersionRechazada Instancia = new();
    private EstadoVersionRechazada() { }
    public override EstadoVersion Valor => EstadoVersion.Rechazada;

    public override void Editar(VersionMensaje v) =>
        throw new ExcepcionDominio("Una versión rechazada no se edita: corregirla crea una versión nueva.");
    public override void Corregir(VersionMensaje v) { }
}

/// <summary>Ya llegó a la población. Se puede volver a difundir (si un canal falló); no se edita.</summary>
public sealed class EstadoVersionDifundida : EstadoVersionBase
{
    public static readonly EstadoVersionDifundida Instancia = new();
    private EstadoVersionDifundida() { }
    public override EstadoVersion Valor => EstadoVersion.Difundida;
    public override bool PermiteDifundir => true;

    public override void Editar(VersionMensaje v) =>
        throw new ExcepcionDominio("Una versión difundida no se puede editar: para corregirla se prepara un Update.");
    public override void Corregir(VersionMensaje v) =>
        throw new ExcepcionDominio("Una versión difundida no se corrige: se prepara un Update o un Cancel que la referencia.");
    public override EstadoVersion Difundir(VersionMensaje v) => EstadoVersion.Difundida;
}
