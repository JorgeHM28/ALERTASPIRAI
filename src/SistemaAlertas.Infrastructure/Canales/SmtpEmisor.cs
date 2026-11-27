using System.Net;
using System.Net.Mail;
using SistemaAlertas.Application.Difusion.Canales;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Infrastructure.Canales;

/// <summary>Lo único que el emisor necesita del cliente SMTP (permite probarlo sin un servidor de correo).</summary>
public interface IClienteSmtp
{
    Task EnviarAsync(MailMessage correo, CancellationToken ct);
}

/// <summary>Cliente SMTP de .NET con TLS y usuario y contraseña (por ejemplo, Gmail con una contraseña de aplicación).</summary>
public sealed class ClienteSmtp(OpcionesEmail opciones) : IClienteSmtp
{
    public async Task EnviarAsync(MailMessage correo, CancellationToken ct)
    {
        using var cliente = new SmtpClient(opciones.Servidor, opciones.Puerto)
        {
            EnableSsl = opciones.UsarSsl,
            Credentials = new NetworkCredential(opciones.Usuario, opciones.Contrasena),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 20_000
        };
        await cliente.SendMailAsync(correo, ct);
    }
}

/// <summary>
/// Patrón Adapter. Adapta el envío por SMTP (System.Net.Mail) a <see cref="IEmisorCanal"/>. Traduce los errores del
/// servidor de correo: un buzón ocupado o un servidor no disponible son pasajeros; una dirección inválida o un
/// rechazo de credenciales son definitivos.
/// </summary>
public sealed class SmtpEmisor(IClienteSmtp cliente, OpcionesEmail opciones) : IEmisorCanal
{
    private static readonly SmtpStatusCode[] Pasajeros =
    [
        SmtpStatusCode.ServiceNotAvailable, SmtpStatusCode.MailboxBusy, SmtpStatusCode.LocalErrorInProcessing,
        SmtpStatusCode.InsufficientStorage, SmtpStatusCode.GeneralFailure
    ];

    public CanalDifusion Canal => CanalDifusion.Email;
    public string Proveedor => $"Email ({opciones.Servidor})";
    public bool EnviaReal => true;

    public async Task<ResultadoEnvioSms> EnviarAsync(MensajeCanal mensaje, CancellationToken ct = default)
    {
        MailMessage correo;
        try
        {
            correo = new MailMessage(new MailAddress(opciones.Remitente, opciones.NombreRemitente), new MailAddress(mensaje.Destino))
            {
                Subject = mensaje.Asunto ?? "Aviso del SAT Río Piraí",
                Body = mensaje.Texto,
                IsBodyHtml = false
            };
        }
        catch (FormatException ex)
        {
            return ResultadoEnvioSms.FalloDefinitivo($"Dirección de correo no válida: {ex.Message}");
        }

        using (correo)
        {
            try
            {
                await cliente.EnviarAsync(correo, ct);
                return ResultadoEnvioSms.Ok(correo.Headers["Message-ID"] ?? $"MAIL-{Guid.NewGuid():N}");
            }
            catch (SmtpFailedRecipientException ex) when (!Pasajeros.Contains(ex.StatusCode))
            {
                return ResultadoEnvioSms.FalloDefinitivo($"El servidor rechazó el destinatario ({(int)ex.StatusCode}): {ex.Message}");
            }
            catch (SmtpException ex) when (Pasajeros.Contains(ex.StatusCode))
            {
                return ResultadoEnvioSms.FalloRecuperable($"Servidor de correo no disponible ({(int)ex.StatusCode}): {ex.Message}");
            }
            catch (SmtpException ex) when (ex.InnerException is IOException or System.Net.Sockets.SocketException)
            {
                return ResultadoEnvioSms.FalloRecuperable($"No se pudo conectar con el servidor de correo: {ex.Message}");
            }
            catch (SmtpException ex)
            {
                return ResultadoEnvioSms.FalloDefinitivo($"El servidor de correo rechazó el envío ({(int)ex.StatusCode}): {ex.Message}");
            }
        }
    }
}
