using NetTopologySuite.Geometries;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Mensajes;

/// <summary>
/// Una versión del mensaje CAP que se prepara a partir de un aviso oficial (RF-02) y que un aprobador aprueba o
/// rechaza (RF-11). El aviso oficial no se toca: de él salen versiones Alert, Update o Cancel.
/// Una versión aprobada no se edita; corregirla crea otra versión (Prototype, <see cref="ContenidoMensaje"/>).
/// Todas las versiones que hablan del mismo evento comparten la <see cref="CadenaId"/> (el aviso que lo originó).
/// </summary>
public class VersionMensaje : Entidad
{
    public const int LargoMinimoMotivo = 5;

    /// <summary>Aviso oficial del que sale esta versión (puede ser un Update del SENAMHI).</summary>
    public Guid AvisoId { get; private set; }
    /// <summary>Aviso original del evento: agrupa la Alert, sus Update y su Cancel.</summary>
    public Guid CadenaId { get; private set; }
    /// <summary>Número correlativo dentro de la cadena (1, 2, 3…).</summary>
    public int Numero { get; private set; }
    public TipoMensajeCap Tipo { get; private set; }
    public EstadoVersion Estado { get; private set; } = EstadoVersion.Borrador;
    /// <summary>El aviso oficial es de prueba o simulacro: el CAP sale con status Exercise/Test y no llega a la web pública.</summary>
    public EstadoCap EstadoCap { get; private set; } = EstadoCap.Actual;

    public ContenidoMensaje Contenido { get; private set; } = null!;
    public Geometry? Area { get; private set; }

    /// <summary>&lt;identifier&gt; y &lt;sender&gt; del mensaje CAP que genera el sistema para esta versión.</summary>
    public string IdentificadorCap { get; private set; } = null!;
    public string Remitente { get; private set; } = null!;
    /// <summary>&lt;sent&gt;: se fija al enviar a revisión, junto con el XML.</summary>
    public DateTimeOffset? EnviadoCap { get; private set; }
    public string? XmlCap { get; private set; }

    /// <summary>Versión a la que se refiere un Update o un Cancel, y su &lt;references&gt; en formato CAP.</summary>
    public Guid? ReferenciaVersionId { get; private set; }
    public string? Referencias { get; private set; }

    /// <summary>Versión rechazada que esta corrige, y el motivo con el que volvió.</summary>
    public Guid? VersionAnteriorId { get; private set; }
    public string? MotivoDevolucion { get; private set; }

    public Guid PreparadoPorId { get; private set; }
    public DateTimeOffset? EnviadaARevisionEn { get; private set; }

    // RF-11: quién evaluó, cuándo y con qué resultado. La versión evaluada es esta misma fila (Id + Numero).
    public Guid? RevisadoPorId { get; private set; }
    public DateTimeOffset? RevisadoEn { get; private set; }
    public string? MotivoRechazo { get; private set; }

    public DateTimeOffset? DifundidaEn { get; private set; }
    /// <summary>Cuándo se publicó en la web pública al difundirla (RF-04, Sprint 3).</summary>
    public DateTimeOffset? PublicadaWebEn { get; private set; }
    /// <summary>Un Update o Cancel aprobado que la reemplaza. Mientras sea null, es la versión vigente de su cadena.</summary>
    public Guid? ReemplazadaPorId { get; private set; }

    /// <summary>RF-05: protocolo oficial del que salen las instrucciones. Título y cita se copian al vincularlo,
    /// así la versión conserva de dónde salió su texto aunque el protocolo cambie después.</summary>
    public Guid? ProtocoloId { get; private set; }
    public string? ProtocoloTitulo { get; private set; }
    /// <summary>Institución, documento y fecha del protocolo: "COED Santa Cruz, Plan 01/2026, 15/01/2026".</summary>
    public string? ProtocoloCita { get; private set; }

    private VersionMensaje() { }

