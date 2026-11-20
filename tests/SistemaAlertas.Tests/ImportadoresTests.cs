using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Tests;

public class ImportadoresTests
{
    private static readonly string RutaEjemplos = Path.Combine(AppContext.BaseDirectory, "ejemplos");
    private readonly FabricaImportadores _fabrica = new([new ImportadorCapXml(), new ImportadorCapJson()]);

    [Fact]
    public void La_fabrica_devuelve_el_importador_de_cada_formato()
    {
        Assert.IsType<ImportadorCapXml>(_fabrica.Crear(FormatoAviso.CapXml));
        Assert.IsType<ImportadorCapJson>(_fabrica.Crear(FormatoAviso.CapJson));
        Assert.Throws<ExcepcionDominio>(() => _fabrica.Crear(FormatoAviso.Manual));
    }

    [Theory]
    [InlineData("  <alert/>", typeof(ImportadorCapXml))]
    [InlineData("{\"identifier\":\"x\"}", typeof(ImportadorCapJson))]
    public void La_fabrica_detecta_el_formato_por_el_contenido(string contenido, Type esperado)
    {
        Assert.IsType(esperado, _fabrica.CrearSegunContenido(contenido));
    }

    [Fact]
    public void La_fabrica_rechaza_contenido_desconocido()
    {
        Assert.Throws<ExcepcionDominio>(() => _fabrica.CrearSegunContenido("hola"));
    }

    [Fact]
    public void Importa_el_ejemplo_xml_del_senamhi()
    {
        var m = new ImportadorCapXml().Importar(File.ReadAllText(Path.Combine(RutaEjemplos, "aviso-senamhi.xml")));

        Assert.Equal("SENAMHI-SCZ-2026-1104-001", m.Identifier);
        Assert.Equal(TimeSpan.FromHours(-4), m.Sent.Offset);
        var info = Assert.Single(m.Infos);
        Assert.Equal(SeveridadCap.Severe, info.Severity);
        Assert.Equal("Ribera Piraí - Porongo", Assert.Single(info.Areas).AreaDesc);
    }

    [Fact]
    public void Importa_el_ejemplo_json()
    {
        var m = new ImportadorCapJson().Importar(File.ReadAllText(Path.Combine(RutaEjemplos, "aviso-cap.json")));
        Assert.Equal("COED-SCZ-2026-1104-007", m.Identifier);
        Assert.Equal(UrgenciaCap.Immediate, Assert.Single(m.Infos).Urgency);
    }

    [Fact]
    public void Xml_serializado_se_vuelve_a_importar_igual()
    {
        var original = new ImportadorCapXml().Importar(File.ReadAllText(Path.Combine(RutaEjemplos, "aviso-senamhi.xml")));
        var copia = new ImportadorCapXml().Importar(CapXml.Serializar(original));

        Assert.Equal(original.Identifier, copia.Identifier);
        Assert.Equal(original.Sent, copia.Sent);
        Assert.Equal(original.Infos[0].Event, copia.Infos[0].Event);
        Assert.Equal(original.Infos[0].Areas[0].Polygons, copia.Infos[0].Areas[0].Polygons);
    }

    [Fact]
    public void Rechaza_xml_que_no_es_cap()
    {
        var ex = Assert.Throws<ExcepcionDominio>(() => new ImportadorCapXml().Importar("<alert><identifier>x</identifier></alert>"));
        Assert.Contains("CAP 1.2", ex.Message);
    }

    [Fact]
    public void Rechaza_valores_que_no_son_del_estandar()
    {
        const string xml = """
            <alert xmlns="urn:oasis:names:tc:emergency:cap:1.2">
              <identifier>x</identifier><sender>a@b.bo</sender><sent>2026-11-04T08:00:00-04:00</sent>
              <status>Actual</status><msgType>Alert</msgType><scope>Public</scope>
              <info><category>Met</category><event>x</event><urgency>Ahora</urgency>
                <severity>Severe</severity><certainty>Likely</certainty></info>
            </alert>
            """;
        var ex = Assert.Throws<ExcepcionDominio>(() => new ImportadorCapXml().Importar(xml));
        Assert.Contains("urgency", ex.Message);
    }

