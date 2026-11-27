using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Difusion;

/// <summary>
/// Canales de difusión de RF-04. SMS, WhatsApp (a grupos) y Email son automáticos; radio, altavoz y redes los ejecuta
/// una persona.
/// </summary>
public enum CanalDifusion { Sms, Radio, Altavoz, RedesSociales, WhatsApp, Email }

/// <summary>
/// Estado de un trabajo de difusión.
/// SMS: Pendiente → Enviado | Fallido | Bloqueado. Tarea manual: Pendiente → Confirmada | NoRealizada.
/// Cualquiera de los dos: Pendiente → Cancelado, cuando su versión fue reemplazada por un Update o Cancel, o venció.
/// </summary>
public enum EstadoTrabajo { Pendiente, Enviado, Fallido, Bloqueado, Confirmada, NoRealizada, Cancelado }

/// <summary>
/// Trabajo de difusión (RF-04, RF-08): lo que hay que hacer por un canal para una versión aprobada.
/// Se especializa en <see cref="EnvioAutomatico"/> (SMS, WhatsApp, Email) y <see cref="TareaManual"/> (herencia disjunta y total del diagrama ER).
/// </summary>
public abstract class TrabajoDifusion : Entidad
{
    public Guid VersionId { get; protected set; }
    public Guid AvisoId { get; protected set; }
    /// <summary>Comunidad a la que va dirigido. Null en las tareas que valen para toda el área (radio, redes).</summary>
    public Guid? ComunidadId { get; protected set; }
    public CanalDifusion Canal { get; protected set; }
    public EstadoTrabajo Estado { get; protected set; } = EstadoTrabajo.Pendiente;
    public string? Resultado { get; protected set; }
    public DateTimeOffset? ResultadoEn { get; protected set; }
    /// <summary>Aviso de prueba o simulacro.</summary>
    public bool EsPrueba { get; protected set; }

    public bool Terminado => Estado != EstadoTrabajo.Pendiente;

    protected TrabajoDifusion() { }

    protected TrabajoDifusion(Guid versionId, Guid avisoId, Guid? comunidadId, CanalDifusion canal, bool esPrueba, DateTimeOffset ahora)
    {
        if (versionId == Guid.Empty) throw new ExcepcionDominio("El trabajo de difusión necesita la versión que difunde.");
        VersionId = versionId;
        AvisoId = avisoId;
        ComunidadId = comunidadId;
        Canal = canal;
        EsPrueba = esPrueba;
        CreadoEn = ahora;
    }

    /// <summary>
    /// RF-04 y RNF-08: un trabajo pendiente de una versión que ya no vale (reemplazada o vencida) no debe salir.
    /// Se cierra como Cancelado con el motivo. Devuelve false si ya había terminado (lo hecho no se deshace).
    /// </summary>
    public virtual bool Cancelar(string motivo, DateTimeOffset ahora)
    {
        if (Terminado) return false;
        Terminar(EstadoTrabajo.Cancelado, motivo, ahora);
        return true;
    }

    protected void Terminar(EstadoTrabajo estado, string resultado, DateTimeOffset ahora)
    {
        Estado = estado;
        Resultado = resultado;
        ResultadoEn = ahora;
    }

    protected void VerificarPendiente()
    {
        if (Terminado) throw new ExcepcionDominio($"El trabajo ya terminó ({Estado}).");
    }
}

/// <summary>
/// Un envío que hace el sistema solo (SMS, WhatsApp o Email). Lo manda la cola en segundo plano. RNF-08: ante un fallo
/// recuperable se reintenta como máximo 2 veces más, esperando 60 s entre intentos; un fallo definitivo no se reintenta.
/// </summary>
public abstract class EnvioAutomatico : TrabajoDifusion
{
    public const int MaxReintentos = 2;
    public static readonly TimeSpan EsperaReintento = TimeSpan.FromSeconds(60);

    /// <summary>Contacto que recibe. Null en un envío a un grupo de WhatsApp.</summary>
    public Guid? ContactoId { get; protected set; }
    /// <summary>Teléfono, id del grupo de WhatsApp o correo. Solo lo usa el sistema; hacia afuera se muestra enmascarado.</summary>
    public string Destino { get; protected set; } = null!;
    /// <summary>Nombre legible del destino, por ejemplo el nombre del grupo de WhatsApp.</summary>
    public string? NombreDestino { get; protected set; }
    /// <summary>Asunto del correo. Null en SMS y WhatsApp.</summary>
    public string? Asunto { get; protected set; }
    public string Texto { get; protected set; } = null!;
    public int Intentos { get; private set; }
    public string? CodigoProveedor { get; private set; }
    public string? UltimoError { get; private set; }
    /// <summary>Cuándo toca el próximo intento. Null cuando el envío terminó.</summary>
    public DateTimeOffset? ProximoIntento { get; private set; }

