using NetTopologySuite.Geometries;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Mensajes;
using SistemaAlertas.Infrastructure.Difusion;

namespace SistemaAlertas.Tests;

/// <summary>Patrones de comportamiento del Sprint 2: State, Chain of Responsibility, Observer, Command, Strategy y Template Method.</summary>
public class PatronesComportamientoTests
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);
    private static readonly Polygon Area = Geo.CreatePolygon([new(-63.25, -17.8), new(-63.2, -17.8), new(-63.2, -17.75), new(-63.25, -17.8)]);
    private static readonly DateTimeOffset Ahora = DateTimeOffset.UtcNow;

    private static Aviso NuevoAviso(DateTimeOffset? expira = null, string? instruccion = "Evacuar", EstadoCap estado = EstadoCap.Actual) =>
        new(new MensajeCapBuilder().DeRemitente("coed@santacruz.gob.bo").ConEstado(estado)
            .AgregarInfo(i => i.ConEvento("Crecida").ConUrgencia(UrgenciaCap.Immediate).ConSeveridad(SeveridadCap.Severe)
                .ConCerteza(CertezaCap.Observed).ConTitular("Alerta roja").ConInstruccion(instruccion)
                .ExpiraEn(expira ?? DateTimeOffset.UtcNow.AddHours(6)))
            .Build(), FormatoAviso.Manual, "<alert/>", Area, Guid.NewGuid());

    // ---------- State ----------

    [Fact]
    public void State_recorre_registrado_aprobado_difundido()
    {
        var aviso = NuevoAviso();
        Assert.IsType<EstadoRegistrado>(aviso.EstadoActual);
        Assert.False(aviso.EstadoActual.PermiteDifundir);

        aviso.Aprobar();
        Assert.Equal(EstadoAviso.Aprobado, aviso.Estado);
        Assert.True(aviso.EstadoActual.PermiteDifundir);

        aviso.MarcarDifundido();
        Assert.Equal(EstadoAviso.Difundido, aviso.Estado);
        aviso.MarcarDifundido(); // volver a difundir está permitido
        Assert.Equal(EstadoAviso.Difundido, aviso.Estado);
    }

    [Fact]
    public void State_aprueba_un_aviso_de_prueba_completo()
    {
        var aviso = NuevoAviso(estado: EstadoCap.Exercise);
        Assert.False(aviso.AptoParaDifusion);
        aviso.Aprobar();
        Assert.Equal(EstadoAviso.Aprobado, aviso.Estado);
    }

    [Fact]
    public void State_rechaza_las_transiciones_que_no_corresponden()
    {
        var registrado = NuevoAviso();
        Assert.Throws<ExcepcionDominio>(registrado.MarcarDifundido);

        var difundido = NuevoAviso();
        difundido.Aprobar();
        difundido.MarcarDifundido();
        var ex = Assert.Throws<ExcepcionDominio>(difundido.Descartar);
        Assert.Contains("Cancel", ex.Message);

        var descartado = NuevoAviso();
        descartado.Descartar();
        Assert.Throws<ExcepcionDominio>(descartado.Aprobar);
        Assert.Throws<ExcepcionDominio>(descartado.Descartar);
    }

    // ---------- Chain of Responsibility ----------

    private static VersionMensaje VersionAprobadaDe(Aviso aviso)
    {
        var contenido = new ContenidoMensaje(aviso.Evento, aviso.Urgencia, aviso.Severidad, aviso.Certeza, aviso.Titular,
            aviso.Descripcion, aviso.Instruccion, aviso.Vigente, aviso.Expira, "Zona");
        var v = new VersionMensaje(aviso.Id, aviso.Id, 1, TipoMensajeCap.Alert, contenido, Area, EstadoCap.Actual, "sat", Guid.NewGuid());
        v.VincularProtocolo(new ProtocoloOficial("Evacuación", aviso.Instruccion ?? "Evacuar a la zona alta.", "COED Santa Cruz",
            "Plan 01/2026", new DateOnly(2026, 1, 15)));
        v.EnviarARevision("<alert/>", Ahora, Ahora);
        v.Aprobar(Guid.NewGuid(), Ahora, null);
        return v;
    }

    [Fact]
    public void Cadena_deja_pasar_una_version_aprobada_vigente_y_completa()
    {
        var aviso = NuevoAviso();
        ReglaDifusion.CadenaPorDefecto().Verificar(VersionAprobadaDe(aviso), aviso, Ahora);
    }

    [Fact]
    public void Cadena_corta_en_el_primer_eslabon_que_rechaza()
    {
        var aviso = NuevoAviso();
        var borrador = new VersionMensaje(aviso.Id, aviso.Id, 1, TipoMensajeCap.Alert,
            new ContenidoMensaje("Crecida", UrgenciaCap.Immediate, SeveridadCap.Severe, CertezaCap.Observed, "T", null, "Evacuar",
                null, Ahora.AddHours(6), "Zona"), Area, EstadoCap.Actual, "sat", Guid.NewGuid());
        var ex = Assert.Throws<ExcepcionDominio>(() => ReglaDifusion.CadenaPorDefecto().Verificar(borrador, aviso, Ahora));
        Assert.Contains("aprobador", ex.Message);

        var descartado = NuevoAviso();
        var version = VersionAprobadaDe(descartado);
        descartado.Descartar();
        ex = Assert.Throws<ExcepcionDominio>(() => ReglaDifusion.CadenaPorDefecto().Verificar(version, descartado, Ahora));
        Assert.Contains("descartado", ex.Message); // el primer eslabón responde antes que los demás
    }

    [Fact]
    public void Cadena_detecta_una_version_que_vencio_despues_de_aprobarse()
    {
        var aviso = NuevoAviso(expira: DateTimeOffset.UtcNow.AddMinutes(30));
        var version = VersionAprobadaDe(aviso);
        var ex = Assert.Throws<ExcepcionDominio>(() => ReglaDifusion.CadenaPorDefecto().Verificar(version, aviso, Ahora.AddHours(1)));
        Assert.Contains("venció", ex.Message);
    }

    private sealed class ReglaHorario : ReglaDifusion
    {
        protected override string? Revisar(VersionMensaje version, Aviso aviso, DateTimeOffset ahora) => "Fuera de horario.";
    }

    [Fact]
    public void Cadena_admite_un_eslabon_nuevo_sin_tocar_los_demas()
    {
        var aviso = NuevoAviso();
        var version = VersionAprobadaDe(aviso);
        var cadena = new ReglaNoDescartado();
        cadena.Luego(new ReglaAprobado()).Luego(new ReglaHorario());
        Assert.Equal("Fuera de horario.", Assert.Throws<ExcepcionDominio>(() => cadena.Verificar(version, aviso, Ahora)).Message);
    }

    // ---------- Observer ----------

    private sealed class ObservadorQueFalla : IObservadorAvisos
    {
        public Task AlOcurrirAsync(EventoAviso e, CancellationToken ct = default) => throw new InvalidOperationException("caído");
    }

    [Fact]
    public async Task Observer_avisa_a_todos_aunque_uno_falle()
    {
        var bitacora = new BitacoraAvisosEnMemoria();
        var publicador = new PublicadorEventosAvisos([new ObservadorQueFalla(), bitacora]);
        var avisoId = Guid.NewGuid();

        await publicador.PublicarAsync(new EventoAviso(avisoId, TipoEventoAviso.Aprobado, Guid.NewGuid(), "ok", Ahora));
        await publicador.PublicarAsync(new EventoAviso(avisoId, TipoEventoAviso.Difundido, null, "ok", Ahora));

        Assert.Equal([TipoEventoAviso.Aprobado, TipoEventoAviso.Difundido], bitacora.De(avisoId).Select(e => e.Tipo));
        Assert.Empty(bitacora.De(Guid.NewGuid()));
    }

    // ---------- Command (con Observer al final) ----------

    private sealed class FuenteFija(VersionAprobada v) : IFuenteVersionesAprobadas
    {
        public Task<VersionAprobada?> ObtenerAsync(Guid id, CancellationToken ct = default) => Task.FromResult<VersionAprobada?>(v);
    }

    private sealed class ComandoEspia(string canal, List<string> orden, bool falla = false) : IComandoDifusion
    {
        public string Canal => canal;
        public Task EjecutarAsync(ContextoDifusion c, CancellationToken ct = default)
        {
            orden.Add(canal);
            if (falla) throw new InvalidOperationException("sin señal");
            c.TrabajosSms += 1;
            return Task.CompletedTask;
        }
    }

    private sealed class SinIncidencias : IRegistroIncidencias
    {
        public Task RegistrarAsync(IncidenciaDifusion i, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static VersionAprobada Version() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Alerta roja", "Alerta roja. Evacúe.", "Alerta roja. Evacúe.", [Guid.NewGuid()], false);

    [Fact]
    public async Task Command_el_invocador_ejecuta_cualquier_canal_registrado_en_orden_y_aisla_fallos()
    {
        var orden = new List<string>();
        var bitacora = new BitacoraAvisosEnMemoria();
        var version = Version();
        var servicio = new ServicioDifusion(new FuenteFija(version),
            [new ComandoEspia("WhatsApp", orden, falla: true), new ComandoEspia("Sirena", orden)],
            new SinIncidencias(), new PublicadorEventosAvisos([bitacora]));

        var r = await servicio.DifundirAsync(version.VersionId);

        Assert.Equal(["WhatsApp", "Sirena"], orden);
        Assert.Equal(1, r.TrabajosSms);
        Assert.Equal("WhatsApp", Assert.Single(r.Incidencias).Canal);
        Assert.Equal(TipoEventoAviso.Difundido, Assert.Single(bitacora.De(version.AvisoId)).Tipo);
    }

    // ---------- Strategy ----------

    private sealed class RedaccionRadioCorta : IRedaccionCanal
    {
        public CanalManual Canal => CanalManual.Radio;
        public string Redactar(VersionAprobada v) => "CORTO: " + v.Titular;
    }

    [Fact]
    public async Task Strategy_cada_canal_redacta_a_su_manera_y_se_puede_cambiar()
    {
        var v = Version();
        var porDefecto = await new GestorTareasManualesEnMemoria(TimeProvider.System)
            .CrearAsync(v, CrearTareasManuales.Canales);
        Assert.Contains("dos veces", porDefecto.Single(t => t.Canal == CanalManual.Radio).Instrucciones);
        Assert.Contains("cada 10 minutos", porDefecto.Single(t => t.Canal == CanalManual.Altavoz).Instrucciones);
        Assert.Contains($"/?aviso={v.VersionId}", porDefecto.Single(t => t.Canal == CanalManual.RedesSociales).Instrucciones);

        var otra = await new GestorTareasManualesEnMemoria(TimeProvider.System, [new RedaccionRadioCorta()])
            .CrearAsync(v, [CanalManual.Radio]);
        Assert.Equal("CORTO: Alerta roja", Assert.Single(otra).Instrucciones);
    }

    // ---------- Template Method ----------

    [Fact]
    public void Template_method_los_dos_formatos_siguen_los_mismos_pasos_y_terminan_en_el_builder()
    {
        Assert.IsAssignableFrom<ImportadorCapBase<System.Xml.Linq.XElement, System.Xml.Linq.XElement>>(new ImportadorCapXml());
        Assert.IsAssignableFrom<ImportadorCapBase<ImportadorCapJson.AlertJson, ImportadorCapJson.InfoJson>>(new ImportadorCapJson());

        // El paso final es el Builder: un JSON sin <info> no pasa su validación, igual que un XML.
        const string json = """{"identifier":"x-1","sender":"coed@santacruz.gob.bo","sent":"2026-11-13T10:00:00-04:00","status":"Actual","msgType":"Alert","scope":"Public"}""";
        Assert.Throws<ExcepcionDominio>(() => new ImportadorCapJson().Importar(json));
    }
}
