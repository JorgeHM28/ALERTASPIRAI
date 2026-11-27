using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Application.Difusion.Canales;

/// <summary>Lo que la cola le pasa a un canal automático para un intento de envío.</summary>
/// <param name="Destino">Teléfono, id del grupo de WhatsApp o correo.</param>
/// <param name="Asunto">Solo en el correo.</param>
public sealed record MensajeCanal(string Destino, string Texto, string? Asunto, Guid AvisoId, bool EsPrueba);

/// <summary>
/// Un canal automático para la cola (SMS, WhatsApp, Email). Cada uno adapta su proveedor (Adapter) y devuelve el
/// resultado en los mismos términos: enviado, fallo pasajero (se reintenta) o fallo definitivo.
/// </summary>
public interface IEmisorCanal
{
    CanalDifusion Canal { get; }
    string Proveedor { get; }
    /// <summary>True si el mensaje llega a personas reales: un aviso de prueba o simulacro no sale por él.</summary>
    bool EnviaReal { get; }
    Task<ResultadoEnvioSms> EnviarAsync(MensajeCanal mensaje, CancellationToken ct = default);
}

/// <summary>El SMS como canal de la cola: usa el emisor SMS con sus decoradores (bitácora y modo prueba).</summary>
public sealed class CanalSms(IEmisorSms emisor) : IEmisorCanal
{
    public CanalDifusion Canal => CanalDifusion.Sms;
    public string Proveedor => emisor.Proveedor;
    public bool EnviaReal => emisor.EnviaReal;

    public Task<ResultadoEnvioSms> EnviarAsync(MensajeCanal m, CancellationToken ct = default) =>
        emisor.EnviarAsync(new MensajeSms(m.Destino, m.Texto, m.AvisoId, m.EsPrueba), ct);
}

/// <summary>Un grupo de WhatsApp que recibe las alertas.</summary>
public sealed class GrupoWhatsApp
{
    public string Nombre { get; set; } = string.Empty;
    /// <summary>Id del grupo en WAHA, por ejemplo 120363012345678901@g.us.</summary>
    public string Id { get; set; } = string.Empty;
}

/// <summary>Sección "WhatsApp" de la configuración (WAHA). Sin URL, clave o grupos, el canal queda apagado.</summary>
public sealed class OpcionesWhatsApp
{
    public const string Seccion = "WhatsApp";
    /// <summary>Dirección de WAHA, por ejemplo http://localhost:3000 (o http://waha:3000 dentro de Docker).</summary>
    public string Url { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Sesion { get; set; } = "default";
    public List<GrupoWhatsApp> Grupos { get; set; } = [];
    public int TiempoEsperaSegundos { get; set; } = 20;

    public IReadOnlyList<GrupoWhatsApp> GruposValidos => Grupos.Where(g => !string.IsNullOrWhiteSpace(g.Id)).ToList();
    public bool Habilitado => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(ApiKey) && GruposValidos.Count > 0;
}

/// <summary>Sección "Email" de la configuración (SMTP). Sin servidor, usuario, contraseña o remitente, el canal queda apagado.</summary>
public sealed class OpcionesEmail
{
    public const string Seccion = "Email";
    public string Servidor { get; set; } = string.Empty;
    public int Puerto { get; set; } = 587;
    public bool UsarSsl { get; set; } = true;
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    /// <summary>Dirección que figura como remitente (con Gmail, la misma cuenta).</summary>
    public string Remitente { get; set; } = string.Empty;
    public string NombreRemitente { get; set; } = "SAT Río Piraí";

    public bool Habilitado => !string.IsNullOrWhiteSpace(Servidor) && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Contrasena) && !string.IsNullOrWhiteSpace(Remitente);
}
