using System.Net;
using System.Text.Json;
using SistemaAlertas.Application.Difusion.Canales;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Infrastructure.Canales;

/// <summary>
/// Patrón Adapter. Adapta la API HTTP de WAHA (WhatsApp HTTP API, https://waha.devlike.pro) a <see cref="IEmisorCanal"/>.
/// WAHA mantiene una sesión de WhatsApp Web vinculada a un número (se escanea un QR) y permite escribir a un grupo:
/// POST {Url}/api/sendText con { session, chatId, text } y la cabecera X-Api-Key.
/// Es una vía no oficial: para la demo conviene un número de prueba.
/// </summary>
public sealed class WahaEmisor(HttpClient http, OpcionesWhatsApp opciones) : IEmisorCanal
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CanalDifusion Canal => CanalDifusion.WhatsApp;
    public string Proveedor => "WhatsApp (WAHA)";
    public bool EnviaReal => true;

    public async Task<ResultadoEnvioSms> EnviarAsync(MensajeCanal mensaje, CancellationToken ct = default)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, $"{opciones.Url.TrimEnd('/')}/api/sendText")
        {
            // Cuerpo con Content-Length (no "chunked"): algunos servidores pequeños no aceptan envíos por partes.
            Content = new StringContent(JsonSerializer.Serialize(new { session = opciones.Sesion, chatId = mensaje.Destino, text = mensaje.Texto }, Json),
                System.Text.Encoding.UTF8, "application/json")
        };
        solicitud.Headers.Add("X-Api-Key", opciones.ApiKey);
        try
        {
            using var respuesta = await http.SendAsync(solicitud, ct);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            if (respuesta.IsSuccessStatusCode) return ResultadoEnvioSms.Ok(LeerId(cuerpo) ?? $"WA-{(int)respuesta.StatusCode}");
            var error = $"WAHA respondió {(int)respuesta.StatusCode} {respuesta.ReasonPhrase}: {(cuerpo.Length > 300 ? cuerpo[..300] : cuerpo)}";
            return EsRecuperable(respuesta.StatusCode) ? ResultadoEnvioSms.FalloRecuperable(error) : ResultadoEnvioSms.FalloDefinitivo(error);
        }
        catch (HttpRequestException ex)
        {
            return ResultadoEnvioSms.FalloRecuperable($"No se pudo conectar con WAHA: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return ResultadoEnvioSms.FalloRecuperable("WAHA no respondió a tiempo.");
        }
    }

    /// <summary>
    /// 429 y los 5xx son pasajeros (también cuando la sesión de WhatsApp se está reconectando). Un 4xx (clave incorrecta,
    /// grupo inexistente) no mejora reintentando.
    /// </summary>
    public static bool EsRecuperable(HttpStatusCode estado) => estado == HttpStatusCode.TooManyRequests || (int)estado >= 500;

    /// <summary>El id del mensaje: según el motor de WAHA viene como texto o como objeto con "_serialized".</summary>
    private static string? LeerId(string cuerpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("id", out var id)) return null;
            return id.ValueKind switch
            {
                JsonValueKind.String => id.GetString(),
                JsonValueKind.Object when id.TryGetProperty("_serialized", out var s) => s.GetString(),
                _ => null
            };
        }
        catch (JsonException) { return null; }
    }
}
