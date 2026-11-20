using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Avisos;

/// <summary>
/// Patrón State (RF-11). Cada estado del aviso decide qué acciones permite y a qué estado lleva.
/// El aviso no tiene un switch por estado: delega en el objeto de su estado actual.
/// Agregar un estado (por ejemplo "En revisión" en el Sprint 3) es una clase nueva, sin tocar las otras.
/// Los estados no guardan datos, así que hay una sola instancia de cada uno.
/// </summary>
public abstract class EstadoAvisoBase
{
    public abstract EstadoAviso Valor { get; }

    /// <summary>True si un aviso en este estado puede pasar a los canales de difusión.</summary>
    public virtual bool PermiteDifundir => false;

    public virtual EstadoAviso Aprobar(Aviso aviso) => throw NoPermitido("aprobar");
    public virtual EstadoAviso Descartar(Aviso aviso) => throw NoPermitido("descartar");
    public virtual EstadoAviso Difundir(Aviso aviso) => throw NoPermitido("difundir");

    protected ExcepcionDominio NoPermitido(string accion) =>
        new($"Un aviso en estado {Valor} no se puede {accion}.");

    public static EstadoAvisoBase De(EstadoAviso estado) => estado switch
    {
        EstadoAviso.Registrado => EstadoRegistrado.Instancia,
        EstadoAviso.Aprobado => EstadoAprobado.Instancia,
        EstadoAviso.Difundido => EstadoDifundido.Instancia,
        EstadoAviso.Descartado => EstadoDescartado.Instancia,
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado de aviso desconocido.")
    };
}

/// <summary>Recién registrado o importado. De él se preparan versiones; se puede descartar.</summary>
public sealed class EstadoRegistrado : EstadoAvisoBase
{
    public static readonly EstadoRegistrado Instancia = new();
    private EstadoRegistrado() { }
    public override EstadoAviso Valor => EstadoAviso.Registrado;

    /// <summary>
    /// Desde el Sprint 2 lo que se aprueba es una versión del mensaje (RF-11), que ya pasó sus propias reglas.
    /// El aviso pasa a Aprobado cuando se aprueba la primera versión que sale de él.
    /// </summary>
    public override EstadoAviso Aprobar(Aviso aviso) => EstadoAviso.Aprobado;

    public override EstadoAviso Descartar(Aviso aviso) => EstadoAviso.Descartado;
}

/// <summary>Tiene una versión aprobada por un aprobador. Ya se puede difundir; todavía se puede descartar.</summary>
public sealed class EstadoAprobado : EstadoAvisoBase
{
    public static readonly EstadoAprobado Instancia = new();
    private EstadoAprobado() { }
    public override EstadoAviso Valor => EstadoAviso.Aprobado;
    public override bool PermiteDifundir => true;

    public override EstadoAviso Aprobar(Aviso aviso) => throw new ExcepcionDominio("El aviso ya está aprobado.");
    public override EstadoAviso Descartar(Aviso aviso) => EstadoAviso.Descartado;
    public override EstadoAviso Difundir(Aviso aviso) => EstadoAviso.Difundido;
}

/// <summary>Ya llegó a la población. Se puede volver a difundir (por ejemplo, si un canal falló), pero no descartar.</summary>
public sealed class EstadoDifundido : EstadoAvisoBase
{
    public static readonly EstadoDifundido Instancia = new();
    private EstadoDifundido() { }
    public override EstadoAviso Valor => EstadoAviso.Difundido;
    public override bool PermiteDifundir => true;

    public override EstadoAviso Aprobar(Aviso aviso) => throw new ExcepcionDominio("El aviso ya fue aprobado y difundido.");
    public override EstadoAviso Descartar(Aviso aviso) =>
        throw new ExcepcionDominio("El aviso ya se difundió: no se descarta, se corrige con un mensaje CAP Update o Cancel.");
    public override EstadoAviso Difundir(Aviso aviso) => EstadoAviso.Difundido;
}

/// <summary>Estado final: no admite ninguna acción.</summary>
public sealed class EstadoDescartado : EstadoAvisoBase
{
    public static readonly EstadoDescartado Instancia = new();
    private EstadoDescartado() { }
    public override EstadoAviso Valor => EstadoAviso.Descartado;
    public override EstadoAviso Descartar(Aviso aviso) => throw new ExcepcionDominio("El aviso ya está descartado.");
}
