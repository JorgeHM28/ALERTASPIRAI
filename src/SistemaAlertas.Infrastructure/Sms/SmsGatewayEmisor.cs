using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Infrastructure.Sms;

/// <summary>
/// Patrón Adapter. Adapta la API HTTP de "SMS Gateway for Android" (https://sms-gate.app, código abierto) a
/// <see cref="IEmisorSms"/>: un celular Android con la app instalada manda los SMS con su propio chip. Sirve donde no hay
/// un proveedor como Twilio (por ejemplo, Bolivia). Funciona en modo local (el celular en la misma red, http://IP:8080)
/// o en modo nube (https://api.sms-gate.app/3rdparty/v1). En los dos se envía POST {Url}/message con autenticación básica.
/// </summary>
public sealed class SmsGatewayEmisor(HttpClient http, OpcionesSmsGateway opciones) : IEmisorSms
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Proveedor => "SMS Gateway (Android)";
    public bool EnviaReal => true;

    public async Task<ResultadoEnvioSms> EnviarAsync(MensajeSms mensaje, CancellationToken ct = default)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, $"{opciones.Url.TrimEnd('/')}/message")
        {
            // Cuerpo con Content-Length (no "chunked"): el servidor de la app en el celular es pequeño.
            Content = new StringContent(JsonSerializer.Serialize(new { textMessage = new { text = mensaje.Texto }, phoneNumbers = new[] { mensaje.Destino } }, Json),
                Encoding.UTF8, "application/json")
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opciones.Usuario}:{opciones.Contrasena}")));
        try
        {
            using var respuesta = await http.SendAsync(solicitud, ct);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            if (respuesta.IsSuccessStatusCode)
                return ResultadoEnvioSms.Ok(LeerId(cuerpo) ?? $"GW-{(int)respuesta.StatusCode}");
            var error = $"SMS Gateway respondió {(int)respuesta.StatusCode} {respuesta.ReasonPhrase}: {Recortar(cuerpo)}";
            return EsRecuperable(respuesta.StatusCode) ? ResultadoEnvioSms.FalloRecuperable(error) : ResultadoEnvioSms.FalloDefinitivo(error);
        }
        catch (HttpRequestException ex)
        {
            // El celular apagado, sin red o fuera de la red local: pasajero.
            return ResultadoEnvioSms.FalloRecuperable($"No se pudo conectar con el celular de SMS Gateway: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return ResultadoEnvioSms.FalloRecuperable("El celular de SMS Gateway no respondió a tiempo.");
        }
    }

    /// <summary>429 y los 5xx son pasajeros. Un 4xx (credenciales, número inválido) no mejora reintentando.</summary>
    public static bool EsRecuperable(HttpStatusCode estado) => estado == HttpStatusCode.TooManyRequests || (int)estado >= 500;

    private static string? LeerId(string cuerpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static string Recortar(string texto) => texto.Length > 300 ? texto[..300] : texto;
}