    /// <summary>Primera versión o versión derivada (Update/Cancel) de una cadena.</summary>
    public VersionMensaje(Guid avisoId, Guid cadenaId, int numero, TipoMensajeCap tipo, ContenidoMensaje contenido,
        Geometry? area, EstadoCap estadoCap, string remitente, Guid preparadoPorId, VersionMensaje? referencia = null)
    {
        if (tipo is not (TipoMensajeCap.Alert or TipoMensajeCap.Update or TipoMensajeCap.Cancel))
            throw new ExcepcionDominio("Una versión del mensaje solo puede ser Alert, Update o Cancel.");
        if (tipo == TipoMensajeCap.Alert && referencia is not null)
            throw new ExcepcionDominio("Un mensaje Alert no lleva referencias: es el primero de su cadena.");
        if (tipo != TipoMensajeCap.Alert)
        {
            if (referencia is null) throw new ExcepcionDominio($"Un mensaje {tipo} debe referirse a una versión aprobada.");
            VerificarReferencia(referencia, cadenaId);
            ReferenciaVersionId = referencia.Id;
            Referencias = referencia.ReferenciaCap;
        }
        AvisoId = avisoId;
        CadenaId = cadenaId;
        Numero = numero;
        Tipo = tipo;
        Contenido = contenido;
        if (area is not null) area.SRID = 4326;
        Area = area;
        EstadoCap = estadoCap;
        Remitente = remitente;
        PreparadoPorId = preparadoPorId;
        IdentificadorCap = $"SAT-PIRAI-{CreadoEn:yyyyMMdd}-{Id.ToString("N")[..12]}";
        // Un Update que conserva las instrucciones de la versión que corrige conserva también su protocolo.
        // Un Cancel nunca: su mensaje es que la alerta terminó, no las instrucciones de la alerta.
        if (tipo != TipoMensajeCap.Cancel && referencia?.ProtocoloId is not null && contenido.Instruccion == referencia.Contenido.Instruccion)
            CopiarProtocolo(referencia);
    }

    /// <summary>
    /// Prototype: la corrección de una versión rechazada es un clon con número nuevo, de vuelta en borrador.
    /// Hereda tipo, referencias, área y contenido (<c>record with</c>); la rechazada queda intacta para la auditoría.
    /// </summary>
    private VersionMensaje(VersionMensaje rechazada, int numero, Guid preparadoPorId)
    {
        AvisoId = rechazada.AvisoId;
        CadenaId = rechazada.CadenaId;
        Numero = numero;
        Tipo = rechazada.Tipo;
        EstadoCap = rechazada.EstadoCap;
        Contenido = rechazada.Contenido with { };
        Area = rechazada.Area?.Copy();
        Remitente = rechazada.Remitente;
        ReferenciaVersionId = rechazada.ReferenciaVersionId;
        Referencias = rechazada.Referencias;
        VersionAnteriorId = rechazada.Id;
        MotivoDevolucion = rechazada.MotivoRechazo;
        CopiarProtocolo(rechazada);
        PreparadoPorId = preparadoPorId;
        IdentificadorCap = $"SAT-PIRAI-{CreadoEn:yyyyMMdd}-{Id.ToString("N")[..12]}";
    }

    /// <summary>Objeto del patrón State que corresponde al estado guardado.</summary>
    public EstadoVersionBase EstadoActual => EstadoVersionBase.De(Estado);
    public NivelAlerta Nivel => Contenido.Nivel;
    public bool EsPrueba => EstadoCap != EstadoCap.Actual;
    public bool EsVigenteEnCadena => ReemplazadaPorId is null && EstadoActual.PermiteDifundir;

    /// <summary>Valor de &lt;references&gt; que usa otro mensaje para referirse a este: "sender,identifier,sent".</summary>
    public string ReferenciaCap => $"{Remitente},{IdentificadorCap},{EnviadoCap:yyyy-MM-ddTHH:mm:sszzz}";

    /// <summary>
    /// Lo que le falta para pasar a revisión. Una versión incompleta queda como borrador (RF-02);
    /// sin instrucciones no se puede aprobar (RF-05).
    /// </summary>
    public IReadOnlyList<string> Faltantes(DateTimeOffset ahora)
    {
        var c = Contenido;
        var faltan = new List<string>();
        if (string.IsNullOrWhiteSpace(c.Titular) && string.IsNullOrWhiteSpace(c.Descripcion)) faltan.Add("Falta el titular o la descripción.");
        if (string.IsNullOrWhiteSpace(c.Instruccion)) faltan.Add("Faltan las instrucciones a la población.");
        // RF-05: las instrucciones de una alerta o actualización salen de un protocolo oficial con fuente y fecha.
        else if (Tipo != TipoMensajeCap.Cancel && ProtocoloId is null)
            faltan.Add("Falta vincular el protocolo oficial del que salen las instrucciones.");
        if (Area is null || Area.IsEmpty) faltan.Add("Falta el área afectada.");
        if (Tipo != TipoMensajeCap.Cancel)
        {
            if (c.Expira is null) faltan.Add("Falta la fecha de expiración.");
            else if (c.Expira <= ahora) faltan.Add($"El aviso venció el {c.Expira:yyyy-MM-dd HH:mm} UTC.");
        }
        return faltan;
    }

