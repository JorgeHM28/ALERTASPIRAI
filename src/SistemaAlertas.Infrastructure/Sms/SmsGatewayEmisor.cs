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
/// Que el celular acepte el pedido no quiere decir que el SMS salió: después se consulta GET {Url}/message/{id} hasta que
/// el celular informe el resultado (Sent, Delivered o Failed). Un "Failed" del chip es fallo pasajero y la cola reintenta.
/// </summary>
public sealed class SmsGatewayEmisor(HttpClient http, OpcionesSmsGateway opciones,
    Func<TimeSpan, CancellationToken, Task>? esperar = null) : IEmisorSms
{
    private readonly Func<TimeSpan, CancellationToken, Task> _esperar = esperar ?? Task.Delay;

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
        solicitud.Headers.Authorization = Autenticacion();
        try
        {
            using var respuesta = await http.SendAsync(solicitud, ct);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            if (respuesta.IsSuccessStatusCode)
            {
                var id = LeerId(cuerpo);
                // Sin id no se puede consultar el resultado: se confía en la aceptación.
                if (id is null || !opciones.ConfirmarEnvio) return ResultadoEnvioSms.Ok(id ?? $"GW-{(int)respuesta.StatusCode}");
                return await ConfirmarAsync(id, ct);
            }
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

    /// <summary>
    /// Consulta el estado hasta que sea final. Failed: fallo pasajero con el error del destinatario (la cola reintenta
    /// a los 60 s). Delivered: enviado. Sent: enviado si sigue así pasado el margen. Sin resultado al vencer el tiempo:
    /// fallo pasajero.
    /// </summary>
    private async Task<ResultadoEnvioSms> ConfirmarAsync(string id, CancellationToken ct)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(1, opciones.IntervaloConsultaSegundos));
        var limite = TimeSpan.FromSeconds(Math.Max(1, opciones.TiempoConfirmacionSegundos));
        var margen = TimeSpan.FromSeconds(Math.Max(0, opciones.MargenTrasEnviadoSegundos));
        TimeSpan transcurrido = TimeSpan.Zero;
        TimeSpan? enviadoDesde = null;
        string? ultimoEstado = null;
        while (true)
        {
            var (estado, error) = await ConsultarAsync(id, ct);
            ultimoEstado = estado ?? ultimoEstado;
            switch (estado)
            {
                case "Delivered":
                    return ResultadoEnvioSms.Ok(id);
                case "Failed":
                    return ResultadoEnvioSms.FalloRecuperable($"El celular no pudo enviar el SMS: {error ?? "sin detalle"}");
                case "Sent":
                    enviadoDesde ??= transcurrido;
                    if (transcurrido - enviadoDesde >= margen) return ResultadoEnvioSms.Ok(id);
                    break;
            }
            if (transcurrido >= limite)
                return ultimoEstado == "Sent"
                    ? ResultadoEnvioSms.Ok(id)
                    : ResultadoEnvioSms.FalloRecuperable($"El celular no confirmó el envío a tiempo (último estado: {ultimoEstado ?? "sin respuesta"}).");
            await _esperar(intervalo, ct);
            transcurrido += intervalo;
        }
    }

    /// <summary>Estado del mensaje y, si falló, el error del primer destinatario. Null si la consulta no respondió.</summary>
    private async Task<(string? Estado, string? Error)> ConsultarAsync(string id, CancellationToken ct)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"{opciones.Url.TrimEnd('/')}/message/{Uri.EscapeDataString(id)}");
        solicitud.Headers.Authorization = Autenticacion();
        try
        {
            using var respuesta = await http.SendAsync(solicitud, ct);
            if (!respuesta.IsSuccessStatusCode) return (null, null);
            using var doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
            var raiz = doc.RootElement;
            var estado = raiz.TryGetProperty("state", out var e) ? e.GetString() : null;
            string? error = null;
            if (raiz.TryGetProperty("recipients", out var destinatarios) && destinatarios.ValueKind == JsonValueKind.Array)
                foreach (var d in destinatarios.EnumerateArray())
                    if (d.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String) { error = err.GetString(); break; }
            return (estado, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Una consulta que falla no decide nada: se vuelve a consultar hasta el tiempo límite.
            return (null, null);
        }
    }

    private AuthenticationHeaderValue Autenticacion() => new("Basic",
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opciones.Usuario}:{opciones.Contrasena}")));

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
