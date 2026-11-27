using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Tests;

/// <summary>Facade + Command: ServicioDifusion emite el CAP y coordina web, SMS y tareas manuales; un canal caído no frena a los demás.</summary>
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

    internal sealed class ArchivoCapFalso(bool valido = true) : IArchivoCap
    {
        public List<Guid> Emitidos { get; } = [];
        public Task<CapEmitido> EmitirAsync(VersionAprobada v, CancellationToken ct = default)
        {
            if (!valido) throw new ExcepcionDominio("El CAP final no es válido contra CAP 1.2 y no se difunde: falta <info>.");
            Emitidos.Add(v.VersionId);
            return Task.FromResult(new CapEmitido(v.VersionId, v.AvisoId, "SAT-PIRAI-1", "sat@pirai", DateTimeOffset.UtcNow, "Actual",
                "Alert", "<alert/>", new string('a', 64), true, null, DateTimeOffset.UtcNow));
        }
    }

    private sealed class WebFalsa : IPublicadorWeb
    {
        public Task<PublicacionWeb> PublicarAsync(VersionAprobada v, CancellationToken ct = default) =>
            Task.FromResult(new PublicacionWeb(v.VersionId, $"/?aviso={v.VersionId}", DateTimeOffset.UtcNow));
    }

    private sealed class WebQueFalla : IPublicadorWeb
    {
        public Task<PublicacionWeb> PublicarAsync(VersionAprobada v, CancellationToken ct = default) =>
            throw new HttpRequestException("El servidor web no responde.");
    }

    private sealed class DestinatariosFalsos(params DestinatarioSms[] lista) : IDestinatariosDifusion
    {
        public Task<IReadOnlyList<DestinatarioSms>> ObtenerSmsAsync(VersionAprobada version, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DestinatarioSms>>(lista);
        public Task<IReadOnlyList<DestinatarioEmail>> ObtenerEmailAsync(VersionAprobada version, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DestinatarioEmail>>(lista.Select(d => new DestinatarioEmail(d.ContactoId, d.ComunidadId, d.Nombre,
                $"{d.Nombre.ToLowerInvariant()}@ejemplo.bo")).ToList());
    }

    private sealed class ColaFalsa : IColaSms
    {
        public List<EnvioAutomatico> Envios { get; } = [];
        public Task<int> EncolarAsync(IReadOnlyCollection<EnvioAutomatico> envios, CancellationToken ct = default)
        { Envios.AddRange(envios); return Task.FromResult(envios.Count); }
    }

    /// <summary>Una tarea por canal, con el texto del redactor real (Strategy).</summary>
    private sealed class TareasFalsas : IGestorTareasManuales
    {
        private readonly RedactorTareas _redactor = new();
        public List<TareaManual> Creadas { get; } = [];
        public Task<IReadOnlyList<TareaManual>> CrearAsync(VersionAprobada v, IReadOnlyCollection<CanalDifusion> canales, CancellationToken ct = default)
        {
            var nuevas = canales.Select(c => new TareaManual(v.VersionId, v.AvisoId, null, c, _redactor.Redactar(c, v), v.EsPrueba, DateTimeOffset.UtcNow)).ToList();
            Creadas.AddRange(nuevas);
            return Task.FromResult<IReadOnlyList<TareaManual>>(nuevas);
        }
    }

    private sealed class IncidenciasFalsas : IRegistroIncidencias
    {
        public List<IncidenciaDifusion> Registradas { get; } = [];
        public Task RegistrarAsync(IncidenciaDifusion i, CancellationToken ct = default) { Registradas.Add(i); return Task.CompletedTask; }
    }

    private static readonly DestinatarioSms[] DosContactos =
        [new(Guid.NewGuid(), Guid.NewGuid(), "Juana", "+59171234589"), new(Guid.NewGuid(), Guid.NewGuid(), "Mario", "+59176543210")];

    private static (ServicioDifusion Servicio, ColaFalsa Cola, TareasFalsas Tareas, IncidenciasFalsas Incidencias, ArchivoCapFalso Cap)
        Armar(VersionAprobada? version, IPublicadorWeb? web = null, IDestinatariosDifusion? destinatarios = null, bool capValido = true)
    {
        var cola = new ColaFalsa();
        var tareas = new TareasFalsas();
        var incidencias = new IncidenciasFalsas();
        var cap = new ArchivoCapFalso(capValido);
        IComandoDifusion[] comandos =
        [
            new PublicarEnWeb(web ?? new WebFalsa()),
            new EncolarSms(destinatarios ?? new DestinatariosFalsos(DosContactos), cola),
            new CrearTareasManuales(tareas)
        ];
        var servicio = new ServicioDifusion(new FuenteFalsa(version), cap, comandos, incidencias);
        return (servicio, cola, tareas, incidencias, cap);
    }

    [Fact]
    public async Task Emite_el_cap_y_difunde_por_web_sms_y_tareas_manuales()
    {
        var version = Version();
        var (servicio, cola, tareas, incidencias, cap) = Armar(version);

        var r = await servicio.DifundirAsync(version.VersionId);

        Assert.Equal([version.VersionId], cap.Emitidos);
        Assert.True(r.PublicadoEnWeb);
        Assert.Equal($"/?aviso={version.VersionId}", r.UrlWeb);
        Assert.Equal(2, r.TrabajosSms);
        Assert.All(cola.Envios, e => { Assert.Equal(version.TextoSms, e.Texto); Assert.Equal(EstadoTrabajo.Pendiente, e.Estado); });
        Assert.Equal([CanalDifusion.Radio, CanalDifusion.Altavoz, CanalDifusion.RedesSociales], tareas.Creadas.Select(t => t.Canal));
        Assert.Equal(3, r.TareasManuales);
        Assert.Empty(r.Incidencias);
        Assert.Empty(incidencias.Registradas);
    }

    [Fact]
    public async Task Si_el_cap_final_no_es_valido_no_se_difunde_por_ningun_canal()
    {
        var version = Version();
        var (servicio, cola, tareas, _, _) = Armar(version, capValido: false);

        var ex = await Assert.ThrowsAsync<ExcepcionDominio>(() => servicio.DifundirAsync(version.VersionId));

        Assert.Contains("no es válido", ex.Message);
        Assert.Empty(cola.Envios);
        Assert.Empty(tareas.Creadas);
    }

    [Fact]
    public async Task Si_falla_la_web_registra_la_incidencia_y_sigue_con_sms_y_tareas()
    {
        var version = Version();
        var (servicio, cola, _, incidencias, _) = Armar(version, web: new WebQueFalla());

        var r = await servicio.DifundirAsync(version.VersionId);

        Assert.False(r.PublicadoEnWeb);
        Assert.Equal(2, r.TrabajosSms);
        Assert.Equal(2, cola.Envios.Count);
        Assert.Equal(3, r.TareasManuales);
        var incidencia = Assert.Single(incidencias.Registradas);
        Assert.Equal("Web", incidencia.Canal);
        Assert.Contains("no responde", incidencia.Detalle);
    }

    [Fact]
    public async Task Un_aviso_de_prueba_viaja_marcado_en_sms_y_tareas()
    {
        var version = Version(esPrueba: true);
        var (servicio, cola, tareas, _, _) = Armar(version);

        var r = await servicio.DifundirAsync(version.VersionId);

        Assert.True(r.EsPrueba);
        Assert.All(cola.Envios, e => Assert.True(e.EsPrueba));
        Assert.All(tareas.Creadas, t => Assert.StartsWith("[PRUEBA]", t.TextoLegible));
    }

    [Fact]
    public async Task Sin_destinatarios_no_crea_sms_pero_deja_incidencia_y_crea_las_tareas()
    {
        var version = Version();
        var (servicio, cola, _, _, _) = Armar(version, destinatarios: new DestinatariosFalsos());

        var r = await servicio.DifundirAsync(version.VersionId);

        Assert.Equal(0, r.TrabajosSms);
        Assert.Empty(cola.Envios);
        Assert.Equal("SMS", Assert.Single(r.Incidencias).Canal);
        Assert.Equal(3, r.TareasManuales);
        Assert.True(r.PublicadoEnWeb);
    }

    [Fact]
    public async Task Una_version_inexistente_no_se_difunde()
    {
        var (servicio, _, _, _, cap) = Armar(null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => servicio.DifundirAsync(Guid.NewGuid()));
        Assert.Empty(cap.Emitidos);
    }
}