    // State: cada acción la decide el estado actual, que devuelve el estado siguiente o rechaza la acción.

    public void Redactar(string? titular, string? descripcion, string? instruccion)
    {
        EstadoActual.Editar(this);
        if (ProtocoloId is not null && (string.IsNullOrWhiteSpace(instruccion) ? null : instruccion.Trim()) != Contenido.Instruccion)
            throw new ExcepcionDominio($"Las instrucciones son las del protocolo «{ProtocoloTitulo}» y no se editan. " +
                "Para usar otro texto, elige otro protocolo o quita el vínculo.");
        Contenido = Contenido.Redactar(titular, descripcion, instruccion);
    }

    /// <summary>RF-05: toma las instrucciones de un protocolo oficial vigente y guarda de dónde salen.</summary>
    public void VincularProtocolo(ProtocoloOficial protocolo)
    {
        EstadoActual.Editar(this);
        if (Tipo == TipoMensajeCap.Cancel)
            throw new ExcepcionDominio("Un Cancel no lleva protocolo: sus instrucciones dicen que la alerta terminó, no qué hacer ante ella.");
        if (!protocolo.Activo) throw new ExcepcionDominio($"El protocolo «{protocolo.Titulo}» está desactivado.");
        ProtocoloId = protocolo.Id;
        ProtocoloTitulo = protocolo.Titulo;
        ProtocoloCita = protocolo.Cita;
        Contenido = Contenido.Redactar(Contenido.Titular, Contenido.Descripcion, protocolo.Instruccion);
    }

    /// <summary>Quita el vínculo: el texto queda, pero la versión no pasa a revisión hasta vincular otro protocolo.</summary>
    public void QuitarProtocolo()
    {
        EstadoActual.Editar(this);
        ProtocoloId = null;
        ProtocoloTitulo = null;
        ProtocoloCita = null;
    }

    private void CopiarProtocolo(VersionMensaje origen)
    {
        ProtocoloId = origen.ProtocoloId;
        ProtocoloTitulo = origen.ProtocoloTitulo;
        ProtocoloCita = origen.ProtocoloCita;
    }

    /// <summary>Fija el XML CAP generado (ya validado) y pasa a revisión. Si está incompleta, sigue en borrador.</summary>
    public void EnviarARevision(string xmlCap, DateTimeOffset enviado, DateTimeOffset ahora)
    {
        Estado = EstadoActual.EnviarARevision(this, ahora);
        XmlCap = xmlCap;
        EnviadoCap = enviado.ToUniversalTime();
        EnviadaARevisionEn = ahora;
    }

    /// <param name="referencia">La versión que este Update/Cancel referencia, para confirmar que sigue vigente.</param>
    public void Aprobar(Guid usuarioId, DateTimeOffset ahora, VersionMensaje? referencia)
    {
        // Separación de funciones (RF-11): nadie aprueba su propio mensaje, tampoco un administrador.
        if (usuarioId == PreparadoPorId)
            throw new ExcepcionDominio("Quien preparó la versión no puede aprobarla: la aprueba otra persona con permiso de aprobación.");
        if (ReferenciaVersionId is not null)
        {
            if (referencia is null || referencia.Id != ReferenciaVersionId)
                throw new ExcepcionDominio("La versión referenciada no existe.");
            VerificarReferencia(referencia, CadenaId);
        }
        Estado = EstadoActual.Aprobar(this, ahora);
        RevisadoPorId = usuarioId;
        RevisadoEn = ahora;
        MotivoRechazo = null;
        referencia?.MarcarReemplazada(Id);
    }

