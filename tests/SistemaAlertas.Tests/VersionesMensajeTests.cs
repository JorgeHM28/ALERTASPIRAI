using NetTopologySuite.Geometries;
using SistemaAlertas.Application.Avisos;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Application.Mensajes;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Mensajes;

namespace SistemaAlertas.Tests;

/// <summary>Sprint 2: RF-02 (versiones Alert/Update/Cancel), RF-11 (aprobar o rechazar), RF-07 (destinos) y RF-06 (nivel en texto).</summary>
public class VersionesMensajeTests
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);
    private static readonly Polygon Area = Geo.CreatePolygon([new(-63.25, -17.8), new(-63.2, -17.8), new(-63.2, -17.75), new(-63.25, -17.8)]);
    private static readonly DateTimeOffset Ahora = DateTimeOffset.UtcNow;
    private static readonly Guid Operador = Guid.NewGuid();
    private static readonly Guid Aprobador = Guid.NewGuid();

    private static ContenidoMensaje Contenido(string? instruccion = "Evacuar a la zona alta.", DateTimeOffset? expira = null,
        SeveridadCap severidad = SeveridadCap.Severe) =>
        new("Crecida del río Piraí", UrgenciaCap.Immediate, severidad, CertezaCap.Observed, "Crecida en Porongo",
            "El río sube 40 cm por hora.", instruccion, Ahora, expira ?? Ahora.AddHours(6), "Porongo");

    private static VersionMensaje Borrador(Guid? cadena = null, ContenidoMensaje? contenido = null) =>
        new(cadena ?? Guid.NewGuid(), cadena ?? Guid.NewGuid(), 1, TipoMensajeCap.Alert, contenido ?? Contenido(), Area,
            EstadoCap.Actual, "sat@santacruz.gob.bo", Operador);

    private static VersionMensaje Aprobada(Guid? cadena = null)
    {
        var id = cadena ?? Guid.NewGuid();
        var v = new VersionMensaje(id, id, 1, TipoMensajeCap.Alert, Contenido(), Area, EstadoCap.Actual, "sat@santacruz.gob.bo", Operador);
        v.EnviarARevision("<alert/>", Ahora, Ahora);
        v.Aprobar(Aprobador, Ahora, null);
        return v;
    }

    // ---------- RF-02: preparación y corrección ----------

    [Fact]
    public void Una_version_incompleta_queda_como_borrador()
    {
        var v = Borrador(contenido: Contenido(instruccion: null));
        var ex = Assert.Throws<ExcepcionDominio>(() => v.EnviarARevision("<alert/>", Ahora, Ahora));
        Assert.Contains("borrador", ex.Message);
        Assert.Contains("instrucciones", ex.Message);
        Assert.Equal(EstadoVersion.Borrador, v.Estado);
        Assert.Null(v.XmlCap);
    }

    [Fact]
    public void Se_corrige_un_borrador_conservando_los_hechos_oficiales()
    {
        var v = Borrador(contenido: Contenido(instruccion: null));
        v.Redactar("Nuevo titular", null, "Diríjase al colegio de Porongo.");
        Assert.Equal("Nuevo titular", v.Contenido.Titular);
        Assert.Equal("Diríjase al colegio de Porongo.", v.Contenido.Instruccion);
        Assert.Equal(SeveridadCap.Severe, v.Contenido.Severidad);
        Assert.Equal("Crecida del río Piraí", v.Contenido.Evento);
        v.EnviarARevision("<alert/>", Ahora, Ahora);
        Assert.Equal(EstadoVersion.EnRevision, v.Estado);
    }

    [Fact]
    public void Una_version_en_revision_o_aprobada_no_se_edita()
    {
        var v = Borrador();
        v.EnviarARevision("<alert/>", Ahora, Ahora);
        Assert.Throws<ExcepcionDominio>(() => v.Redactar("x", "y", "z"));
        v.Aprobar(Aprobador, Ahora, null);
        var ex = Assert.Throws<ExcepcionDominio>(() => v.Redactar("x", "y", "z"));
        Assert.Contains("aprobada no se puede editar", ex.Message);
    }

    [Fact]
    public void Un_borrador_lo_elimina_quien_lo_preparo_o_un_administrador()
    {
        var v = Borrador();
        v.VerificarEliminable(Operador, esAdministrador: false);
        v.VerificarEliminable(Guid.NewGuid(), esAdministrador: true);
        var ex = Assert.Throws<UnauthorizedAccessException>(() => v.VerificarEliminable(Guid.NewGuid(), esAdministrador: false));
        Assert.Contains("quien preparó", ex.Message);
    }

    [Fact]
    public void Solo_se_eliminan_borradores_ni_en_revision_ni_rechazada_ni_aprobada()
    {
        var enRevision = Borrador();
        enRevision.EnviarARevision("<alert/>", Ahora, Ahora);
        Assert.Throws<ExcepcionDominio>(() => enRevision.VerificarEliminable(Operador, esAdministrador: true));

        var rechazada = Borrador();
        rechazada.EnviarARevision("<alert/>", Ahora, Ahora);
        rechazada.Rechazar(Aprobador, "Falta el punto de encuentro.", Ahora);
        var ex = Assert.Throws<ExcepcionDominio>(() => rechazada.VerificarEliminable(Operador, esAdministrador: true));
        Assert.Contains("solo se eliminan borradores", ex.Message);

        Assert.Throws<ExcepcionDominio>(() => Aprobada().VerificarEliminable(Operador, esAdministrador: true));
    }

    // ---------- RF-11: aprobación o rechazo ----------

    [Fact]
    public void Aprobar_guarda_identidad_y_fecha()
    {
        var v = Aprobada();
        Assert.Equal(EstadoVersion.Aprobada, v.Estado);
        Assert.Equal(Aprobador, v.RevisadoPorId);
        Assert.Equal(Ahora, v.RevisadoEn);
        Assert.True(v.EstadoActual.PermiteDifundir);
    }

    [Fact]
    public void Quien_preparo_la_version_no_puede_aprobarla()
    {
        var v = Borrador();
        v.EnviarARevision("<alert/>", Ahora, Ahora);
        var ex = Assert.Throws<ExcepcionDominio>(() => v.Aprobar(Operador, Ahora, null));
        Assert.Contains("no puede aprobarla", ex.Message);
        Assert.Equal(EstadoVersion.EnRevision, v.Estado);
        v.Aprobar(Aprobador, Ahora, null);
        Assert.Equal(EstadoVersion.Aprobada, v.Estado);
    }

    [Fact]
    public void Un_borrador_no_se_aprueba_sin_pasar_por_revision()
    {
        var v = Borrador();
        Assert.Throws<ExcepcionDominio>(() => v.Aprobar(Aprobador, Ahora, null));
    }

    [Fact]
    public void Se_exige_motivo_al_rechazar()
    {
        var v = Borrador();
        v.EnviarARevision("<alert/>", Ahora, Ahora);
        Assert.Throws<ExcepcionDominio>(() => v.Rechazar(Aprobador, null, Ahora));
        Assert.Throws<ExcepcionDominio>(() => v.Rechazar(Aprobador, "  ", Ahora));
        Assert.Equal(EstadoVersion.EnRevision, v.Estado);

        v.Rechazar(Aprobador, "La instrucción no indica el punto de reunión.", Ahora);
        Assert.Equal(EstadoVersion.Rechazada, v.Estado);
        Assert.Equal(Aprobador, v.RevisadoPorId);
        Assert.Equal("La instrucción no indica el punto de reunión.", v.MotivoRechazo);
    }

    [Fact]
    public void No_se_aprueba_si_vencio_mientras_esperaba_revision()
    {
        var v = Borrador(contenido: Contenido(expira: Ahora.AddMinutes(10)));
        v.EnviarARevision("<alert/>", Ahora, Ahora);
        var ex = Assert.Throws<ExcepcionDominio>(() => v.Aprobar(Aprobador, Ahora.AddHours(1), null));
        Assert.Contains("venció", ex.Message);
    }

    [Fact]
    public void Prototype_el_borrador_rechazado_vuelve_con_su_motivo_y_la_correccion_es_otra_version()
    {
        var v = Borrador();
        v.EnviarARevision("<alert/>", Ahora, Ahora);
        v.Rechazar(Aprobador, "Falta el punto de reunión.", Ahora);

        var nueva = v.Corregir(2, Operador);

        Assert.NotEqual(v.Id, nueva.Id);
        Assert.Equal(2, nueva.Numero);
        Assert.Equal(EstadoVersion.Borrador, nueva.Estado);
        Assert.Equal(v.Id, nueva.VersionAnteriorId);
        Assert.Equal("Falta el punto de reunión.", nueva.MotivoDevolucion);
        Assert.Equal(v.Contenido, nueva.Contenido);          // mismo contenido (record: igualdad por valor)
        Assert.NotSame(v.Contenido, nueva.Contenido);        // pero es un clon
        Assert.NotEqual(v.IdentificadorCap, nueva.IdentificadorCap);
        Assert.Equal(EstadoVersion.Rechazada, v.Estado);     // la rechazada queda intacta
        nueva.Redactar("Crecida en Porongo", null, "Reunirse en la plaza principal.");
        Assert.Equal("El río sube 40 cm por hora.", v.Contenido.Descripcion);
    }

    [Fact]
    public void Una_version_aprobada_no_se_corrige_se_prepara_un_update()
    {
        var v = Aprobada();
        var ex = Assert.Throws<ExcepcionDominio>(() => v.Corregir(2, Operador));
        Assert.Contains("Update", ex.Message);
    }

    // ---------- RF-02: Update y Cancel con referencias ----------

    [Fact]
    public void Update_referencia_a_la_version_aprobada_y_al_aprobarse_la_reemplaza()
    {
        var alerta = Aprobada();
        var update = new VersionMensaje(alerta.AvisoId, alerta.CadenaId, 2, TipoMensajeCap.Update, alerta.Contenido with { },
            Area, EstadoCap.Actual, "sat@santacruz.gob.bo", Operador, alerta);

        Assert.Equal(alerta.Id, update.ReferenciaVersionId);
        Assert.Equal($"sat@santacruz.gob.bo,{alerta.IdentificadorCap},{Ahora:yyyy-MM-ddTHH:mm:sszzz}", update.Referencias);

        update.EnviarARevision("<alert/>", Ahora, Ahora);
        update.Aprobar(Aprobador, Ahora, alerta);
        Assert.Equal(update.Id, alerta.ReemplazadaPorId);
        Assert.False(alerta.EsVigenteEnCadena);
        Assert.True(update.EsVigenteEnCadena);
    }

    [Fact]
    public void Se_rechazan_referencias_incoherentes()
    {
        var cadena = Guid.NewGuid();
        // A un borrador: todavía no existe para la población.
        var borrador = Borrador(cadena);
        Assert.Contains("incoherente", Assert.Throws<ExcepcionDominio>(() => new VersionMensaje(cadena, cadena, 2,
            TipoMensajeCap.Update, Contenido(), Area, EstadoCap.Actual, "sat", Operador, borrador)).Message);

        // A una versión de otro aviso.
        var otra = Aprobada();
        Assert.Contains("otro aviso", Assert.Throws<ExcepcionDominio>(() => new VersionMensaje(cadena, cadena, 2,
            TipoMensajeCap.Update, Contenido(), Area, EstadoCap.Actual, "sat", Operador, otra)).Message);

        // Un Update sin referencia, o un Alert con referencia.
        Assert.Throws<ExcepcionDominio>(() => new VersionMensaje(cadena, cadena, 2, TipoMensajeCap.Update, Contenido(), Area,
            EstadoCap.Actual, "sat", Operador));
        var aprobada = Aprobada(cadena);
        Assert.Throws<ExcepcionDominio>(() => new VersionMensaje(cadena, cadena, 2, TipoMensajeCap.Alert, Contenido(), Area,
            EstadoCap.Actual, "sat", Operador, aprobada));
    }

    [Fact]
    public void No_se_actualiza_lo_cancelado_ni_lo_ya_reemplazado()
    {
        var alerta = Aprobada();
        var cancel = new VersionMensaje(alerta.AvisoId, alerta.CadenaId, 2, TipoMensajeCap.Cancel, alerta.Contenido, Area,
            EstadoCap.Actual, "sat", Operador, alerta);
        cancel.EnviarARevision("<alert/>", Ahora, Ahora);
        cancel.Aprobar(Aprobador, Ahora, alerta);

        Assert.Contains("reemplazada", Assert.Throws<ExcepcionDominio>(() => new VersionMensaje(alerta.AvisoId, alerta.CadenaId, 3,
            TipoMensajeCap.Update, alerta.Contenido, Area, EstadoCap.Actual, "sat", Operador, alerta)).Message);
        Assert.Contains("cancelado", Assert.Throws<ExcepcionDominio>(() => new VersionMensaje(alerta.AvisoId, alerta.CadenaId, 3,
            TipoMensajeCap.Update, alerta.Contenido, Area, EstadoCap.Actual, "sat", Operador, cancel)).Message);
    }

    [Fact]
    public void Dos_updates_de_la_misma_version_el_segundo_no_se_aprueba()
    {
        var alerta = Aprobada();
        var u1 = new VersionMensaje(alerta.AvisoId, alerta.CadenaId, 2, TipoMensajeCap.Update, alerta.Contenido, Area, EstadoCap.Actual, "sat", Operador, alerta);
        var u2 = new VersionMensaje(alerta.AvisoId, alerta.CadenaId, 3, TipoMensajeCap.Update, alerta.Contenido, Area, EstadoCap.Actual, "sat", Operador, alerta);
        u1.EnviarARevision("<alert/>", Ahora, Ahora);
        u2.EnviarARevision("<alert/>", Ahora, Ahora);
        u1.Aprobar(Aprobador, Ahora, alerta);
        Assert.Contains("reemplazada", Assert.Throws<ExcepcionDominio>(() => u2.Aprobar(Aprobador, Ahora, alerta)).Message);
        Assert.Equal(EstadoVersion.EnRevision, u2.Estado);
    }

    [Fact]
    public void Un_cancel_no_exige_vigencia()
    {
        var alerta = Aprobada();
        var cancel = new VersionMensaje(alerta.AvisoId, alerta.CadenaId, 2, TipoMensajeCap.Cancel,
            alerta.Contenido with { Expira = Ahora.AddHours(-1), Instruccion = "Puede volver a su casa." }, Area, EstadoCap.Actual, "sat", Operador, alerta);
        cancel.EnviarARevision("<alert/>", Ahora, Ahora);
        Assert.Equal(EstadoVersion.EnRevision, cancel.Estado);
    }

    // ---------- Aviso oficial: Update del SENAMHI ----------

    private static Aviso AvisoOficial(TipoMensajeCap tipo, string? referencias = null) =>
        new(new MensajeCapBuilder().ConIdentificador("SENAMHI-" + Guid.NewGuid().ToString("N")[..6]).DeRemitente("senamhi@senamhi.gob.bo")
                .ConTipo(tipo).ConReferencias(referencias)
                .AgregarInfo(i => i.ConEvento("Crecida").ConUrgencia(UrgenciaCap.Immediate).ConSeveridad(SeveridadCap.Severe)
                    .ConCerteza(CertezaCap.Observed).ConTitular("Crecida").ExpiraEn(DateTimeOffset.UtcNow.AddHours(6)))
                .Build(), FormatoAviso.CapXml, "<alert/>", Area, Guid.NewGuid());

    [Fact]
    public void Un_update_oficial_con_referencia_inexistente_no_se_prepara()
    {
        var update = AvisoOficial(TipoMensajeCap.Update, "senamhi@senamhi.gob.bo,NO-EXISTE,2026-11-14T10:00:00-04:00");
        Assert.Null(update.CadenaId);
        Assert.Contains(update.MotivosNoPreparable(Ahora), m => m.Contains("Referencia inexistente"));
    }

    [Fact]
    public void Un_update_oficial_se_engancha_a_la_cadena_del_aviso_original()
    {
        var original = AvisoOficial(TipoMensajeCap.Alert);
        Assert.Equal(original.Id, original.CadenaId);
        var update = AvisoOficial(TipoMensajeCap.Update, $"{original.Remitente},{original.IdentificadorCap},2026-11-14T10:00:00-04:00");
        Assert.Equal((original.Remitente, original.IdentificadorCap), Assert.Single(update.ReferenciasCap()));
        update.EnlazarCadena(original);
        Assert.Equal(original.Id, update.CadenaId);
        Assert.Empty(update.MotivosNoPreparable(Ahora));
    }

    // ---------- RF-07: destinos ----------

    [Fact]
    public void No_se_duplica_un_destino_por_envio_version_y_canal()
    {
        var lista = new ListaDestinos(Guid.NewGuid(), [TipoCanal.Sms, TipoCanal.Radio], Operador);
        var comunidad = Guid.NewGuid();
        Assert.True(lista.AgregarDestino(comunidad, Guid.NewGuid(), TipoCanal.Sms, "+59171234567"));
        Assert.False(lista.AgregarDestino(Guid.NewGuid(), Guid.NewGuid(), TipoCanal.Sms, "+59171234567"));
        Assert.True(lista.AgregarDestino(comunidad, Guid.NewGuid(), TipoCanal.Radio, "+59171234567"));
        Assert.Equal(2, lista.Destinos.Count);
        Assert.Throws<ExcepcionDominio>(() => lista.AgregarDestino(comunidad, Guid.NewGuid(), TipoCanal.Email, "a@b.bo"));
    }

    [Fact]
    public void Las_comunidades_sin_canal_figuran_como_pendientes_una_sola_vez()
    {
        var lista = new ListaDestinos(Guid.NewGuid(), [TipoCanal.Sms], Operador);
        var comunidad = Guid.NewGuid();
        lista.AgregarPendiente(comunidad, "Las Cruces", "Sin contactos registrados.");
        lista.AgregarPendiente(comunidad, "Las Cruces", "Sin contactos registrados.");
        Assert.Equal("Las Cruces", Assert.Single(lista.Pendientes).Comunidad);
    }

    // ---------- RF-06: nivel con texto ----------

    [Theory]
    [InlineData(SeveridadCap.Extreme, "Alerta roja")]
    [InlineData(SeveridadCap.Severe, "Alerta naranja")]
    [InlineData(SeveridadCap.Moderate, "Alerta amarilla")]
    [InlineData(SeveridadCap.Minor, "Alerta verde")]
    public void El_nivel_se_identifica_con_texto(SeveridadCap severidad, string texto) =>
        Assert.Equal(texto, NivelAlerta.De(severidad).Texto);

    [Fact]
    public void El_sms_y_la_lectura_llevan_el_nivel_en_palabras()
    {
        var v = Aprobada();
        var (sms, lectura) = TextosMensaje.De(v);
        Assert.StartsWith("ALERTA NARANJA: Crecida en Porongo.", sms);
        Assert.Contains("Evacuar a la zona alta.", sms);
        Assert.StartsWith("Alerta naranja. Crecida en Porongo.", lectura);
        Assert.Contains("Qué hacer: Evacuar a la zona alta.", lectura);
        Assert.Contains("Zona: Porongo", lectura);
    }

    // ---------- XML CAP 1.2 generado y validado ----------

    [Fact]
    public void El_xml_de_un_update_es_cap_valido_y_lleva_sus_referencias()
    {
        var modulo = new ModuloCap(new FabricaImportadores([new ImportadorCapXml(), new ImportadorCapJson()]), new ValidadorCap());
        var servicio = new ServicioVersiones(null!, modulo, new OpcionesCap(), new PublicadorEventosAvisos([]));
        var alerta = Aprobada();
        var update = new VersionMensaje(alerta.AvisoId, alerta.CadenaId, 2, TipoMensajeCap.Update, alerta.Contenido, Area,
            EstadoCap.Actual, "sat@santacruz.gob.bo", Operador, alerta);

        var xml = servicio.GenerarXml(update, Ahora.ToOffset(TimeSpan.FromHours(-4)));

        Assert.True(modulo.Validar(xml).Valido, string.Join(" ", modulo.Validar(xml).Errores));
        var leido = modulo.Importar(xml).Mensaje;
        Assert.Equal(TipoMensajeCap.Update, leido.MsgType);
        Assert.Equal(update.Referencias, leido.References);
        Assert.Equal(update.IdentificadorCap, leido.Identifier);
        Assert.Equal("Evacuar a la zona alta.", leido.Infos[0].Instruction);
        Assert.Single(leido.Infos[0].Areas);
    }

    [Fact]
    public void Un_xml_invalido_no_pasa_la_validacion()
    {
        var modulo = new ModuloCap(new FabricaImportadores([new ImportadorCapXml(), new ImportadorCapJson()]), new ValidadorCap());
        Assert.False(modulo.Validar("<alert xmlns=\"urn:oasis:names:tc:emergency:cap:1.2\"><identifier>x</identifier></alert>").Valido);
    }
}