    protected EnvioAutomatico() { }

    protected EnvioAutomatico(Guid versionId, Guid avisoId, Guid? comunidadId, CanalDifusion canal, Guid? contactoId, string destino,
        string texto, bool esPrueba, DateTimeOffset ahora) : base(versionId, avisoId, comunidadId, canal, esPrueba, ahora)
    {
        if (string.IsNullOrWhiteSpace(destino)) throw new ExcepcionDominio($"El envío por {canal} necesita un destino.");
        if (string.IsNullOrWhiteSpace(texto)) throw new ExcepcionDominio($"El envío por {canal} necesita un texto.");
        ContactoId = contactoId;
        Destino = destino.Trim();
        Texto = texto;
        ProximoIntento = ahora;
    }

    public override bool Cancelar(string motivo, DateTimeOffset ahora)
    {
        if (!base.Cancelar(motivo, ahora)) return false;
        ProximoIntento = null;
        return true;
    }

    /// <summary>Registra el resultado de un intento y decide si toca reintentar (RNF-08).</summary>
    public void RegistrarIntento(ResultadoIntento r, DateTimeOffset ahora, int maxReintentos = MaxReintentos, TimeSpan? espera = null)
    {
        VerificarPendiente();
        Intentos++;
        if (r.Exitoso)
        {
            CodigoProveedor = r.CodigoProveedor;
            ProximoIntento = null;
            Terminar(EstadoTrabajo.Enviado, Intentos == 1 ? "Enviado." : $"Enviado en el intento {Intentos}.", ahora);
            return;
        }
        UltimoError = r.Error;
        if (r.Bloqueado)
        {
            ProximoIntento = null;
            Terminar(EstadoTrabajo.Bloqueado, r.Error ?? "Bloqueado.", ahora);
        }
        else if (r.Recuperable && Intentos <= Math.Clamp(maxReintentos, 0, MaxReintentos))
        {
            ProximoIntento = ahora + (espera ?? EsperaReintento);
        }
        else
        {
            ProximoIntento = null;
            Terminar(EstadoTrabajo.Fallido, r.Recuperable
                ? $"Falló después de {Intentos} intentos: {r.Error}"
                : $"Fallo definitivo, no se reintenta: {r.Error}", ahora);
        }
    }
}

/// <summary>Un SMS a un contacto (RF-04).</summary>
public class EnvioSms : EnvioAutomatico
{
    private EnvioSms() { }

    public EnvioSms(Guid versionId, Guid avisoId, Guid? comunidadId, Guid contactoId, string telefono, string texto, bool esPrueba,
        DateTimeOffset ahora) : base(versionId, avisoId, comunidadId, CanalDifusion.Sms, contactoId, telefono, texto, esPrueba, ahora) { }
}

/// <summary>
/// Un mensaje a un grupo de WhatsApp (RF-04). Un solo envío por grupo en vez de uno por número: los vecinos se suman al
/// grupo de su comunidad o municipio. Lleva el texto completo, con el nivel en palabras.
/// </summary>
public class EnvioWhatsApp : EnvioAutomatico
{
    private EnvioWhatsApp() { }

    public EnvioWhatsApp(Guid versionId, Guid avisoId, string grupoId, string? nombreGrupo, string texto, bool esPrueba, DateTimeOffset ahora)
        : base(versionId, avisoId, null, CanalDifusion.WhatsApp, null, grupoId, texto, esPrueba, ahora)
    {
        NombreDestino = string.IsNullOrWhiteSpace(nombreGrupo) ? null : nombreGrupo.Trim();
    }
}

/// <summary>Un correo a un contacto (RF-04).</summary>
public class EnvioEmail : EnvioAutomatico
{
    private EnvioEmail() { }

    public EnvioEmail(Guid versionId, Guid avisoId, Guid? comunidadId, Guid contactoId, string correo, string asunto, string texto,
        bool esPrueba, DateTimeOffset ahora)
        : base(versionId, avisoId, comunidadId, CanalDifusion.Email, contactoId, correo, texto, esPrueba, ahora)
    {
        if (string.IsNullOrWhiteSpace(asunto)) throw new ExcepcionDominio("El correo necesita un asunto.");
        Asunto = asunto.Trim();
    }
}

/// <summary>Resultado de un intento de envío, tal como lo informa el proveedor.</summary>
public sealed record ResultadoIntento(bool Exitoso, string? CodigoProveedor, string? Error, bool Recuperable, bool Bloqueado);

/// <summary>
/// Tarea para un canal que ejecuta una persona (radio, altavoz, redes sociales). Lleva el texto listo para leer
/// o publicar. Se confirma con quién la hizo, o se marca como no realizada con el motivo. No se reintenta.
/// </summary>
public class TareaManual : TrabajoDifusion
{
    public string TextoLegible { get; private set; } = null!;
    public Guid? ConfirmadoPorId { get; private set; }
    public string? Observacion { get; private set; }