    public void Rechazar(Guid usuarioId, string? motivo, DateTimeOffset ahora)
    {
        motivo = motivo?.Trim();
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Length < LargoMinimoMotivo)
            throw new ExcepcionDominio($"Para rechazar hay que indicar el motivo (al menos {LargoMinimoMotivo} caracteres).");
        if (motivo.Length > 1000) throw new ExcepcionDominio("El motivo supera los 1000 caracteres.");
        Estado = EstadoActual.Rechazar(this);
        RevisadoPorId = usuarioId;
        RevisadoEn = ahora;
        MotivoRechazo = motivo;
    }

    /// <summary>Crea la versión que corrige a esta (que debe estar rechazada).</summary>
    public VersionMensaje Corregir(int numero, Guid usuarioId)
    {
        EstadoActual.Corregir(this);
        return new VersionMensaje(this, numero, usuarioId);
    }

    /// <summary>
    /// Comprueba que esta versión se puede eliminar: tiene que ser un borrador (lo decide su estado) y solo la elimina
    /// quien la preparó o un administrador.
    /// </summary>
    public void VerificarEliminable(Guid usuarioId, bool esAdministrador)
    {
        EstadoActual.Eliminar(this);
        if (!esAdministrador && PreparadoPorId != usuarioId)
            throw new UnauthorizedAccessException("Solo quien preparó el borrador o un administrador puede eliminarlo.");
    }

    /// <summary>Instrucción de un Cancel cuando el aviso oficial no trae la suya: nunca la de la alerta que cancela.</summary>
    public const string InstruccionCancelNeutra =
        "El aviso anterior queda cancelado. Siga las indicaciones de las autoridades de su comunidad y manténgase atento a nuevos avisos.";

    /// <summary>
    /// Contenido de un Update o Cancel preparado desde una versión aprobada. Un Update parte del texto aprobado (Prototype).
    /// Un Cancel no: decir "evacúe" en una cancelación confunde a la población, así que parte de un titular, una descripción
    /// y una instrucción de cierre. Lo que el operador redacte reemplaza a esos textos.
    /// </summary>
    public static ContenidoMensaje ContenidoDerivado(TipoMensajeCap tipo, ContenidoMensaje aprobado, string? titular, string? descripcion,
        string? instruccion) => tipo == TipoMensajeCap.Cancel
        ? aprobado.Redactar(titular ?? $"Fin del aviso: {aprobado.Evento}",
            descripcion ?? $"Se cancela el aviso anterior ({aprobado.Titular ?? aprobado.Evento}).",
            instruccion ?? InstruccionCancelNeutra)
        : aprobado.Redactar(titular ?? aprobado.Titular, descripcion ?? aprobado.Descripcion, instruccion ?? aprobado.Instruccion);

    /// <summary>
    /// RF-04 y RNF-08: por qué lo que quedó pendiente de esta versión ya no debe salir. Null si sigue valiendo:
    /// aprobada o difundida, no reemplazada por un Update o Cancel y vigente (un Cancel vale aunque haya vencido).
    /// </summary>
    public string? MotivoParaDetenerDifusion(DateTimeOffset ahora)
    {
        if (ReemplazadaPorId is not null) return $"La versión {Numero} fue reemplazada por un Update o Cancel: no se envía.";
        if (!EstadoActual.PermiteDifundir) return $"La versión {Numero} está {Estado}: no se envía.";
        if (Tipo != TipoMensajeCap.Cancel && Contenido.Expira is { } e && e <= ahora)
            return $"La versión {Numero} venció el {e.ToUniversalTime():yyyy-MM-dd HH:mm} UTC: no se envía.";
        return null;
    }

    public void MarcarDifundida(DateTimeOffset ahora)
    {
        Estado = EstadoActual.Difundir(this);
        DifundidaEn ??= ahora;
    }

    public void MarcarPublicadaWeb(DateTimeOffset ahora)
    {
        if (!EstadoActual.PermiteDifundir) throw new ExcepcionDominio($"La versión está {Estado}: solo se publica una versión aprobada.");
        PublicadaWebEn ??= ahora;
    }

    private void MarcarReemplazada(Guid nuevaId) => ReemplazadaPorId = nuevaId;

    /// <summary>
    /// Un Update o Cancel solo puede referirse a la versión vigente de su misma cadena, ya aprobada,
    /// que no sea un Cancel. Cualquier otra referencia es incoherente.
    /// </summary>
    public static void VerificarReferencia(VersionMensaje referencia, Guid cadenaId)
    {
        if (referencia.CadenaId != cadenaId)
            throw new ExcepcionDominio("Referencia incoherente: la versión referenciada es de otro aviso.");
        if (!referencia.EstadoActual.PermiteDifundir)
            throw new ExcepcionDominio($"Referencia incoherente: la versión {referencia.Numero} está {referencia.Estado}; solo se referencia una versión aprobada.");
        if (referencia.Tipo == TipoMensajeCap.Cancel)
            throw new ExcepcionDominio("Referencia incoherente: el mensaje ya fue cancelado.");
        if (referencia.ReemplazadaPorId is not null)
            throw new ExcepcionDominio($"Referencia incoherente: la versión {referencia.Numero} ya fue reemplazada por otra.");
    }
}
