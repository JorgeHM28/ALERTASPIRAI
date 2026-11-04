using NetTopologySuite.Geometries;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Avisos;

/// <summary>Aviso de amenaza recibido (por ejemplo, del SENAMHI) o registrado a mano por un operador (RF-01).</summary>
public class Aviso : Entidad
{
    public string IdentificadorCap { get; private set; } = null!;
    public string Remitente { get; private set; } = null!;
    public DateTimeOffset Enviado { get; private set; }
    public FormatoAviso Formato { get; private set; }
    public EstadoAviso Estado { get; private set; } = EstadoAviso.Registrado;
    public TipoMensajeCap TipoMensaje { get; private set; }

    public string Evento { get; private set; } = null!;
    public UrgenciaCap Urgencia { get; private set; }
    public SeveridadCap Severidad { get; private set; }
    public CertezaCap Certeza { get; private set; }
    public string? Titular { get; private set; }
    public string? Descripcion { get; private set; }
    public string? Instruccion { get; private set; }
    public DateTimeOffset? Vigente { get; private set; }
    public DateTimeOffset? Expira { get; private set; }

    public string? DescripcionArea { get; private set; }
    public Geometry? Area { get; private set; }

    /// <summary>Mensaje CAP 1.2 completo en XML, tal como se recibió o se generó.</summary>
    public string XmlCap { get; private set; } = null!;
    public Guid RegistradoPorId { get; private set; }

    private Aviso() { }

    public Aviso(MensajeCap mensaje, FormatoAviso formato, string xmlCap, Geometry? area, Guid registradoPorId)
    {
        var info = mensaje.Infos.FirstOrDefault()
            ?? throw new ExcepcionDominio("El aviso necesita al menos un bloque <info>.");
        IdentificadorCap = mensaje.Identifier;
        Remitente = mensaje.Sender;
        Enviado = mensaje.Sent.ToUniversalTime();
        TipoMensaje = mensaje.MsgType;
        Formato = formato;
        Evento = info.Event;
        Urgencia = info.Urgency;
        Severidad = info.Severity;
        Certeza = info.Certainty;
        Titular = info.Headline;
        Descripcion = info.Description;
        Instruccion = info.Instruction;
        Vigente = info.Effective?.ToUniversalTime();
        Expira = info.Expires?.ToUniversalTime();
        DescripcionArea = info.Areas.Count == 0 ? null : string.Join("; ", info.Areas.Select(a => a.AreaDesc));
        if (area is not null) area.SRID = 4326;
        Area = area;
        XmlCap = xmlCap;
        RegistradoPorId = registradoPorId;
    }

    public void Descartar()
    {
        if (Estado == EstadoAviso.Descartado) throw new ExcepcionDominio("El aviso ya está descartado.");
        Estado = EstadoAviso.Descartado;
    }
}
