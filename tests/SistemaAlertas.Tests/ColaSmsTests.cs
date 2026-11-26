using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Domain.Difusion;
using SistemaAlertas.Infrastructure.Difusion;
using SistemaAlertas.Infrastructure.Persistence;
using SistemaAlertas.Infrastructure.Sms.Decoradores;

namespace SistemaAlertas.Tests;

/// <summary>
/// Sprint 3: la cola SMS persistente (DespachadorSms) con un contexto EF en memoria.
/// RNF-02: un lote grande termina dentro de los 5 minutos. RNF-08: reintento a los 60 s solo ante fallos recuperables.
/// </summary>
public class ColaSmsTests
{
    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    /// <summary>Proveedor falso: responde según el número y cuenta las llamadas. Puede tardar para simular la red.</summary>
    private sealed class ProveedorFalso(Func<string, int, ResultadoEnvioSms>? respuesta = null, int demoraMs = 0, bool real = false) : IEmisorSms
    {
        public ConcurrentDictionary<string, int> Llamadas { get; } = new();
        public int MaximoEnParalelo => _maximo;
        private int _enCurso, _maximo;
        public string Proveedor => "Falso";
        public bool EnviaReal => real;

        public async Task<ResultadoEnvioSms> EnviarAsync(MensajeSms m, CancellationToken ct = default)
        {
            var n = Llamadas.AddOrUpdate(m.Destino, 1, (_, v) => v + 1);
            var actual = Interlocked.Increment(ref _enCurso);
            InterlockedMax(ref _maximo, actual);
            try
            {
                if (demoraMs > 0) await Task.Delay(demoraMs, ct);
                return respuesta?.Invoke(m.Destino, n) ?? ResultadoEnvioSms.Ok($"OK-{m.Destino}-{n}");
            }
            finally { Interlocked.Decrement(ref _enCurso); }
        }

        private static void InterlockedMax(ref int destino, int valor)
        {
            int actual;
            while ((actual = destino) < valor && Interlocked.CompareExchange(ref destino, valor, actual) != actual) { }
        }
    }

    private static readonly DateTimeOffset T0 = new(2026, 12, 4, 10, 0, 0, TimeSpan.Zero);

    private static AlertasDbContext Contexto() => new(new DbContextOptionsBuilder<AlertasDbContext>()
        .UseInMemoryDatabase($"cola-{Guid.NewGuid()}").Options);

