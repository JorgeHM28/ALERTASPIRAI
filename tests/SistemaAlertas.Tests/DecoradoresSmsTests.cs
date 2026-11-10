using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Infrastructure.Sms;
using SistemaAlertas.Infrastructure.Sms.Decoradores;

namespace SistemaAlertas.Tests;

/// <summary>Emisor de prueba: devuelve los resultados en el orden indicado y cuenta las llamadas.</summary>
internal sealed class EmisorGuionado(bool enviaReal, params ResultadoEnvioSms[] guion) : IEmisorSms
{
    private int _llamadas;
    public List<MensajeSms> Recibidos { get; } = [];
    public int Llamadas => _llamadas;
    public string Proveedor => enviaReal ? "Real" : "Simulador";
    public bool EnviaReal => enviaReal;

    public Task<ResultadoEnvioSms> EnviarAsync(MensajeSms mensaje, CancellationToken ct = default)
    {
        Recibidos.Add(mensaje);
        var i = Interlocked.Increment(ref _llamadas) - 1;
        return Task.FromResult(guion[Math.Min(i, guion.Length - 1)]);
    }
}

internal sealed class BitacoraFalsa : IBitacoraEnvios
{
    public List<RegistroEnvioSms> Registros { get; } = [];
    public Task RegistrarAsync(RegistroEnvioSms registro, CancellationToken ct = default) { Registros.Add(registro); return Task.CompletedTask; }
    public IReadOnlyList<RegistroEnvioSms> Recientes(int cantidad = 100) => Registros;
}

public class DecoradoresSmsTests
{
    private static readonly MensajeSms Real = new("+59171234589", "Evacúe hacia zonas altas", Guid.NewGuid());
    private static readonly MensajeSms DePrueba = Real with { EsPrueba = true };
    private static readonly Func<int, CancellationToken, Task> SinEspera = (_, _) => Task.CompletedTask;

    private static EmisorConReintentos ConReintentos(IEmisorSms interno, int max = 2) => new(interno, max, esperar: SinEspera);

    [Fact]
    public async Task Reintenta_un_fallo_recuperable_hasta_que_sale()
    {
        var interno = new EmisorGuionado(true, ResultadoEnvioSms.FalloRecuperable("503"), ResultadoEnvioSms.Ok("SM1"));

        var r = await ConReintentos(interno).EnviarAsync(Real);

        Assert.True(r.Exitoso);
        Assert.Equal(2, interno.Llamadas);
        Assert.Equal(2, r.Intentos);
    }

    [Fact]
    public async Task Hace_como_maximo_dos_reintentos_adicionales()
    {
        var interno = new EmisorGuionado(true, ResultadoEnvioSms.FalloRecuperable("503"));

        var r = await ConReintentos(interno).EnviarAsync(Real);

        Assert.False(r.Exitoso);
        Assert.Equal(3, interno.Llamadas); // 1 intento + 2 reintentos
        Assert.Equal(3, r.Intentos);
    }

    [Fact]
    public async Task No_pasa_de_dos_reintentos_aunque_la_configuracion_pida_mas()
    {
        var interno = new EmisorGuionado(true, ResultadoEnvioSms.FalloRecuperable("503"));
        await ConReintentos(interno, max: 10).EnviarAsync(Real);
        Assert.Equal(3, interno.Llamadas);
    }

    [Fact]
    public async Task No_reintenta_un_fallo_definitivo()
    {
        var interno = new EmisorGuionado(true, ResultadoEnvioSms.FalloDefinitivo("número inválido"));

        var r = await ConReintentos(interno).EnviarAsync(Real);

        Assert.False(r.Exitoso);
        Assert.Equal(1, interno.Llamadas);
    }

    [Fact]
    public async Task No_reintenta_un_envio_exitoso()
    {
        var interno = new EmisorGuionado(true, ResultadoEnvioSms.Ok("SM1"));
        await ConReintentos(interno).EnviarAsync(Real);
        Assert.Equal(1, interno.Llamadas);
    }

