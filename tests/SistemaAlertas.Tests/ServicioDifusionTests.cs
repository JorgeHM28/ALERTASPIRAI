using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Infrastructure.Difusion;

namespace SistemaAlertas.Tests;

/// <summary>Facade + Command: ServicioDifusion coordina web, SMS y tareas manuales; un canal caído no frena a los demás.</summary>
public class ServicioDifusionTests
{
    private static readonly Guid Zona = Guid.NewGuid();

    private static VersionAprobada Version(bool esPrueba = false, params Guid[] zonas) => new(Guid.NewGuid(), Guid.NewGuid(),
        "Alerta roja río Piraí", "Alerta roja. Evacúe.", "Alerta roja río Piraí. Evacúe hacia zonas altas.",
        zonas.Length == 0 ? [Zona] : zonas, esPrueba);

    private sealed class FuenteFalsa(VersionAprobada? version) : IFuenteVersionesAprobadas
    {
        public Task<VersionAprobada?> ObtenerAsync(Guid id, CancellationToken ct = default) => Task.FromResult(version);
    }

    private sealed class WebQueFalla : IPublicadorWeb
    {
        public Task<PublicacionWeb> PublicarAsync(VersionAprobada v, CancellationToken ct = default) =>
            throw new HttpRequestException("El servidor web no responde.");
    }

    private sealed class DestinatariosFalsos(params DestinatarioSms[] lista) : IDestinatariosDifusion
    {
        public Task<IReadOnlyList<DestinatarioSms>> ObtenerSmsAsync(IReadOnlyCollection<Guid> zonaIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DestinatarioSms>>(lista);
    }

    private sealed class ColaFalsa : IColaSms
    {
        public List<TrabajoSms> Trabajos { get; } = [];
        public Task<int> EncolarAsync(IReadOnlyCollection<TrabajoSms> trabajos, CancellationToken ct = default)
        { Trabajos.AddRange(trabajos); return Task.FromResult(trabajos.Count); }
    }

    private sealed class IncidenciasFalsas : IRegistroIncidencias
    {
        public List<IncidenciaDifusion> Registradas { get; } = [];
        public Task RegistrarAsync(IncidenciaDifusion i, CancellationToken ct = default) { Registradas.Add(i); return Task.CompletedTask; }
    }

    private static readonly DestinatarioSms[] DosContactos =
        [new(Guid.NewGuid(), "Juana", "+59171234589"), new(Guid.NewGuid(), "Mario", "+59176543210")];

    private static (ServicioDifusion Servicio, ColaFalsa Cola, GestorTareasManualesEnMemoria Tareas, IncidenciasFalsas Incidencias)
        Armar(VersionAprobada? version, IPublicadorWeb? web = null, IDestinatariosDifusion? destinatarios = null)
    {
        var cola = new ColaFalsa();
        var tareas = new GestorTareasManualesEnMemoria(TimeProvider.System);
        var incidencias = new IncidenciasFalsas();
        IComandoDifusion[] comandos =
        [
            new PublicarEnWeb(web ?? new PublicadorWebEnMemoria(TimeProvider.System)),
            new EncolarSms(destinatarios ?? new DestinatariosFalsos(DosContactos), cola),
            new CrearTareasManuales(tareas)
        ];
        var servicio = new ServicioDifusion(new FuenteFalsa(version), comandos, incidencias);
        return (servicio, cola, tareas, incidencias);
    }

    [Fact]
    public async Task Difunde_por_web_sms_y_crea_las_tareas_manuales()
    {
        var version = Version();
        var (servicio, cola, _, incidencias) = Armar(version);

        var r = await servicio.DifundirAsync(version.VersionId);

        Assert.True(r.PublicadoEnWeb);
        Assert.Equal($"/avisos/{version.VersionId}", r.UrlWeb);
        Assert.Equal(2, r.TrabajosSms);
        Assert.All(cola.Trabajos, t => Assert.Equal(version.TextoSms, t.Mensaje.Texto));
        Assert.Equal([CanalManual.Radio, CanalManual.Altavoz, CanalManual.RedesSociales], r.TareasManuales.Select(t => t.Canal));
        Assert.Empty(r.Incidencias);
        Assert.Empty(incidencias.Registradas);
    }

    [Fact]
    public async Task Si_falla_la_web_registra_la_incidencia_y_sigue_con_sms_y_tareas()
    {
        var version = Version();
        var (servicio, cola, _, incidencias) = Armar(version, web: new WebQueFalla());

        var r = await servicio.DifundirAsync(version.VersionId);

        Assert.False(r.PublicadoEnWeb);
        Assert.Equal(2, r.TrabajosSms);
        Assert.Equal(2, cola.Trabajos.Count);
        Assert.Equal(3, r.TareasManuales.Count);
        var incidencia = Assert.Single(incidencias.Registradas);
        Assert.Equal("Web", incidencia.Canal);
        Assert.Contains("no responde", incidencia.Detalle);
    }

    [Fact]
    public async Task Un_aviso_de_prueba_viaja_marcado_en_sms_y_tareas()
    {
        var version = Version(esPrueba: true);
        var (servicio, cola, _, _) = Armar(version);

        var r = await servicio.DifundirAsync(version.VersionId);

        Assert.True(r.EsPrueba);
        Assert.All(cola.Trabajos, t => Assert.True(t.Mensaje.EsPrueba));
        Assert.All(r.TareasManuales, t => Assert.StartsWith("[PRUEBA]", t.Instrucciones));
    }

    [Fact]
    public async Task Sin_destinatarios_no_crea_sms_pero_deja_incidencia_y_crea_las_tareas()
    {
        var version = Version();
        var (servicio, cola, _, _) = Armar(version, destinatarios: new DestinatariosFalsos());

        var r = await servicio.DifundirAsync(version.VersionId);

        Assert.Equal(0, r.TrabajosSms);
        Assert.Empty(cola.Trabajos);
        Assert.Equal("SMS", Assert.Single(r.Incidencias).Canal);
        Assert.Equal(3, r.TareasManuales.Count);
        Assert.True(r.PublicadoEnWeb);
    }

    [Fact]
    public async Task Un_aviso_sin_zonas_no_manda_sms_a_todos()
    {
        var version = Version() with { ZonaIds = [] };
        var (servicio, cola, _, _) = Armar(version);

        var r = await servicio.DifundirAsync(version.VersionId);

        Assert.Equal(0, r.TrabajosSms);
        Assert.Empty(cola.Trabajos);
    }

    [Fact]
    public async Task Una_version_inexistente_no_se_difunde()
    {
        var (servicio, _, _, _) = Armar(null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servicio.DifundirAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task La_cola_en_memoria_entrega_los_trabajos_en_orden()
    {
        var cola = new ColaSmsEnMemoria();
        var version = Version();
        var trabajos = DosContactos.Select(d => new TrabajoSms(Guid.NewGuid(), version.VersionId, d.ContactoId,
            new Application.Difusion.Sms.MensajeSms(d.Telefono, "x"))).ToList();

        await cola.EncolarAsync(trabajos);

        Assert.True(cola.Pendientes.TryRead(out var primero));
        Assert.Equal(trabajos[0].Id, primero.Id);
    }
}