    [Fact]
    public void Bloquea_dtd_para_evitar_xxe()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE alert [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <alert xmlns="urn:oasis:names:tc:emergency:cap:1.2"><identifier>&xxe;</identifier></alert>
            """;
        Assert.Throws<ExcepcionDominio>(() => new ImportadorCapXml().Importar(xml));
    }

    [Fact]
    public void Utc_se_escribe_como_menos_cero_cero()
    {
        Assert.Equal("2026-11-04T12:00:00-00:00", CapXml.Fecha(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal("2026-11-04T08:00:00-04:00", CapXml.Fecha(new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.FromHours(-4))));
    }

    [Fact]
    public void Poligono_cap_usa_lat_lon_y_la_geometria_lon_lat()
    {
        var g = PoligonoCap.ALaGeometria("-17.77,-63.25 -17.77,-63.21 -17.82,-63.21 -17.77,-63.25");
        Assert.Equal(-63.25, g.Coordinates[0].X);
        Assert.Equal(-17.77, g.Coordinates[0].Y);
        Assert.Equal("-17.77,-63.25 -17.77,-63.21 -17.82,-63.21 -17.77,-63.25", PoligonoCap.DesdeGeometria(g));
    }
}

/// <summary>Adapter: XML CAP 1.2 del SENAMHI traducido al modelo interno y a la entidad Aviso de RF-01.</summary>
public class ImportadorCapAdapterTests
{
    private static readonly string RutaEjemplos = Path.Combine(AppContext.BaseDirectory, "ejemplos");

    [Fact]
    public void El_xml_cap_valido_se_traduce_a_la_entidad_aviso()
    {
        var xml = File.ReadAllText(Path.Combine(RutaEjemplos, "aviso-senamhi.xml"));
        IImportadorAviso adaptador = new ImportadorCapXml();

        var mensaje = adaptador.Importar(xml);
        var aviso = new Aviso(mensaje, adaptador.Formato, xml,
            PoligonoCap.UnirAreas(mensaje.Infos.SelectMany(i => i.Areas).SelectMany(a => a.Polygons)), Guid.NewGuid(), xml);

        Assert.Equal("SENAMHI-SCZ-2026-1104-001", aviso.IdentificadorCap);
        Assert.Equal(mensaje.Sender, aviso.Remitente);
        Assert.Equal(FormatoAviso.CapXml, aviso.Formato);
        Assert.Equal(SeveridadCap.Severe, aviso.Severidad);
        Assert.NotNull(aviso.Area);
        Assert.Equal(64, aviso.HashOriginal!.Length);
    }

    [Theory]
    [InlineData("<alert xmlns=\"urn:oasis:names:tc:emergency:cap:1.2\"><identifier>x</identifier>")]
    [InlineData("<?xml version=\"1.0\"?><!DOCTYPE alert [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><alert>&x;</alert>")]
    public void El_xml_mal_formado_o_con_dtd_se_rechaza(string xml)
    {
        var ex = Assert.Throws<ExcepcionDominio>(() => new ImportadorCapXml().Importar(xml));
        Assert.Contains("XML no es válido", ex.Message);
    }

    [Fact]
    public void Un_xml_cap_sin_campos_obligatorios_se_rechaza()
    {
        const string xml = """
            <alert xmlns="urn:oasis:names:tc:emergency:cap:1.2">
              <identifier>x</identifier><sent>2026-11-04T08:00:00-04:00</sent>
              <status>Actual</status><msgType>Alert</msgType><scope>Public</scope>
            </alert>
            """;
        Assert.Throws<ExcepcionDominio>(() => new ImportadorCapXml().Importar(xml));
    }
}
