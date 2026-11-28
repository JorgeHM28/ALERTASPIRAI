using System.Net;
using System.Text;
using System.Text.Json;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Infrastructure.Sms;
using SistemaAlertas.Infrastructure.Sms.Decoradores;

namespace SistemaAlertas.Tests;

/// <summary>Adapter de SMS Gateway for Android: arma la petición que espera la app y traduce sus respuestas.</summary>
public class SmsGatewayEmisorTests
{
    private sealed class CelularFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Ultima { get; private set; }
        public string? Cuerpo { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Ultima = request;
            Cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return responder(request);
        }
    }

    // Estas pruebas miran el pedido de envío; la confirmación del resultado se prueba más abajo.
    private static readonly OpcionesSmsGateway Opciones = new() { Url = "http://192.168.1.50:8080/", Usuario = "sms", Contrasena = "clave123", ConfirmarEnvio = false };

    private static (SmsGatewayEmisor Emisor, CelularFalso Celular) Armar(HttpStatusCode estado, string cuerpo = "")
    {
        var celular = new CelularFalso(_ => new HttpResponseMessage(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") });
        return (new SmsGatewayEmisor(new HttpClient(celular), Opciones), celular);
    }

    [Fact]
    public async Task Envia_el_texto_al_numero_con_autenticacion_basica()
    {
        var (emisor, celular) = Armar(HttpStatusCode.Accepted, """{"id":"PyDmBQZZXYmyxMwED8Fzy","state":"Pending"}""");

        var r = await emisor.EnviarAsync(new MensajeSms("+59171234567", "ALERTA ROJA: evacúe."));

        Assert.True(r.Exitoso);
        Assert.Equal("PyDmBQZZXYmyxMwED8Fzy", r.IdProveedor);
        Assert.Equal(HttpMethod.Post, celular.Ultima!.Method);
        Assert.Equal("http://192.168.1.50:8080/message", celular.Ultima.RequestUri!.ToString());
        Assert.Equal("Basic", celular.Ultima.Headers.Authorization!.Scheme);
        Assert.Equal("sms:clave123", Encoding.UTF8.GetString(Convert.FromBase64String(celular.Ultima.Headers.Authorization.Parameter!)));
        using var json = JsonDocument.Parse(celular.Cuerpo!);
        Assert.Equal("ALERTA ROJA: evacúe.", json.RootElement.GetProperty("textMessage").GetProperty("text").GetString());
        Assert.Equal("+59171234567", json.RootElement.GetProperty("phoneNumbers")[0].GetString());
    }

    [Fact]
    public async Task Sin_id_en_la_respuesta_igual_cuenta_como_enviado()
    {
        var (emisor, _) = Armar(HttpStatusCode.OK, "");
        var r = await emisor.EnviarAsync(new MensajeSms("+59171234567", "x"));
        Assert.True(r.Exitoso);
        Assert.Equal("GW-200", r.IdProveedor);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Credenciales_o_datos_incorrectos_son_fallo_definitivo(HttpStatusCode estado)
    {
        var (emisor, _) = Armar(estado, """{"message":"invalid"}""");
        var r = await emisor.EnviarAsync(new MensajeSms("+59171234567", "x"));
        Assert.False(r.Exitoso);
        Assert.False(r.Recuperable);
        Assert.Contains(((int)estado).ToString(System.Globalization.CultureInfo.InvariantCulture), r.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Saturacion_o_error_del_celular_es_fallo_pasajero(HttpStatusCode estado)
    {
        var (emisor, _) = Armar(estado);
        var r = await emisor.EnviarAsync(new MensajeSms("+59171234567", "x"));
        Assert.True(r.Recuperable);
    }

    [Fact]
    public async Task Celular_apagado_o_fuera_de_la_red_es_fallo_pasajero()
    {
        var celular = new CelularFalso(_ => throw new HttpRequestException("No route to host"));
        var r = await new SmsGatewayEmisor(new HttpClient(celular), Opciones).EnviarAsync(new MensajeSms("+59171234567", "x"));
        Assert.False(r.Exitoso);
        Assert.True(r.Recuperable);
        Assert.Contains("celular", r.Error);
    }

    [Fact]
    public async Task Si_el_celular_no_responde_a_tiempo_es_fallo_pasajero()
    {
        var celular = new CelularFalso(_ => throw new TaskCanceledException("timeout"));
        var r = await new SmsGatewayEmisor(new HttpClient(celular), Opciones).EnviarAsync(new MensajeSms("+59171234567", "x"));
        Assert.True(r.Recuperable);
    }

    [Fact]
    public async Task Envia_a_telefonos_reales_asi_que_un_simulacro_se_bloquea()
    {
        var (emisor, celular) = Armar(HttpStatusCode.Accepted);
        Assert.True(emisor.EnviaReal);
        var r = await new EmisorModoPrueba(emisor).EnviarAsync(new MensajeSms("+59171234567", "x", EsPrueba: true));
        Assert.True(r.Bloqueado);
        Assert.Null(celular.Ultima);
    }

    // ---------- Confirmación del resultado (GET /message/{id}) ----------

    /// <summary>El celular acepta (202 con id) y después responde a cada consulta con el siguiente estado de la lista.</summary>
    private static (SmsGatewayEmisor Emisor, List<HttpRequestMessage> Consultas) ConEstados(params string?[] estados)
    {
        var consultas = new List<HttpRequestMessage>();
        var i = 0;
        var celular = new CelularFalso(req =>
        {
            if (req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("""{"id":"abc123","state":"Pending"}""") };
            consultas.Add(req);
            var estado = estados[Math.Min(i++, estados.Length - 1)];
            var cuerpo = estado == "Failed"
                ? """{"id":"abc123","state":"Failed","recipients":[{"phoneNumber":"+59171234567","state":"Failed","error":"Send result: RESULT_ERROR_GENERIC_FAILURE (Generic failure cause)"}]}"""
                : $$"""{"id":"abc123","state":"{{estado}}","recipients":[{"phoneNumber":"+59171234567","state":"{{estado}}"}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(cuerpo) };
        });
        var opciones = new OpcionesSmsGateway
        {
            Url = "http://192.168.1.50:8080", Usuario = "sms", Contrasena = "clave123", ConfirmarEnvio = true,
            TiempoConfirmacionSegundos = 30, IntervaloConsultaSegundos = 3, MargenTrasEnviadoSegundos = 9
        };
        // Sin esperas reales: la prueba solo cuenta el tiempo.
        return (new SmsGatewayEmisor(new HttpClient(celular), opciones, (_, _) => Task.CompletedTask), consultas);
    }

    private static readonly MensajeSms Alerta = new("+59171234567", "ALERTA NARANJA: crecida del rio Pirai en El Torno.");

    [Fact]
    public async Task Aceptado_y_luego_failed_es_fallo_pasajero_con_el_error_del_chip()
    {
        var (emisor, _) = ConEstados("Pending", "Processed", "Sent", "Failed");
        var r = await emisor.EnviarAsync(Alerta);
        Assert.False(r.Exitoso);
        Assert.True(r.Recuperable);
        Assert.Contains("RESULT_ERROR_GENERIC_FAILURE", r.Error);
    }

    [Fact]
    public async Task Aceptado_y_luego_sent_estable_es_enviado()
    {
        var (emisor, _) = ConEstados("Pending", "Sent");
        var r = await emisor.EnviarAsync(Alerta);
        Assert.True(r.Exitoso);
        Assert.Equal("abc123", r.IdProveedor);
    }

    [Fact]
    public async Task Delivered_es_enviado_de_inmediato()
    {
        var (emisor, consultas) = ConEstados("Delivered");
        Assert.True((await emisor.EnviarAsync(Alerta)).Exitoso);
        Assert.Single(consultas);
    }

    [Fact]
    public async Task Si_el_celular_nunca_informa_el_resultado_es_fallo_pasajero_por_tiempo()
    {
        var (emisor, consultas) = ConEstados("Pending");
        var r = await emisor.EnviarAsync(Alerta);
        Assert.True(r.Recuperable);
        Assert.Contains("no confirmó el envío a tiempo", r.Error);
        Assert.Contains("Pending", r.Error);
        Assert.Equal(11, consultas.Count); // 30 s cada 3 s, más la primera consulta
    }

    [Fact]
    public async Task El_estado_se_consulta_con_el_id_devuelto_y_autenticacion_basica()
    {
        var (emisor, consultas) = ConEstados("Delivered");
        await emisor.EnviarAsync(Alerta);
        var c = Assert.Single(consultas);
        Assert.Equal(HttpMethod.Get, c.Method);
        Assert.Equal("http://192.168.1.50:8080/message/abc123", c.RequestUri!.ToString());
        Assert.Equal("sms:clave123", Encoding.UTF8.GetString(Convert.FromBase64String(c.Headers.Authorization!.Parameter!)));
    }
}
