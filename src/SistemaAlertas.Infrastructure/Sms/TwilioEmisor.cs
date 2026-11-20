using SistemaAlertas.Application.Difusion.Sms;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SistemaAlertas.Infrastructure.Sms;

/// <summary>
/// Patrón Adapter. Adapta el SDK de Twilio (el adaptado: <see cref="MessageResource"/>, <see cref="PhoneNumber"/>,
/// <see cref="ApiException"/>) a <see cref="IEmisorSms"/> (el objetivo). El resto del sistema no ve ningún tipo de Twilio:
/// recibe un <see cref="ResultadoEnvioSms"/> y sabe si el fallo es recuperable sin conocer los códigos HTTP del proveedor.
/// </summary>
public sealed class TwilioEmisor(ITwilioRestClient cliente, OpcionesTwilio opciones) : IEmisorSms
{
    private static readonly string[] EstadosFallidos = ["failed", "undelivered", "canceled"];

    public string Proveedor => "Twilio";
    public bool EnviaReal => true;

    public async Task<ResultadoEnvioSms> EnviarAsync(MensajeSms mensaje, CancellationToken ct = default)
    {
        var solicitud = new CreateMessageOptions(new PhoneNumber(mensaje.Destino))
        {
            From = new PhoneNumber(opciones.NumeroOrigen),
            Body = mensaje.Texto
        };
        try
        {
            var respuesta = await MessageResource.CreateAsync(solicitud, cliente);
            var estado = respuesta.Status?.ToString();
            return estado is not null && EstadosFallidos.Contains(estado)
                ? ResultadoEnvioSms.FalloDefinitivo($"Twilio rechazó el mensaje ({estado}, código {respuesta.ErrorCode}): {respuesta.ErrorMessage}")
                : ResultadoEnvioSms.Ok(respuesta.Sid);
        }
        catch (ApiException ex)
        {
            var error = $"Twilio respondió {ex.Status} (código {ex.Code}): {ex.Message}";
            return EsRecuperable(ex.Status) ? ResultadoEnvioSms.FalloRecuperable(error) : ResultadoEnvioSms.FalloDefinitivo(error);
        }
        catch (ApiConnectionException ex)
        {
            return ResultadoEnvioSms.FalloRecuperable($"No se pudo conectar con Twilio: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return ResultadoEnvioSms.FalloRecuperable($"Error de red con Twilio: {ex.Message}");
        }
    }

    /// <summary>429 (demasiadas solicitudes) y los 5xx son pasajeros; un 4xx (número inválido, sin saldo) no mejora reintentando.</summary>
    public static bool EsRecuperable(int estadoHttp) => estadoHttp == 429 || estadoHttp >= 500;
}
