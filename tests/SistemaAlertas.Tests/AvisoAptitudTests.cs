using NetTopologySuite.Geometries;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;

namespace SistemaAlertas.Tests;

/// <summary>RF-01: avisos incompletos o vencidos quedan registrados como no aptos para difusión.</summary>
public class AvisoAptitudTests
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);
    private static readonly Polygon Area = Geo.CreatePolygon([new(-63.25, -17.8), new(-63.2, -17.8), new(-63.2, -17.75), new(-63.25, -17.8)]);

    private static MensajeCap Mensaje(DateTimeOffset? expira, string? instruccion = "Evacuar", EstadoCap estado = EstadoCap.Actual) =>
        new MensajeCapBuilder().DeRemitente("coed@santacruz.gob.bo").ConEstado(estado)
            .AgregarInfo(i => i.ConEvento("Crecida").ConUrgencia(UrgenciaCap.Immediate).ConSeveridad(SeveridadCap.Severe)
                .ConCerteza(CertezaCap.Observed).ConTitular("Alerta roja").ConInstruccion(instruccion).ExpiraEn(expira))
            .Build();

    [Fact]
    public void Aviso_completo_y_vigente_es_apto()
    {
        var aviso = new Aviso(Mensaje(DateTimeOffset.UtcNow.AddHours(6)), FormatoAviso.Manual, "<alert/>", Area, Guid.NewGuid());
        Assert.True(aviso.AptoParaDifusion);
        Assert.False(aviso.Incompleto);
        Assert.Empty(aviso.MotivosNoApto);
    }

    [Fact]
    public void Aviso_incompleto_no_es_apto_y_dice_por_que()
    {
        var aviso = new Aviso(Mensaje(null, instruccion: null), FormatoAviso.Manual, "<alert/>", null, Guid.NewGuid());
        Assert.True(aviso.Incompleto);
        Assert.False(aviso.AptoParaDifusion);
        Assert.Contains(aviso.MotivosNoApto, m => m.Contains("instrucción"));
        Assert.Contains(aviso.MotivosNoApto, m => m.Contains("área"));
        Assert.Contains(aviso.MotivosNoApto, m => m.Contains("expiración"));
    }

    [Fact]
    public void Aviso_vence_y_queda_registrado()
    {
        var aviso = new Aviso(Mensaje(DateTimeOffset.UtcNow.AddHours(1)), FormatoAviso.Manual, "<alert/>", Area, Guid.NewGuid());
        Assert.True(aviso.AptoParaDifusion);

        Assert.True(aviso.EvaluarAptitud(DateTimeOffset.UtcNow.AddHours(2)));
        Assert.True(aviso.Vencido);
        Assert.False(aviso.AptoParaDifusion);
        Assert.Contains(aviso.MotivosNoApto, m => m.StartsWith("Vencido", StringComparison.Ordinal));
        Assert.False(aviso.EvaluarAptitud(DateTimeOffset.UtcNow.AddHours(3))); // ya estaba registrado
    }

    [Fact]
    public void Un_mensaje_de_prueba_no_es_apto()
    {
        var aviso = new Aviso(Mensaje(DateTimeOffset.UtcNow.AddHours(6), estado: EstadoCap.Test), FormatoAviso.CapXml, "<alert/>", Area, Guid.NewGuid());
        Assert.False(aviso.AptoParaDifusion);
        Assert.False(aviso.Incompleto);
    }

    [Fact]
    public void Guarda_el_original_exacto_con_su_hash()
    {
        const string json = "{ \"identifier\" : \"X\",\n  \"sender\":\"a@b.bo\" }";
        var aviso = new Aviso(Mensaje(null), FormatoAviso.CapJson, "<alert/>", null, Guid.NewGuid(), json);
        Assert.Equal(json, aviso.ContenidoOriginal);
        Assert.Equal("application/json", aviso.TipoContenidoOriginal);
        Assert.Equal(64, aviso.HashOriginal!.Length);
    }
}