    private TareaManual() { }

    public TareaManual(Guid versionId, Guid avisoId, Guid? comunidadId, CanalDifusion canal, string textoLegible, bool esPrueba,
        DateTimeOffset ahora) : base(versionId, avisoId, comunidadId, canal, esPrueba, ahora)
    {
        if (canal is CanalDifusion.Sms or CanalDifusion.WhatsApp or CanalDifusion.Email)
            throw new ExcepcionDominio($"{canal} es un envío automático, no una tarea manual.");
        if (string.IsNullOrWhiteSpace(textoLegible)) throw new ExcepcionDominio("La tarea necesita el texto a difundir.");
        TextoLegible = textoLegible;
    }

    public void Confirmar(Guid usuarioId, string? observacion, DateTimeOffset ahora)
    {
        VerificarPendiente();
        ConfirmadoPorId = usuarioId;
        Observacion = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim();
        Terminar(EstadoTrabajo.Confirmada, "Realizada.", ahora);
    }

    public void MarcarNoRealizada(Guid usuarioId, string? motivo, DateTimeOffset ahora)
    {
        VerificarPendiente();
        if (string.IsNullOrWhiteSpace(motivo)) throw new ExcepcionDominio("Indica por qué no se pudo realizar la tarea.");
        ConfirmadoPorId = usuarioId;
        Observacion = motivo.Trim();
        Terminar(EstadoTrabajo.NoRealizada, "No realizada.", ahora);
    }
}

/// <summary>
/// CAP emitido (RF-04): el XML CAP 1.2 final de una versión aprobada, archivado tal cual salió, con su huella SHA-256
/// y el resultado de la validación contra el XSD. Un Update o Cancel queda enlazado al CAP emitido de la versión que
/// reemplaza. Si esa versión se aprobó pero nunca se difundió, no hay CAP previo que enlazar: el enlace queda vacío
/// y el XML conserva igual su &lt;references&gt;.
/// </summary>
public class CapEmitido : Entidad
{
    public Guid VersionId { get; private set; }
    public Guid AvisoId { get; private set; }
    public string Identificador { get; private set; } = null!;
    public string Remitente { get; private set; } = null!;
    public DateTimeOffset Enviado { get; private set; }
    /// <summary>status del CAP (Actual, Exercise, Test...).</summary>
    public string Estado { get; private set; } = null!;
    /// <summary>msgType del CAP (Alert, Update, Cancel).</summary>
    public string Tipo { get; private set; } = null!;
    public string Xml { get; private set; } = null!;
    public string Sha256 { get; private set; } = null!;
    public bool Valido { get; private set; }
    public Guid? ReferenciaCapId { get; private set; }

    private CapEmitido() { }

    public CapEmitido(Guid versionId, Guid avisoId, string identificador, string remitente, DateTimeOffset enviado, string estado,
        string tipo, string xml, string sha256, bool valido, Guid? referenciaCapId, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(xml)) throw new ExcepcionDominio("No hay XML CAP para emitir.");
        if (!valido) throw new ExcepcionDominio("Solo se emite un CAP válido.");
        if (tipo == "Alert" && referenciaCapId is not null)
            throw new ExcepcionDominio("Un Alert no referencia a otro CAP.");
        VersionId = versionId;
        AvisoId = avisoId;
        Identificador = identificador;
        Remitente = remitente;
        Enviado = enviado;
        Estado = estado;
        Tipo = tipo;
        Xml = xml;
        Sha256 = sha256;
        Valido = valido;
        ReferenciaCapId = referenciaCapId;
        CreadoEn = ahora;
    }
}

/// <summary>Evento de la bitácora de un aviso (RNF-04): quién hizo qué y cuándo. Se guarda en la base.</summary>
public class RegistroEventoAviso : Entidad
{
    public Guid AvisoId { get; private set; }
    public Guid? VersionId { get; private set; }
    public string Tipo { get; private set; } = null!;
    public Guid? UsuarioId { get; private set; }
    public string Detalle { get; private set; } = null!;
    public DateTimeOffset Fecha { get; private set; }

    private RegistroEventoAviso() { }

    public RegistroEventoAviso(Guid avisoId, Guid? versionId, string tipo, Guid? usuarioId, string detalle, DateTimeOffset fecha)
    {
        AvisoId = avisoId;
        VersionId = versionId;
        Tipo = tipo;
        UsuarioId = usuarioId;
        Detalle = detalle.Length > 1000 ? detalle[..1000] : detalle;
        Fecha = fecha;
        CreadoEn = fecha;
    }
}
