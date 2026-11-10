using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Tests;

/// <summary>Facade: ModuloCap agrupa importar, generar y validar CAP 1.2.</summary>
public class ModuloCapTests
{
    private static readonly string RutaEjemplos = Path.Combine(AppContext.BaseDirectory, "ejemplos");
    private readonly ModuloCap _modulo = new(new FabricaImportadores([new ImportadorCapXml(), new ImportadorCapJson()]), new ValidadorCap());

    private static string Ejemplo(string nombre) => File.ReadAllText(Path.Combine(RutaEjemplos, nombre));

    [Theory]
    [InlineData("aviso-senamhi.xml", FormatoAviso.CapXml)]
    [InlineData("aviso-cap.json", FormatoAviso.CapJson)]
    public void Importa_detectando_el_formato(string archivo, FormatoAviso esperado)
    {
        var r = _modulo.Importar(Ejemplo(archivo));
        Assert.Equal(esperado, r.Formato);
        Assert.NotEmpty(r.Mensaje.Identifier);
    }

    [Fact]
    public void Lo_que_genera_pasa_su_propia_validacion()
    {
        var mensaje = _modulo.Importar(Ejemplo("aviso-cap.json")).Mensaje;

        var xml = _modulo.Generar(mensaje);

        Assert.True(_modulo.Validar(xml).Valido);
        Assert.Equal(mensaje.Identifier, _modulo.Importar(xml, FormatoAviso.CapXml).Mensaje.Identifier);
    }

    [Fact]
    public void Valida_el_ejemplo_oficial()
    {
        var r = _modulo.Validar(Ejemplo("aviso-senamhi.xml"));
        Assert.True(r.Valido, string.Join(" | ", r.Errores));
    }

    [Fact]
    public void Informa_todos_los_elementos_obligatorios_que_faltan()
    {
        const string xml = """
            <alert xmlns="urn:oasis:names:tc:emergency:cap:1.2">
              <identifier>x</identifier><status>Actual</status><msgType>Alert</msgType>
              <info><event>Inundación</event><urgency>Immediate</urgency>
                <area><polygon>-17.7,-63.2 -17.8,-63.2 -17.7,-63.2</polygon></area>
              </info>
            </alert>
            """;
        var r = _modulo.Validar(xml);

        Assert.False(r.Valido);
        Assert.Contains("alert: falta <sender>.", r.Errores);
        Assert.Contains("alert: falta <sent>.", r.Errores);
        Assert.Contains("alert: falta <scope>.", r.Errores);
        Assert.Contains("info[1]: falta <category>.", r.Errores);
        Assert.Contains("info[1]: falta <severity>.", r.Errores);
        Assert.Contains("info[1]/area[1]: falta <areaDesc>.", r.Errores);
        Assert.Contains(r.Errores, e => e.Contains("polígono inválido"));
    }

    [Theory]
    [InlineData("<alert")]
    [InlineData("<alert><identifier>x</identifier></alert>")]
    public void Rechaza_xml_mal_formado_o_sin_namespace_cap(string xml)
    {
        Assert.False(_modulo.Validar(xml).Valido);
    }

    [Fact]
    public void Importar_un_formato_sin_importador_es_un_error_de_dominio()
    {
        Assert.Throws<ExcepcionDominio>(() => _modulo.Importar("x", FormatoAviso.Manual));
    }
}
