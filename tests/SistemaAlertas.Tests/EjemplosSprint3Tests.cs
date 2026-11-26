using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Tests;

/// <summary>
/// Sprint 3: los avisos simulados de docs/ejemplos/sprint-3 (XML, JSON y Excel) se importan, generan un CAP 1.2 válido
/// contra el XSD oficial, y los ejemplos inválidos se rechazan con un motivo claro.
/// </summary>
public class EjemplosSprint3Tests
{
    private static readonly string Ruta = Path.Combine(AppContext.BaseDirectory, "ejemplos", "sprint-3");
    private static readonly ModuloCap Modulo = new(
        new FabricaImportadores([new ImportadorCapXml(), new ImportadorCapJson(), new ImportadorCapExcel()]), new ValidadorCap());

    /// <summary>Lo que la API recibe: el texto del XML o JSON, o el Excel en base64.</summary>
    private static string Contenido(string archivo)
    {
        var ruta = Path.Combine(Ruta, archivo);
        return archivo.EndsWith(".xlsx", StringComparison.Ordinal) ? Convert.ToBase64String(File.ReadAllBytes(ruta)) : File.ReadAllText(ruta);
    }

    [Theory]
    [InlineData("01-alerta-naranja-el-torno.xml", FormatoAviso.CapXml, TipoMensajeCap.Alert, SeveridadCap.Severe)]
    [InlineData("02-actualizacion-roja-el-torno.xml", FormatoAviso.CapXml, TipoMensajeCap.Update, SeveridadCap.Extreme)]
    [InlineData("03-cancelacion-el-torno.xml", FormatoAviso.CapXml, TipoMensajeCap.Cancel, SeveridadCap.Minor)]
    [InlineData("04-alerta-amarilla-norte.json", FormatoAviso.CapJson, TipoMensajeCap.Alert, SeveridadCap.Moderate)]
    [InlineData("05-alerta-roja-porongo.xlsx", FormatoAviso.Excel, TipoMensajeCap.Alert, SeveridadCap.Extreme)]
    [InlineData("06-simulacro-porongo.json", FormatoAviso.CapJson, TipoMensajeCap.Alert, SeveridadCap.Severe)]
    public void Cada_ejemplo_se_importa_y_genera_un_cap_valido(string archivo, FormatoAviso formato, TipoMensajeCap tipo, SeveridadCap severidad)
    {
        var importado = Modulo.Importar(Contenido(archivo));

        Assert.Equal(formato, importado.Formato);
        Assert.Equal(tipo, importado.Mensaje.MsgType);
        var info = Assert.Single(importado.Mensaje.Infos);
        Assert.Equal(severidad, info.Severity);
        Assert.NotEmpty(Assert.Single(info.Areas).Polygons);
        var validacion = Modulo.Validar(Modulo.Generar(importado.Mensaje));
        Assert.True(validacion.Valido, string.Join(" ", validacion.Errores));
    }

    [Fact]
    public void El_excel_trae_todos_los_campos_de_la_plantilla()
    {
        var m = Modulo.Importar(Contenido("05-alerta-roja-porongo.xlsx")).Mensaje;
        var info = m.Infos[0];
        Assert.Equal("SENAMHI-SIM-2026-1204-005", m.Identifier);
        Assert.Equal(new DateTimeOffset(2026, 12, 4, 6, 30, 0, TimeSpan.FromHours(-4)), m.Sent);
        Assert.Contains("simulado", m.Note);
        Assert.Equal(UrgenciaCap.Immediate, info.Urgency);
        Assert.Equal(CertezaCap.Observed, info.Certainty);
        Assert.StartsWith("Evacúe", info.Instruction);
        Assert.Equal("Río Piraí, tramo Porongo y La Guardia", info.Areas[0].AreaDesc);
    }

    [Fact]
    public void Un_simulacro_viaja_como_exercise() =>
        Assert.Equal(EstadoCap.Exercise, Modulo.Importar(Contenido("06-simulacro-porongo.json")).Mensaje.Status);

    [Fact]
    public void El_update_y_el_cancel_referencian_al_aviso_anterior()
    {
        Assert.Contains("SENAMHI-SIM-2026-1201-001", Modulo.Importar(Contenido("02-actualizacion-roja-el-torno.xml")).Mensaje.References);
        Assert.Contains("SENAMHI-SIM-2026-1201-002", Modulo.Importar(Contenido("03-cancelacion-el-torno.xml")).Mensaje.References);
    }

    [Theory]
    [InlineData("90-invalido-sin-info.xml", "info")]
    [InlineData("91-invalido-update-sin-referencia.json", "references")]
    [InlineData("92-invalido-excel-sin-hoja.xlsx", "Aviso")]
    public void Los_ejemplos_invalidos_se_rechazan_con_su_motivo(string archivo, string motivo)
    {
        var ex = Assert.Throws<ExcepcionDominio>(() => Modulo.Importar(Contenido(archivo)));
        Assert.Contains(motivo, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void La_plantilla_vacia_no_pasa_la_validacion() =>
        Assert.Throws<ExcepcionDominio>(() => Modulo.Importar(Contenido("plantilla-aviso.xlsx")));

    [Theory]
    [InlineData("UEsDBno-es-un-zip")]
    [InlineData("UEsDBBQ=")]
    public void Un_base64_que_no_es_un_excel_se_rechaza(string contenido) =>
        Assert.Throws<ExcepcionDominio>(() => new ImportadorCapExcel().Importar(contenido));
}