    [Fact]
    public async Task El_modo_prueba_bloquea_el_envio_real_de_un_aviso_de_prueba()
    {
        var interno = new EmisorGuionado(true, ResultadoEnvioSms.Ok("SM1"));

        var r = await new EmisorModoPrueba(interno).EnviarAsync(DePrueba);

        Assert.True(r.Bloqueado);
        Assert.False(r.Exitoso);
        Assert.Equal(0, interno.Llamadas);
    }

    [Fact]
    public async Task El_modo_prueba_marca_el_mensaje_cuando_el_proveedor_es_el_simulador()
    {
        var interno = new EmisorGuionado(false, ResultadoEnvioSms.Ok("SIM-1"));

        var r = await new EmisorModoPrueba(interno).EnviarAsync(DePrueba);

        Assert.True(r.Exitoso);
        Assert.StartsWith(EmisorModoPrueba.Marca, Assert.Single(interno.Recibidos).Texto);
    }

    [Fact]
    public async Task El_modo_prueba_no_toca_un_aviso_real()
    {
        var interno = new EmisorGuionado(true, ResultadoEnvioSms.Ok("SM1"));
        await new EmisorModoPrueba(interno).EnviarAsync(Real);
        Assert.Equal(Real.Texto, Assert.Single(interno.Recibidos).Texto);
    }

    [Fact]
    public async Task La_bitacora_registra_el_resultado_con_el_destino_enmascarado()
    {
        var bitacora = new BitacoraFalsa();
        var emisor = new EmisorConBitacora(new EmisorGuionado(true, ResultadoEnvioSms.Ok("SM1")), bitacora);

        await emisor.EnviarAsync(Real);

        var registro = Assert.Single(bitacora.Registros);
        Assert.True(registro.Exitoso);
        Assert.Equal("SM1", registro.IdProveedor);
        Assert.Equal(Real.AvisoId, registro.AvisoId);
        Assert.Equal("+591 7*****89", registro.DestinoEnmascarado);
    }

    [Fact]
    public async Task La_cadena_completa_registra_una_sola_vez_con_el_numero_de_intentos()
    {
        var bitacora = new BitacoraFalsa();
        var proveedor = new EmisorGuionado(true, ResultadoEnvioSms.FalloRecuperable("429"), ResultadoEnvioSms.Ok("SM2"));
        IEmisorSms cadena = new EmisorConBitacora(new EmisorModoPrueba(ConReintentos(proveedor)), bitacora);

        var r = await cadena.EnviarAsync(Real);

        Assert.True(r.Exitoso);
        Assert.Equal(2, Assert.Single(bitacora.Registros).Intentos);
    }

    [Fact]
    public async Task En_la_cadena_un_aviso_de_prueba_bloqueado_no_se_reintenta()
    {
        var bitacora = new BitacoraFalsa();
        var proveedor = new EmisorGuionado(true, ResultadoEnvioSms.Ok("SM1"));
        IEmisorSms cadena = new EmisorConBitacora(new EmisorModoPrueba(ConReintentos(proveedor)), bitacora);

        var r = await cadena.EnviarAsync(DePrueba);

        Assert.True(r.Bloqueado);
        Assert.Equal(0, proveedor.Llamadas);
        Assert.True(Assert.Single(bitacora.Registros).Bloqueado);
    }

    [Fact]
    public void El_registro_de_di_encadena_los_decoradores_sobre_el_simulador()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var servicios = new ServiceCollection().AddLogging().AddCanalSms(configuracion).BuildServiceProvider();

        var emisor = servicios.GetRequiredService<IEmisorSms>();

        Assert.IsType<EmisorConBitacora>(emisor);
        Assert.Equal("Simulador", emisor.Proveedor);
        Assert.False(emisor.EnviaReal);
    }

    [Fact]
    public void El_modo_twilio_sin_credenciales_falla_al_arrancar()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Sms:Modo"] = "Twilio" }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCanalSms(configuracion));
    }

    [Fact]
    public async Task El_simulador_no_envia_nada_real_y_guarda_el_mensaje()
    {
        var simulador = new SimuladorEmisor(NullLogger<SimuladorEmisor>.Instance);
        var r = await simulador.EnviarAsync(Real);
        Assert.True(r.Exitoso);
        Assert.StartsWith("SIM-", r.IdProveedor);
        Assert.Single(simulador.Enviados);
    }
}
