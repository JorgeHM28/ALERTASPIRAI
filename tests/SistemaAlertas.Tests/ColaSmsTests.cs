using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Mensajes;
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

    internal static readonly DateTimeOffset T0 = new(2026, 12, 4, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Las versiones viven en memoria (el proveedor EF en memoria no maneja el contenido del mensaje): este doble aplica
    /// la misma regla del dominio, <see cref="VersionMensaje.MotivoParaDetenerDifusion"/>.
    /// </summary>
    internal sealed class VersionesFalsas : IVersionesDifusion
    {
        public List<VersionMensaje> Lista { get; } = [];
        public Task<IReadOnlyDictionary<Guid, string?>> MotivosParaDetenerAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset ahora, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string?>>(ids.ToDictionary(id => id,
                id => Lista.FirstOrDefault(v => v.Id == id)?.MotivoParaDetenerDifusion(ahora) ?? (Lista.Any(v => v.Id == id) ? null : "no existe")));
        public Task<(int Numero, string Tipo, IReadOnlyList<Guid> Anteriores)> AnterioresAsync(Guid versionId, CancellationToken ct = default)
        {
            var v = Lista.Single(x => x.Id == versionId);
            return Task.FromResult<(int, string, IReadOnlyList<Guid>)>((v.Numero, v.Tipo.ToString(),
                Lista.Where(x => x.CadenaId == v.CadenaId && x.Id != versionId).Select(x => x.Id).ToList()));
        }
    }

    private static AlertasDbContext Contexto() => new(new DbContextOptionsBuilder<AlertasDbContext>()
        .UseInMemoryDatabase($"cola-{Guid.NewGuid()}").Options);

    private static readonly Polygon Area = new GeometryFactory(new PrecisionModel(), 4326)
        .CreatePolygon([new(-63.25, -17.8), new(-63.2, -17.8), new(-63.2, -17.75), new(-63.25, -17.8)]);

    /// <summary>Versión aprobada (la prepara un operador y la aprueba otra persona) que vence en <paramref name="expira"/>.</summary>
    internal static VersionMensaje VersionAprobada(Guid cadena, int numero = 1, TipoMensajeCap tipo = TipoMensajeCap.Alert,
        VersionMensaje? referencia = null, DateTimeOffset? expira = null)
    {
        var contenido = new ContenidoMensaje("Crecida del río Piraí", UrgenciaCap.Immediate, SeveridadCap.Severe, CertezaCap.Observed,
            "Crecida en El Torno", "El río sube.", "Evacuar a la zona alta.", T0, expira ?? T0.AddDays(30), "El Torno");
        var v = new VersionMensaje(cadena, cadena, numero, tipo, contenido, Area, EstadoCap.Actual, "sat@pirai", Guid.NewGuid(), referencia);
        if (tipo != TipoMensajeCap.Cancel)
            v.VincularProtocolo(new ProtocoloOficial("Evacuación", "Evacuar a la zona alta.", "COED", "Plan 01", new DateOnly(2026, 1, 15)));
        v.EnviarARevision("<alert/>", T0, T0);
        v.Aprobar(Guid.NewGuid(), T0, referencia);
        return v;
    }

    private readonly VersionesFalsas _versiones = new();

    private DespachadorSms Despachador(AlertasDbContext db, IEmisorSms emisor, OpcionesSms opciones, TimeProvider reloj) =>
        new(db, _versiones, emisor, opciones, reloj);

    private VersionMensaje Encolar(AlertasDbContext db, int cantidad, bool esPrueba = false, VersionMensaje? version = null)
    {
        if (version is null)
        {
            version = VersionAprobada(Guid.NewGuid());
            _versiones.Lista.Add(version);
        }
        for (var i = 0; i < cantidad; i++)
            db.TrabajosDifusion.Add(new EnvioSms(version.Id, version.AvisoId, Guid.NewGuid(), Guid.NewGuid(), $"+5917{i:D7}",
                "ALERTA ROJA: evacúe hacia la zona alta.", esPrueba, T0));
        db.SaveChanges();
        return version;
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
        var despachador = Despachador(db, proveedor, Opciones(lote: 200, simultaneos: 10), new RelojFijo(T0));

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
        await VaciarAsync(Despachador(db, proveedor, Opciones(simultaneos: 3), new RelojFijo(T0)));
        Assert.True(proveedor.MaximoEnParalelo <= 3);
    }

    [Fact]
    public async Task RNF08_un_fallo_recuperable_espera_60_segundos_y_luego_se_envia()
    {
        await using var db = Contexto();
        Encolar(db, 1);
        var reloj = new RelojFijo(T0);
        var proveedor = new ProveedorFalso((_, n) => n == 1 ? ResultadoEnvioSms.FalloRecuperable("Proveedor saturado") : ResultadoEnvioSms.Ok("SM2"));
        var despachador = Despachador(db, proveedor, Opciones(), reloj);

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
        var despachador = Despachador(db, proveedor, Opciones(), reloj);

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
        var despachador = Despachador(db, proveedor, Opciones(), reloj);

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
        await Despachador(db, proveedor, Opciones(), new RelojFijo(T0)).ProcesarLoteAsync();

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
        await Despachador(db, new EmisorModoPrueba(real), Opciones(), new RelojFijo(T0)).ProcesarLoteAsync();

        Assert.All(await db.TrabajosDifusion.OfType<EnvioSms>().ToListAsync(), e => Assert.Equal(EstadoTrabajo.Bloqueado, e.Estado));
        Assert.Empty(real.Llamadas);
    }

    [Fact]
    public async Task Las_tareas_manuales_no_entran_en_la_cola()
    {
        await using var db = Contexto();
        db.TrabajosDifusion.Add(new TareaManual(Guid.NewGuid(), Guid.NewGuid(), null, CanalDifusion.Radio, "Leer al aire", false, T0));
        db.SaveChanges();
        Assert.Equal(0, await Despachador(db, new ProveedorFalso(), Opciones(), new RelojFijo(T0)).ProcesarLoteAsync());
    }

    // ---------- RF-04 y RNF-08: lo pendiente de una versión que ya no vale no sale ----------

    [Fact]
    public async Task RF04_un_sms_pendiente_de_una_version_reemplazada_no_se_envia()
    {
        await using var db = Contexto();
        var cadena = Guid.NewGuid();
        var v1 = VersionAprobada(cadena);
        _versiones.Lista.Add(v1);
        Encolar(db, 3, version: v1);
        // Llega el Update aprobado: reemplaza a la v1 antes de que la cola la procese.
        _versiones.Lista.Add(VersionAprobada(cadena, 2, TipoMensajeCap.Update, v1));

        var proveedor = new ProveedorFalso();
        await Despachador(db, proveedor, Opciones(), new RelojFijo(T0)).ProcesarLoteAsync();

        Assert.Empty(proveedor.Llamadas);
        Assert.All(await db.TrabajosDifusion.OfType<EnvioSms>().ToListAsync(), e =>
        {
            Assert.Equal(EstadoTrabajo.Cancelado, e.Estado);
            Assert.Contains("reemplazada", e.Resultado);
            Assert.Null(e.ProximoIntento);
        });
    }

    [Fact]
    public async Task RNF08_antes_de_reintentar_se_revisa_que_la_version_siga_vigente()
    {
        await using var db = Contexto();
        // La versión vence a los 30 s: el primer intento falla y el reintento (a los 60 s) ya no debe salir.
        var version = VersionAprobada(Guid.NewGuid(), expira: T0.AddSeconds(30));
        _versiones.Lista.Add(version);
        Encolar(db, 1, version: version);
        var reloj = new RelojFijo(T0);
        var proveedor = new ProveedorFalso((_, _) => ResultadoEnvioSms.FalloRecuperable("Proveedor saturado"));
        var despachador = Despachador(db, proveedor, Opciones(), reloj);

        await despachador.ProcesarLoteAsync();
        reloj.Ahora = T0.AddSeconds(60);
        await despachador.ProcesarLoteAsync();

        var envio = await db.TrabajosDifusion.OfType<EnvioSms>().SingleAsync();
        Assert.Equal(EstadoTrabajo.Cancelado, envio.Estado);
        Assert.Contains("venció", envio.Resultado);
        Assert.Equal(1, envio.Intentos);
        Assert.Equal(1, proveedor.Llamadas.Values.Single());
    }

    [Fact]
    public async Task RF04_difundir_un_update_detiene_lo_pendiente_de_la_version_anterior_y_respeta_lo_hecho()
    {
        await using var db = Contexto();
        var cadena = Guid.NewGuid();
        var v1 = VersionAprobada(cadena);
        _versiones.Lista.Add(v1);
        Encolar(db, 2, version: v1);
        var enviado = await db.TrabajosDifusion.OfType<EnvioSms>().FirstAsync();
        enviado.RegistrarIntento(new ResultadoIntento(true, "SM1", null, false, false), T0);
        var altavoz = new TareaManual(v1.Id, v1.AvisoId, Guid.NewGuid(), CanalDifusion.Altavoz, "Anunciar", false, T0);
        var radio = new TareaManual(v1.Id, v1.AvisoId, null, CanalDifusion.Radio, "Leer al aire", false, T0);
        radio.Confirmar(Guid.NewGuid(), null, T0);
        db.TrabajosDifusion.AddRange(altavoz, radio);
        var v2 = VersionAprobada(cadena, 2, TipoMensajeCap.Update, v1);
        _versiones.Lista.Add(v2);
        await db.SaveChangesAsync();

        var detenidos = await new DetenedorDespachosAnteriores(db, _versiones, new RelojFijo(T0.AddMinutes(5)))
            .DetenerAsync(new VersionAprobada(v2.Id, v2.AvisoId, "Alerta roja", "Alerta roja.", "ALERTA ROJA", [], false));

        Assert.Equal(2, detenidos); // el SMS pendiente y la tarea de altavoz
        var trabajos = await db.TrabajosDifusion.Where(t => t.VersionId == v1.Id).ToListAsync();
        Assert.Equal(EstadoTrabajo.Enviado, trabajos.Single(t => t.Id == enviado.Id).Estado);
        Assert.Equal(EstadoTrabajo.Confirmada, trabajos.Single(t => t.Id == radio.Id).Estado);
        Assert.Equal(EstadoTrabajo.Cancelado, trabajos.Single(t => t.Id == altavoz.Id).Estado);
        Assert.Contains("versión 2 (Update)", trabajos.Single(t => t.Id == altavoz.Id).Resultado);
    }

    [Fact]
    public async Task Difundir_la_primera_version_no_detiene_nada()
    {
        await using var db = Contexto();
        var v1 = Encolar(db, 2);
        var detenidos = await new DetenedorDespachosAnteriores(db, _versiones, new RelojFijo(T0))
            .DetenerAsync(new VersionAprobada(v1.Id, v1.AvisoId, "t", "t", "t", [], false));
        Assert.Equal(0, detenidos);
        Assert.All(await db.TrabajosDifusion.ToListAsync(), t => Assert.Equal(EstadoTrabajo.Pendiente, t.Estado));
    }
}
