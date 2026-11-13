using NetTopologySuite.Geometries;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;
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
    public void State_no_aprueba_un_aviso_incompleto_y_dice_por_que()
    {
        var aviso = NuevoAviso(instruccion: null);
        var ex = Assert.Throws<ExcepcionDominio>(aviso.Aprobar);
        Assert.Contains("instrucción", ex.Message);
        Assert.Equal(EstadoAviso.Registrado, aviso.Estado);
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

    [Fact]
    public void Cadena_deja_pasar_un_aviso_aprobado_vigente_y_completo()
    {
        var aviso = NuevoAviso();
        aviso.Aprobar();
        ReglaDifusion.CadenaPorDefecto().Verificar(aviso, Ahora);
    }

    [Fact]
    public void Cadena_corta_en_el_primer_eslabon_que_rechaza()
    {
        var sinAprobar = NuevoAviso();
        var ex = Assert.Throws<ExcepcionDominio>(() => ReglaDifusion.CadenaPorDefecto().Verificar(sinAprobar, Ahora));
        Assert.Contains("aprobador", ex.Message);

        var descartado = NuevoAviso();
        descartado.Descartar();
        ex = Assert.Throws<ExcepcionDominio>(() => ReglaDifusion.CadenaPorDefecto().Verificar(descartado, Ahora));
        Assert.Contains("descartado", ex.Message); // el primer eslabón responde antes que el de aprobación
    }

    [Fact]
    public void Cadena_detecta_un_aviso_que_vencio_despues_de_aprobarse()
    {
        var aviso = NuevoAviso(expira: DateTimeOffset.UtcNow.AddMinutes(30));
        aviso.Aprobar();
        var ex = Assert.Throws<ExcepcionDominio>(() => ReglaDifusion.CadenaPorDefecto().Verificar(aviso, Ahora.AddHours(1)));
        Assert.Contains("venció", ex.Message);
    }

    private sealed class ReglaHorario : ReglaDifusion
    {
        protected override string? Revisar(Aviso aviso, DateTimeOffset ahora) => "Fuera de horario.";
    }

    [Fact]
    public void Cadena_admite_un_eslabon_nuevo_sin_tocar_los_demas()
    {
        var aviso = NuevoAviso();
        aviso.Aprobar();
        var cadena = new ReglaNoDescartado();
        cadena.Luego(new ReglaAprobado()).Luego(new ReglaHorario());
        Assert.Equal("Fuera de horario.", Assert.Throws<ExcepcionDominio>(() => cadena.Verificar(aviso, Ahora)).Message);
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
        Assert.Contains($"/avisos/{v.VersionId}", porDefecto.Single(t => t.Canal == CanalManual.RedesSociales).Instrucciones);

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
