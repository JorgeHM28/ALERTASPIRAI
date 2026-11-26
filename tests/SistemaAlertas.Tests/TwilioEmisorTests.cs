using System.Net;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Infrastructure.Sms;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Http;

namespace SistemaAlertas.Tests;

/// <summary>Adapter: el SDK de Twilio adaptado a IEmisorSms, probado con un cliente HTTP de Twilio simulado (sin red).</summary>
public class TwilioEmisorTests
{
    private static readonly OpcionesTwilio Opciones = new() { AccountSid = "ACprueba", AuthToken = "x", NumeroOrigen = "+15005550006" };

    /// <summary>Sustituye al cliente REST de Twilio: guarda la solicitud y devuelve una respuesta fija o lanza una excepción.</summary>
    private sealed class ClienteTwilioFalso(Func<Request, Response> responder) : ITwilioRestClient
    {
        public Request? Ultima { get; private set; }
        public string AccountSid => "ACprueba";
        public string Region => null!;
        public Twilio.Http.HttpClient HttpClient => null!;
        public Response Request(Request request) { Ultima = request; return responder(request); }
        public Task<Response> RequestAsync(Request request) => Task.FromResult(Request(request));
    }

    private static Response Json(string json) => new(HttpStatusCode.Created, json, null);

    private static ApiException ErrorTwilio(int estado, int codigo) => new(codigo, estado, "error de Twilio", "", null, null);

    [Fact]
    public async Task Traduce_el_mensaje_a_la_solicitud_de_twilio_y_devuelve_el_sid()
    {
        var cliente = new ClienteTwilioFalso(_ => Json("""{"sid":"SM123","status":"queued"}"""));
        IEmisorSms emisor = new TwilioEmisor(cliente, Opciones);

        var r = await emisor.EnviarAsync(new MensajeSms("+59171234567", "Alerta roja río Piraí"));

        Assert.True(r.Exitoso);
        Assert.Equal("SM123", r.IdProveedor);
        var parametros = cliente.Ultima!.PostParams.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("+59171234567", parametros["To"]);
        Assert.Equal("+15005550006", parametros["From"]);
        Assert.Equal("Alerta roja río Piraí", parametros["Body"]);
        Assert.Contains("/Accounts/ACprueba/Messages.json", cliente.Ultima.ConstructUrl().ToString());
    }

    [Theory]
    [InlineData(429, 20429, true)]
    [InlineData(503, 0, true)]
    [InlineData(400, 21211, false)] // número inválido
    [InlineData(401, 20003, false)] // credenciales
    public async Task Clasifica_los_errores_de_twilio_en_recuperables_o_definitivos(int estado, int codigo, bool recuperable)
    {
        var emisor = new TwilioEmisor(new ClienteTwilioFalso(_ => throw ErrorTwilio(estado, codigo)), Opciones);

        var r = await emisor.EnviarAsync(new MensajeSms("+59171234567", "x"));

        Assert.False(r.Exitoso);
        Assert.Equal(recuperable, r.Recuperable);
        Assert.Contains(codigo.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Error);
    }

    [Fact]
    public async Task Un_fallo_de_conexion_es_recuperable()
    {
        var emisor = new TwilioEmisor(new ClienteTwilioFalso(_ => throw new ApiConnectionException("timeout")), Opciones);
        var r = await emisor.EnviarAsync(new MensajeSms("+59171234567", "x"));
        Assert.True(r.Recuperable);
    }

    [Fact]
    public async Task Un_mensaje_con_estado_failed_es_un_fallo_definitivo()
    {
        var emisor = new TwilioEmisor(new ClienteTwilioFalso(_ => Json(
            """{"sid":"SM9","status":"failed","error_code":30008,"error_message":"Unknown error"}""")), Opciones);
        var r = await emisor.EnviarAsync(new MensajeSms("+59171234567", "x"));
        Assert.False(r.Exitoso);
        Assert.False(r.Recuperable);
    }

    [Fact]
    public void El_modo_por_defecto_es_el_simulador()
    {
        Assert.Equal(ModoSms.Simulador, new OpcionesSms().Modo);
    }
}
