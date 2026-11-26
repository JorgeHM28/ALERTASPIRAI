using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Tests;

/// <summary>Sprint 3: reglas de los trabajos de difusión (RF-04), reintentos (RNF-08) y CAP emitido.</summary>
public class TrabajosDifusionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 12, 1, 12, 0, 0, TimeSpan.Zero);

    private static EnvioSms Envio() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "+59170000001", "ALERTA ROJA: evacúe.", false, T0);

    private static ResultadoIntento Ok => new(true, "SM123", null, false, false);
    private static ResultadoIntento Pasajero => new(false, null, "Proveedor saturado", true, false);
    private static ResultadoIntento Definitivo => new(false, null, "Número inválido", false, false);

    [Fact]
    public void Un_envio_nuevo_esta_pendiente_y_listo_para_enviar_ya()
    {
        var e = Envio();
        Assert.Equal(EstadoTrabajo.Pendiente, e.Estado);
        Assert.Equal(CanalDifusion.Sms, e.Canal);
        Assert.Equal(T0, e.ProximoIntento);
        Assert.Equal(0, e.Intentos);
    }

    [Fact]
    public void Enviado_al_primer_intento_guarda_el_codigo_del_proveedor()
    {
        var e = Envio();
        e.RegistrarIntento(Ok, T0.AddSeconds(2));
        Assert.Equal(EstadoTrabajo.Enviado, e.Estado);
        Assert.Equal("SM123", e.CodigoProveedor);
        Assert.Null(e.ProximoIntento);
        Assert.Equal(1, e.Intentos);
    }

    [Fact]
    public void RNF08_un_fallo_recuperable_se_reintenta_a_los_60_segundos_como_maximo_dos_veces()
    {
        var e = Envio();
        e.RegistrarIntento(Pasajero, T0);
        Assert.Equal(EstadoTrabajo.Pendiente, e.Estado);
        Assert.Equal(T0.AddSeconds(60), e.ProximoIntento);

        e.RegistrarIntento(Pasajero, T0.AddSeconds(60));
        Assert.Equal(EstadoTrabajo.Pendiente, e.Estado);
        Assert.Equal(T0.AddSeconds(120), e.ProximoIntento);

        e.RegistrarIntento(Pasajero, T0.AddSeconds(120));
        Assert.Equal(EstadoTrabajo.Fallido, e.Estado);
        Assert.Equal(3, e.Intentos);
        Assert.Null(e.ProximoIntento);
        Assert.Contains("3 intentos", e.Resultado);
    }

    [Fact]
    public void RNF08_si_el_reintento_funciona_queda_enviado()
    {
        var e = Envio();
        e.RegistrarIntento(Pasajero, T0);
        e.RegistrarIntento(Ok, T0.AddSeconds(60));
        Assert.Equal(EstadoTrabajo.Enviado, e.Estado);
        Assert.Equal(2, e.Intentos);
        Assert.Equal("Proveedor saturado", e.UltimoError);
    }

    [Fact]
    public void RNF08_un_fallo_definitivo_no_se_reintenta()
    {
        var e = Envio();
        e.RegistrarIntento(Definitivo, T0);
        Assert.Equal(EstadoTrabajo.Fallido, e.Estado);
        Assert.Equal(1, e.Intentos);
        Assert.Contains("no se reintenta", e.Resultado);
    }

    [Fact]
    public void Un_aviso_de_prueba_bloqueado_termina_sin_reintentos()
    {
        var e = Envio();
        e.RegistrarIntento(new ResultadoIntento(false, null, "Aviso de prueba", false, true), T0);
        Assert.Equal(EstadoTrabajo.Bloqueado, e.Estado);
    }

    [Fact]
    public void No_se_registra_un_intento_sobre_un_envio_terminado()
    {
        var e = Envio();
        e.RegistrarIntento(Ok, T0);
        Assert.Throws<ExcepcionDominio>(() => e.RegistrarIntento(Ok, T0));
    }

    [Fact]
    public void La_configuracion_no_puede_pasar_el_maximo_de_dos_reintentos()
    {
        var e = Envio();
        for (var i = 0; i < 3; i++) e.RegistrarIntento(Pasajero, T0.AddMinutes(i), maxReintentos: 10);
        Assert.Equal(EstadoTrabajo.Fallido, e.Estado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Un_envio_necesita_telefono(string telefono) =>
        Assert.Throws<ExcepcionDominio>(() => new EnvioSms(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), telefono, "x", false, T0));

    private static TareaManual Tarea() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CanalDifusion.Altavoz, "Anunciar: ALERTA ROJA", false, T0);

    [Fact]
    public void Una_tarea_se_confirma_con_quien_la_hizo()
    {
        var t = Tarea();
        var u = Guid.NewGuid();
        t.Confirmar(u, "Se anunció 3 veces", T0.AddMinutes(10));
        Assert.Equal(EstadoTrabajo.Confirmada, t.Estado);
        Assert.Equal(u, t.ConfirmadoPorId);
        Assert.Equal("Se anunció 3 veces", t.Observacion);
        Assert.Throws<ExcepcionDominio>(() => t.Confirmar(u, null, T0));
    }

    [Fact]
    public void Marcar_una_tarea_como_no_realizada_exige_el_motivo()
    {
        var t = Tarea();
        Assert.Throws<ExcepcionDominio>(() => t.MarcarNoRealizada(Guid.NewGuid(), " ", T0));
        t.MarcarNoRealizada(Guid.NewGuid(), "Sin energía en la comunidad", T0);
        Assert.Equal(EstadoTrabajo.NoRealizada, t.Estado);
    }

    [Fact]
    public void El_sms_no_es_una_tarea_manual() =>
        Assert.Throws<ExcepcionDominio>(() => new TareaManual(Guid.NewGuid(), Guid.NewGuid(), null, CanalDifusion.Sms, "x", false, T0));

    [Fact]
    public void Un_cap_emitido_debe_ser_valido_y_un_update_referencia_al_previo()
    {
        CapEmitido Cap(string tipo, bool valido, Guid? referencia) => new(Guid.NewGuid(), Guid.NewGuid(), "id", "s", T0, "Actual", tipo,
            "<alert/>", new string('0', 64), valido, referencia, T0);

        Assert.Throws<ExcepcionDominio>(() => Cap("Alert", false, null));
        Assert.Throws<ExcepcionDominio>(() => Cap("Update", true, null));
        Assert.Throws<ExcepcionDominio>(() => Cap("Cancel", true, null));
        Assert.Equal("Update", Cap("Update", true, Guid.NewGuid()).Tipo);
        Assert.Null(Cap("Alert", true, null).ReferenciaCapId);
    }
}
