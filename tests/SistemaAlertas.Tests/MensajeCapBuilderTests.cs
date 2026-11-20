using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Tests;

public class MensajeCapBuilderTests
{
    private static MensajeCapBuilder BuilderValido() => new MensajeCapBuilder()
        .ConIdentificador("SAT-001")
        .DeRemitente("sat@pirai.bo")
        .EnviadoEn(new DateTimeOffset(2026, 11, 4, 8, 0, 0, 500, TimeSpan.FromHours(-4)))
        .AgregarInfo(i => i.ConEvento("Crecida").ConUrgencia(UrgenciaCap.Immediate)
            .ConSeveridad(SeveridadCap.Severe).ConCerteza(CertezaCap.Observed)
            .AgregarArea("Porongo", "-17.77,-63.25 -17.77,-63.21 -17.82,-63.21 -17.77,-63.25"));

    [Fact]
    public void Build_con_datos_validos_crea_el_mensaje()
    {
        var m = BuilderValido().Build();

        Assert.Equal("SAT-001", m.Identifier);
        Assert.Equal(TipoMensajeCap.Alert, m.MsgType);
        Assert.Equal(0, m.Sent.Millisecond); // CAP no admite fracciones de segundo
        var info = Assert.Single(m.Infos);
        Assert.Equal([CategoriaCap.Met], info.Categories);
        Assert.Equal("es-BO", info.Language);
    }

    [Fact]
    public void Build_sin_identificador_genera_uno()
    {
        var m = BuilderValido().ConIdentificador("").Build();
        Assert.StartsWith("SAT-PIRAI-", m.Identifier);
    }

    [Theory]
    [InlineData("con espacio")]
    [InlineData("con,coma")]
    [InlineData("a&b")]
    public void Build_rechaza_identificadores_con_caracteres_prohibidos(string id)
    {
        var ex = Assert.Throws<ExcepcionDominio>(() => BuilderValido().ConIdentificador(id).Build());
        Assert.Contains("identifier", ex.Message);
    }

    [Fact]
    public void Build_acumula_todos_los_errores()
    {
        var ex = Assert.Throws<ExcepcionDominio>(() => new MensajeCapBuilder()
            .AgregarInfo(i => i.ConEvento(""))
            .Build());

        Assert.Contains("sender", ex.Message);
        Assert.Contains("event", ex.Message);
        Assert.Contains("urgency", ex.Message);
        Assert.Contains("severity", ex.Message);
        Assert.Contains("certainty", ex.Message);
    }

    [Fact]
    public void Alert_sin_info_no_es_valido()
    {
        var ex = Assert.Throws<ExcepcionDominio>(() => new MensajeCapBuilder().DeRemitente("a@b.bo").Build());
        Assert.Contains("info", ex.Message);
    }

    [Fact]
    public void Update_exige_references()
    {
        var ex = Assert.Throws<ExcepcionDominio>(() => BuilderValido().ConTipo(TipoMensajeCap.Update).Build());
        Assert.Contains("references", ex.Message);
        BuilderValido().ConTipo(TipoMensajeCap.Update).ConReferencias("sat@pirai.bo,SAT-000,2026-11-04T07:00:00-04:00").Build();
    }

    [Fact]
    public void Restricted_exige_restriction()
    {
        Assert.Throws<ExcepcionDominio>(() => BuilderValido().ConAlcance(AlcanceCap.Restricted).Build());
    }

    [Fact]
    public void Expires_debe_ser_posterior_a_effective()
    {
        var ahora = DateTimeOffset.UtcNow;
        var ex = Assert.Throws<ExcepcionDominio>(() => BuilderValido()
            .AgregarInfo(i => i.ConEvento("x").ConUrgencia(UrgenciaCap.Future).ConSeveridad(SeveridadCap.Minor)
                .ConCerteza(CertezaCap.Possible).VigenteDesde(ahora).ExpiraEn(ahora.AddHours(-1)))
            .Build());
        Assert.Contains("expires", ex.Message);
    }

    [Theory]
    [InlineData("-17.7,-63.2 -17.7,-63.1 -17.8,-63.1", "al menos 4")]
    [InlineData("-17.7,-63.2 -17.7,-63.1 -17.8,-63.1 -17.9,-63.2", "primer y el último")]
    [InlineData("-17.7;-63.2 -17.7,-63.1 -17.8,-63.1 -17.7,-63.2", "no es un par")]
    [InlineData("-97.7,-63.2 -17.7,-63.1 -17.8,-63.1 -97.7,-63.2", "fuera del rango")]
    public void Poligonos_invalidos_se_rechazan(string poligono, string mensaje)
    {
        var ex = Assert.Throws<ExcepcionDominio>(() => BuilderValido()
            .AgregarInfo(i => i.ConEvento("x").ConUrgencia(UrgenciaCap.Future).ConSeveridad(SeveridadCap.Minor)
                .ConCerteza(CertezaCap.Possible).AgregarArea("Zona", poligono))
            .Build());
        Assert.Contains(mensaje, ex.Message);
    }
}
