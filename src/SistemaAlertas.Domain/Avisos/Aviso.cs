using System.Security.Cryptography;
using System.Text;
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
    /// <summary>&lt;status&gt; del CAP: solo Actual es un aviso real; Test, Exercise, etc. no se difunden.</summary>
    public EstadoCap EstadoCap { get; private set; } = EstadoCap.Actual;

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

    /// <summary>Documento tal como llegó, byte a byte (XML oficial o JSON exacto). Null en los avisos manuales.</summary>
    public string? ContenidoOriginal { get; private set; }
    /// <summary>Tipo MIME del original: application/xml o application/json.</summary>
    public string? TipoContenidoOriginal { get; private set; }
    /// <summary>SHA-256 del original en UTF-8, para comprobar que no se alteró.</summary>
    public string? HashOriginal { get; private set; }

    // Aptitud para difusión: se evalúa al registrar el aviso y cada vez que vence.
    public bool Incompleto { get; private set; }
    public bool Vencido { get; private set; }
    public bool AptoParaDifusion { get; private set; }
    public List<string> MotivosNoApto { get; private set; } = [];
    public DateTimeOffset AptitudEvaluadaEn { get; private set; }

    /// <summary>Marca de los avisos anteriores a la evaluación de aptitud: se evalúan al arrancar la API.</summary>
    public static readonly DateTimeOffset SinEvaluar = DateTimeOffset.UnixEpoch;

    private Aviso() { }

    /// <param name="contenidoOriginal">El documento importado sin tocar; null si el aviso se registró a mano.</param>
    public Aviso(MensajeCap mensaje, FormatoAviso formato, string xmlCap, Geometry? area, Guid registradoPorId,
        string? contenidoOriginal = null)
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
        EstadoCap = mensaje.Status;
        if (contenidoOriginal is not null)
        {
            ContenidoOriginal = contenidoOriginal;
            TipoContenidoOriginal = formato == FormatoAviso.CapJson ? "application/json" : "application/xml";
            HashOriginal = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenidoOriginal)));
        }
        EvaluarAptitud(DateTimeOffset.UtcNow);
    }

    /// <summary>Objeto del patrón State que corresponde al estado guardado.</summary>
    public EstadoAvisoBase EstadoActual => EstadoAvisoBase.De(Estado);

    /// <summary>Aviso de prueba o simulacro: el CAP tiene un status distinto de Actual.</summary>
    public bool EsPrueba => EstadoCap != EstadoCap.Actual;

    // State: cada acción la decide el estado actual, que devuelve el estado siguiente o rechaza la acción.
    public void Aprobar() => Estado = EstadoActual.Aprobar(this);

    public void MarcarDifundido() => Estado = EstadoActual.Difundir(this);

    public void Descartar()
    {
        Estado = EstadoActual.Descartar(this);
        EvaluarAptitud(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Decide si el aviso puede difundirse y deja registrados los motivos si no.
    /// Incompleto: le falta algo que la población necesita (titular o descripción, instrucción, área, expiración).
    /// Vencido: ya pasó su fecha de expiración. Devuelve true si cambió el resultado.
    /// </summary>
    public bool EvaluarAptitud(DateTimeOffset ahora)
    {
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(Titular) && string.IsNullOrWhiteSpace(Descripcion)) faltantes.Add("Falta el titular o la descripción.");
        if (string.IsNullOrWhiteSpace(Instruccion)) faltantes.Add("Falta la instrucción a la población.");
        if (Area is null || Area.IsEmpty) faltantes.Add("Falta el área afectada (polígono).");
        if (Expira is null) faltantes.Add("Falta la fecha de expiración.");

        var vencido = Expira is { } e && e <= ahora;
        var motivos = new List<string>(faltantes);
        if (vencido) motivos.Add($"Vencido: expiró el {Expira!.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC.");
        if (EstadoCap != EstadoCap.Actual) motivos.Add($"El CAP tiene status {EstadoCap}: no es un aviso real.");
        if (TipoMensaje is TipoMensajeCap.Cancel or TipoMensajeCap.Ack or TipoMensajeCap.Error)
            motivos.Add($"Un mensaje {TipoMensaje} no se difunde a la población.");
        if (Estado == EstadoAviso.Descartado) motivos.Add("El aviso fue descartado.");

        var cambio = AptitudEvaluadaEn <= SinEvaluar || Incompleto != faltantes.Count > 0 || Vencido != vencido
            || !MotivosNoApto.SequenceEqual(motivos);
        Incompleto = faltantes.Count > 0;
        Vencido = vencido;
        AptoParaDifusion = motivos.Count == 0;
        if (cambio)
        {
            MotivosNoApto = motivos;
            AptitudEvaluadaEn = ahora;
        }
        return cambio;
    }
}