    private static void Encolar(AlertasDbContext db, int cantidad, bool esPrueba = false)
    {
        var version = Guid.NewGuid();
        for (var i = 0; i < cantidad; i++)
            db.TrabajosDifusion.Add(new EnvioSms(version, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), $"+5917{i:D7}",
                "ALERTA ROJA: evacúe hacia la zona alta.", esPrueba, T0));
        db.SaveChanges();
    }

    private static OpcionesSms Opciones(int lote = 200, int simultaneos = 10) =>
        new() { TamanoLote = lote, EnviosSimultaneos = simultaneos, MaxReintentos = 2, EsperaReintentoSegundos = 60 };

    private static async Task<int> VaciarAsync(DespachadorSms d)
    {
        int total = 0, n;
        while ((n = await d.ProcesarLoteAsync()) > 0) total += n;
        return total;
    }

    [Fact]
    public async Task RNF02_un_lote_de_1000_sms_termina_muy_por_debajo_de_los_5_minutos()
    {
        await using var db = Contexto();
        Encolar(db, 1000);
        var proveedor = new ProveedorFalso(demoraMs: 20); // 20 ms por SMS, como una red lenta
        var despachador = new DespachadorSms(db, proveedor, Opciones(lote: 200, simultaneos: 10), new RelojFijo(T0));

        var reloj = Stopwatch.StartNew();
        var procesados = await VaciarAsync(despachador);
        reloj.Stop();

        Assert.Equal(1000, procesados);
        Assert.Equal(1000, await db.TrabajosDifusion.OfType<EnvioSms>().CountAsync(e => e.Estado == EstadoTrabajo.Enviado));
        Assert.True(reloj.Elapsed < TimeSpan.FromMinutes(5), $"Tardó {reloj.Elapsed}");
        // Con 10 envíos a la vez, 1000 × 20 ms deberían tardar unos 2 s; en serie serían 20 s.
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(15), $"Tardó {reloj.Elapsed}: no se está enviando en paralelo");
        Assert.InRange(proveedor.MaximoEnParalelo, 2, 10);
    }

    [Fact]
    public async Task Respeta_el_tope_de_envios_simultaneos()
    {
        await using var db = Contexto();
        Encolar(db, 40);
        var proveedor = new ProveedorFalso(demoraMs: 10);
        await VaciarAsync(new DespachadorSms(db, proveedor, Opciones(simultaneos: 3), new RelojFijo(T0)));
        Assert.True(proveedor.MaximoEnParalelo <= 3);
    }

    [Fact]
    public async Task RNF08_un_fallo_recuperable_espera_60_segundos_y_luego_se_envia()
    {
        await using var db = Contexto();
        Encolar(db, 1);
        var reloj = new RelojFijo(T0);
        var proveedor = new ProveedorFalso((_, n) => n == 1 ? ResultadoEnvioSms.FalloRecuperable("Proveedor saturado") : ResultadoEnvioSms.Ok("SM2"));
        var despachador = new DespachadorSms(db, proveedor, Opciones(), reloj);

        Assert.Equal(1, await despachador.ProcesarLoteAsync());
        var envio = await db.TrabajosDifusion.OfType<EnvioSms>().SingleAsync();
        Assert.Equal(EstadoTrabajo.Pendiente, envio.Estado);
        Assert.Equal(T0.AddSeconds(60), envio.ProximoIntento);

        reloj.Ahora = T0.AddSeconds(30);
        Assert.Equal(0, await despachador.ProcesarLoteAsync()); // todavía no toca

        reloj.Ahora = T0.AddSeconds(60);
        Assert.Equal(1, await despachador.ProcesarLoteAsync());
        Assert.Equal(EstadoTrabajo.Enviado, envio.Estado);
        Assert.Equal(2, envio.Intentos);
        Assert.Equal("SM2", envio.CodigoProveedor);
    }

    [Fact]
    public async Task RNF08_como_maximo_tres_intentos_en_total()
    {
        await using var db = Contexto();
        Encolar(db, 1);
        var reloj = new RelojFijo(T0);
        var proveedor = new ProveedorFalso((_, _) => ResultadoEnvioSms.FalloRecuperable("Sin red"));
        var despachador = new DespachadorSms(db, proveedor, Opciones(), reloj);

        for (var i = 0; i < 5; i++)
        {
            await despachador.ProcesarLoteAsync();
            reloj.Ahora = reloj.Ahora.AddSeconds(61);
        }

        var envio = await db.TrabajosDifusion.OfType<EnvioSms>().SingleAsync();
        Assert.Equal(EstadoTrabajo.Fallido, envio.Estado);
        Assert.Equal(3, envio.Intentos);
        Assert.Equal(3, proveedor.Llamadas.Values.Single());
    }

    [Fact]
    public async Task RNF08_un_fallo_definitivo_no_se_reintenta()
    {
        await using var db = Contexto();
        Encolar(db, 1);
        var reloj = new RelojFijo(T0);
        var proveedor = new ProveedorFalso((_, _) => ResultadoEnvioSms.FalloDefinitivo("Número inválido"));
        var despachador = new DespachadorSms(db, proveedor, Opciones(), reloj);

        await despachador.ProcesarLoteAsync();
        reloj.Ahora = T0.AddMinutes(10);
        Assert.Equal(0, await despachador.ProcesarLoteAsync());

        var envio = await db.TrabajosDifusion.OfType<EnvioSms>().SingleAsync();
        Assert.Equal(EstadoTrabajo.Fallido, envio.Estado);
        Assert.Equal(1, envio.Intentos);
    }

    [Fact]
    public async Task Una_excepcion_del_proveedor_se_trata_como_fallo_pasajero()
    {
        await using var db = Contexto();
        Encolar(db, 1);
        var proveedor = new ProveedorFalso((_, _) => throw new HttpRequestException("timeout"));
        await new DespachadorSms(db, proveedor, Opciones(), new RelojFijo(T0)).ProcesarLoteAsync();

        var envio = await db.TrabajosDifusion.OfType<EnvioSms>().SingleAsync();
        Assert.Equal(EstadoTrabajo.Pendiente, envio.Estado);
        Assert.Contains("timeout", envio.UltimoError);
    }

    [Fact]
    public async Task Un_aviso_de_prueba_no_sale_por_un_proveedor_real()
    {
        await using var db = Contexto();
        Encolar(db, 2, esPrueba: true);
        var real = new ProveedorFalso(real: true);
        await new DespachadorSms(db, new EmisorModoPrueba(real), Opciones(), new RelojFijo(T0)).ProcesarLoteAsync();

        Assert.All(await db.TrabajosDifusion.OfType<EnvioSms>().ToListAsync(), e => Assert.Equal(EstadoTrabajo.Bloqueado, e.Estado));
        Assert.Empty(real.Llamadas);
    }

    [Fact]
    public async Task Las_tareas_manuales_no_entran_en_la_cola()
    {
        await using var db = Contexto();
        db.TrabajosDifusion.Add(new TareaManual(Guid.NewGuid(), Guid.NewGuid(), null, CanalDifusion.Radio, "Leer al aire", false, T0));
        db.SaveChanges();
        Assert.Equal(0, await new DespachadorSms(db, new ProveedorFalso(), Opciones(), new RelojFijo(T0)).ProcesarLoteAsync());
    }
}
