using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Application.Difusion.Canales;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Difusion;
using SistemaAlertas.Infrastructure.Canales;
using SistemaAlertas.Infrastructure.Difusion;
using SistemaAlertas.Infrastructure.Persistence;

namespace SistemaAlertas.Tests;

/// <summary>Sprint 3: WhatsApp a grupos (WAHA) y Email (SMTP) como canales automáticos de la cola.</summary>
public class CanalesAutomaticosTests
{
    private static readonly DateTimeOffset T0 = ColaSmsTests.T0;

    // ---------- WAHA ----------

    private sealed class WahaFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
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

    private static readonly OpcionesWhatsApp Whatsapp = new()
    {
        Url = "http://localhost:3000/", ApiKey = "clave-waha", Sesion = "default",
        Grupos = [new() { Nombre = "Alertas cuenca Piraí", Id = "120363012345678901@g.us" }]
    };

    private static (WahaEmisor Emisor, WahaFalso Waha) Waha(HttpStatusCode estado, string cuerpo = "")
    {
        var waha = new WahaFalso(_ => new HttpResponseMessage(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") });
        return (new WahaEmisor(new HttpClient(waha), Whatsapp), waha);
    }

    [Fact]
    public async Task WhatsApp_manda_el_texto_al_grupo_con_la_clave_de_waha()
    {
        var (emisor, waha) = Waha(HttpStatusCode.Created, """{"id":{"_serialized":"true_120363012345678901@g.us_3EB0ABC"}}""");

        var r = await emisor.EnviarAsync(new MensajeCanal("120363012345678901@g.us", "ALERTA ROJA en El Torno", null, Guid.NewGuid(), false));

        Assert.True(r.Exitoso);
        Assert.Equal("true_120363012345678901@g.us_3EB0ABC", r.IdProveedor);
        Assert.Equal("http://localhost:3000/api/sendText", waha.Ultima!.RequestUri!.ToString());
        Assert.Equal("clave-waha", waha.Ultima.Headers.GetValues("X-Api-Key").Single());
        using var json = JsonDocument.Parse(waha.Cuerpo!);
        Assert.Equal("default", json.RootElement.GetProperty("session").GetString());
        Assert.Equal("120363012345678901@g.us", json.RootElement.GetProperty("chatId").GetString());
        Assert.Equal("ALERTA ROJA en El Torno", json.RootElement.GetProperty("text").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.UnprocessableEntity, false)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    public async Task WhatsApp_clasifica_las_respuestas_de_waha(HttpStatusCode estado, bool recuperable)
    {
        var (emisor, _) = Waha(estado, "{}");
        var r = await emisor.EnviarAsync(new MensajeCanal("120363012345678901@g.us", "x", null, Guid.NewGuid(), false));
        Assert.False(r.Exitoso);
        Assert.Equal(recuperable, r.Recuperable);
    }

    [Fact]
    public async Task WhatsApp_sin_conexion_con_waha_es_fallo_pasajero()
    {
        var waha = new WahaFalso(_ => throw new HttpRequestException("Connection refused"));
        var r = await new WahaEmisor(new HttpClient(waha), Whatsapp).EnviarAsync(new MensajeCanal("x@g.us", "x", null, Guid.NewGuid(), false));
        Assert.True(r.Recuperable);
    }

    [Fact]
    public void Un_grupo_sin_id_no_cuenta_y_sin_grupos_el_canal_queda_apagado()
    {
        Assert.True(Whatsapp.Habilitado);
        var sinGrupos = new OpcionesWhatsApp { Url = "http://localhost:3000", ApiKey = "k", Grupos = [new() { Nombre = "vacío", Id = " " }] };
        Assert.False(sinGrupos.Habilitado);
        Assert.False(new OpcionesEmail { Servidor = "smtp.gmail.com", Usuario = "a@gmail.com" }.Habilitado);
    }

    // ---------- SMTP ----------

    private sealed class SmtpFalso(Exception? error = null) : IClienteSmtp
    {
        public MailMessage? Ultimo { get; private set; }
        public Task EnviarAsync(MailMessage correo, CancellationToken ct)
        {
            Ultimo = new MailMessage(correo.From!, correo.To[0]) { Subject = correo.Subject, Body = correo.Body };
            return error is null ? Task.CompletedTask : Task.FromException(error);
        }
    }

    private static readonly OpcionesEmail Email = new()
    {
        Servidor = "smtp.gmail.com", Usuario = "sat.pirai@gmail.com", Contrasena = "app-pass", Remitente = "sat.pirai@gmail.com"
    };

    [Fact]
    public async Task Email_manda_asunto_y_texto_desde_el_remitente()
    {
        var smtp = new SmtpFalso();
        var r = await new SmtpEmisor(smtp, Email).EnviarAsync(
            new MensajeCanal("vecina@ejemplo.bo", "ALERTA ROJA. Evacúe.", "Alerta roja en El Torno", Guid.NewGuid(), false));

        Assert.True(r.Exitoso);
        Assert.Equal("sat.pirai@gmail.com", smtp.Ultimo!.From!.Address);
        Assert.Equal("SAT Río Piraí", smtp.Ultimo.From.DisplayName);
        Assert.Equal("vecina@ejemplo.bo", smtp.Ultimo.To[0].Address);
        Assert.Equal("Alerta roja en El Torno", smtp.Ultimo.Subject);
        Assert.Equal("ALERTA ROJA. Evacúe.", smtp.Ultimo.Body);
    }

    [Fact]
    public async Task Email_a_una_direccion_mal_escrita_es_fallo_definitivo()
    {
        var r = await new SmtpEmisor(new SmtpFalso(), Email).EnviarAsync(new MensajeCanal("no-es-correo", "x", "x", Guid.NewGuid(), false));
        Assert.False(r.Recuperable);
        Assert.Contains("no válida", r.Error);
    }

    [Fact]
    public async Task Email_con_servidor_ocupado_es_pasajero_y_con_destinatario_rechazado_es_definitivo()
    {
        var ocupado = await new SmtpEmisor(new SmtpFalso(new SmtpException(SmtpStatusCode.MailboxBusy, "busy")), Email)
            .EnviarAsync(new MensajeCanal("a@b.bo", "x", "x", Guid.NewGuid(), false));
        Assert.True(ocupado.Recuperable);

        var rechazado = await new SmtpEmisor(new SmtpFalso(new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, "a@b.bo")), Email)
            .EnviarAsync(new MensajeCanal("a@b.bo", "x", "x", Guid.NewGuid(), false));
        Assert.False(rechazado.Recuperable);

        var credenciales = await new SmtpEmisor(new SmtpFalso(new SmtpException(SmtpStatusCode.ClientNotPermitted, "auth")), Email)
            .EnviarAsync(new MensajeCanal("a@b.bo", "x", "x", Guid.NewGuid(), false));
        Assert.False(credenciales.Recuperable);
    }

    // ---------- La cola reparte por canal ----------

    private sealed class CanalFalso(CanalDifusion canal, bool real = true) : IEmisorCanal
    {
        public List<MensajeCanal> Enviados { get; } = [];
        public CanalDifusion Canal => canal;
        public string Proveedor => $"Falso {canal}";
        public bool EnviaReal => real;
        public Task<ResultadoEnvioSms> EnviarAsync(MensajeCanal m, CancellationToken ct = default)
        {
            lock (Enviados) Enviados.Add(m);
            return Task.FromResult(ResultadoEnvioSms.Ok($"{canal}-1"));
        }
    }

    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private static (AlertasDbContext Db, ColaSmsTests.VersionesFalsas Versiones, Domain.Mensajes.VersionMensaje Version) Base()
    {
        var db = new AlertasDbContext(new DbContextOptionsBuilder<AlertasDbContext>().UseInMemoryDatabase($"canales-{Guid.NewGuid()}").Options);
        var versiones = new ColaSmsTests.VersionesFalsas();
        var v = ColaSmsTests.VersionAprobada(Guid.NewGuid());
        versiones.Lista.Add(v);
        return (db, versiones, v);
    }

    [Fact]
    public async Task La_cola_manda_cada_envio_por_su_canal()
    {
        var (db, versiones, v) = Base();
        db.TrabajosDifusion.AddRange(
            new EnvioWhatsApp(v.Id, v.AvisoId, "120363012345678901@g.us", "Alertas cuenca", "ALERTA ROJA texto completo", false, T0),
            new EnvioEmail(v.Id, v.AvisoId, null, Guid.NewGuid(), "vecina@ejemplo.bo", "Alerta roja", "ALERTA ROJA texto completo", false, T0),
            new EnvioSms(v.Id, v.AvisoId, null, Guid.NewGuid(), "+59171234567", "ALERTA ROJA", false, T0));
        await db.SaveChangesAsync();
        CanalFalso sms = new(CanalDifusion.Sms), wa = new(CanalDifusion.WhatsApp), mail = new(CanalDifusion.Email);

        await new DespachadorEnvios(db, versiones, [sms, wa, mail], new OpcionesSms(), new Reloj(T0)).ProcesarLoteAsync();

        Assert.Equal("120363012345678901@g.us", Assert.Single(wa.Enviados).Destino);
        Assert.Equal("Alerta roja", Assert.Single(mail.Enviados).Asunto);
        Assert.Equal("+59171234567", Assert.Single(sms.Enviados).Destino);
        Assert.All(await db.TrabajosDifusion.ToListAsync(), t => Assert.Equal(EstadoTrabajo.Enviado, t.Estado));
    }

    [Fact]
    public async Task Un_canal_sin_configurar_deja_el_envio_como_fallido_y_un_simulacro_no_sale_por_un_canal_real()
    {
        var (db, versiones, v) = Base();
        var sinCanal = new EnvioEmail(v.Id, v.AvisoId, null, Guid.NewGuid(), "a@b.bo", "x", "x", false, T0);
        var simulacro = new EnvioWhatsApp(v.Id, v.AvisoId, "1@g.us", "g", "x", true, T0);
        db.TrabajosDifusion.AddRange(sinCanal, simulacro);
        await db.SaveChangesAsync();
        var wa = new CanalFalso(CanalDifusion.WhatsApp);

        await new DespachadorEnvios(db, versiones, [wa], new OpcionesSms(), new Reloj(T0)).ProcesarLoteAsync();

        Assert.Equal(EstadoTrabajo.Fallido, sinCanal.Estado);
        Assert.Contains("no está configurado", sinCanal.Resultado);
        Assert.Equal(EstadoTrabajo.Bloqueado, simulacro.Estado);
        Assert.Empty(wa.Enviados);
    }

    [Fact]
    public void Un_envio_a_un_grupo_no_tiene_contacto_y_whatsapp_no_es_una_tarea_manual()
    {
        var e = new EnvioWhatsApp(Guid.NewGuid(), Guid.NewGuid(), "1@g.us", "Grupo", "x", false, T0);
        Assert.Null(e.ContactoId);
        Assert.Equal(CanalDifusion.WhatsApp, e.Canal);
        Assert.Throws<ExcepcionDominio>(() => new TareaManual(Guid.NewGuid(), Guid.NewGuid(), null, CanalDifusion.WhatsApp, "x", false, T0));
        Assert.Throws<ExcepcionDominio>(() => new EnvioEmail(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), "a@b.bo", " ", "x", false, T0));
    }

    // ---------- Comandos de la difusión ----------

    private sealed class ColaFalsa : IColaSms
    {
        public List<EnvioAutomatico> Envios { get; } = [];
        public Task<int> EncolarAsync(IReadOnlyCollection<EnvioAutomatico> envios, CancellationToken ct = default)
        { Envios.AddRange(envios); return Task.FromResult(envios.Count); }
    }

    private sealed class Destinatarios(params DestinatarioEmail[] correos) : IDestinatariosDifusion
    {
        public Task<IReadOnlyList<DestinatarioSms>> ObtenerSmsAsync(VersionAprobada v, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DestinatarioSms>>([]);
        public Task<IReadOnlyList<DestinatarioEmail>> ObtenerEmailAsync(VersionAprobada v, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DestinatarioEmail>>(correos);
    }

    private static readonly VersionAprobada Aprobada = new(Guid.NewGuid(), Guid.NewGuid(), "Alerta roja en El Torno",
        "ALERTA ROJA. Crecida del río Piraí. Evacúe hacia la zona alta.", "ALERTA ROJA: evacúe.", [], false);

    [Fact]
    public async Task La_difusion_encola_un_mensaje_por_grupo_con_el_texto_completo()
    {
        var cola = new ColaFalsa();
        var opciones = new OpcionesWhatsApp { Url = "http://waha:3000", ApiKey = "k",
            Grupos = [new() { Nombre = "Cuenca", Id = "1@g.us" }, new() { Nombre = "El Torno", Id = "2@g.us" }] };
        var c = new ContextoDifusion(Aprobada);

        await new EncolarWhatsApp(opciones, cola).EjecutarAsync(c);

        Assert.Equal(2, c.EnviosWhatsApp);
        Assert.All(cola.Envios, e => Assert.Equal(Aprobada.TextoCompleto, e.Texto));
        Assert.Equal(["Cuenca", "El Torno"], cola.Envios.Select(e => e.NombreDestino));
    }

    [Fact]
    public async Task Sin_configurar_whatsapp_ni_email_la_difusion_no_encola_nada()
    {
        var cola = new ColaFalsa();
        var c = new ContextoDifusion(Aprobada);
        await new EncolarWhatsApp(new OpcionesWhatsApp(), cola).EjecutarAsync(c);
        await new EncolarEmail(new OpcionesEmail(), new Destinatarios(new DestinatarioEmail(Guid.NewGuid(), Guid.NewGuid(), "Ana", "ana@b.bo")), cola).EjecutarAsync(c);
        Assert.Empty(cola.Envios);
    }

    [Fact]
    public async Task La_difusion_encola_un_correo_por_contacto_con_asunto_marcado_si_es_prueba()
    {
        var cola = new ColaFalsa();
        var c = new ContextoDifusion(Aprobada with { EsPrueba = true });
        await new EncolarEmail(Email, new Destinatarios(new DestinatarioEmail(Guid.NewGuid(), Guid.NewGuid(), "Ana", "ana@b.bo"),
            new DestinatarioEmail(Guid.NewGuid(), Guid.NewGuid(), "Luis", "luis@b.bo")), cola).EjecutarAsync(c);
        Assert.Equal(2, c.EnviosEmail);
        Assert.All(cola.Envios, e => Assert.Equal("[PRUEBA] Alerta roja en El Torno", e.Asunto));
    }
}
