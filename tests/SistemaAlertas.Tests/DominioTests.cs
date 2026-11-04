using NetTopologySuite.Geometries;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Usuarios;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Tests;

public class DominioTests
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), 4326);

    private static Polygon Cuadrado(double lon, double lat, double lado = 0.01) => Geo.CreatePolygon([
        new(lon, lat), new(lon + lado, lat), new(lon + lado, lat + lado), new(lon, lat + lado), new(lon, lat)]);

    [Fact]
    public void Zona_acepta_poligonos_validos()
    {
        var zona = new Zona("Porongo", null, NivelRiesgo.Alto, Cuadrado(-63.25, -17.8));
        Assert.Equal(4326, zona.Area.SRID);
    }

    [Fact]
    public void Zona_rechaza_puntos_y_poligonos_que_se_cruzan()
    {
        Assert.Throws<ExcepcionDominio>(() => new Zona("X", null, NivelRiesgo.Bajo, Geo.CreatePoint(new Coordinate(-63, -17))));
        var corbata = Geo.CreatePolygon([new(0, 0), new(1, 1), new(1, 0), new(0, 1), new(0, 0)]);
        Assert.Throws<ExcepcionDominio>(() => new Zona("X", null, NivelRiesgo.Bajo, corbata));
    }

    [Fact]
    public void Zona_rechaza_coordenadas_que_no_son_wgs84()
    {
        // Típico error: coordenadas UTM en vez de lon/lat.
        Assert.Throws<ExcepcionDominio>(() => new Zona("X", null, NivelRiesgo.Bajo, Cuadrado(480000, 8030000, 100)));
    }

    [Theory]
    [InlineData(TipoCanal.Sms, "+591 712-34567", "+59171234567")]
    [InlineData(TipoCanal.Email, "Juan@Mail.COM", "juan@mail.com")]
    [InlineData(TipoCanal.Radio, "Radio Integración 98.5", "Radio Integración 98.5")]
    public void Canal_normaliza_el_destino(TipoCanal tipo, string entrada, string esperado)
    {
        Assert.Equal(esperado, new CanalContacto(tipo, entrada, false).Destino);
    }

    [Theory]
    [InlineData(TipoCanal.Sms, "71234567")]
    [InlineData(TipoCanal.WhatsApp, "+0123")]
    [InlineData(TipoCanal.Email, "no-es-email")]
    [InlineData(TipoCanal.Radio, " ")]
    public void Canal_rechaza_destinos_invalidos(TipoCanal tipo, string destino)
    {
        Assert.Throws<ExcepcionDominio>(() => new CanalContacto(tipo, destino, false));
    }

    [Fact]
    public void Contacto_permite_un_solo_canal_preferido_y_sin_repetidos()
    {
        var comunidad = new Comunidad("Porongo", null, 100, null, Guid.NewGuid());
        var contacto = comunidad.AgregarContacto("Juan", null);

        Assert.Throws<ExcepcionDominio>(() => contacto.ReemplazarCanales([]));
        Assert.Throws<ExcepcionDominio>(() => contacto.ReemplazarCanales([
            (TipoCanal.Sms, "+59171234567", true), (TipoCanal.WhatsApp, "+59171234567", true)]));
        Assert.Throws<ExcepcionDominio>(() => contacto.ReemplazarCanales([
            (TipoCanal.Sms, "+59171234567", false), (TipoCanal.Sms, "+591 71234567", false)]));

        contacto.ReemplazarCanales([(TipoCanal.Sms, "+59171234567", true), (TipoCanal.Email, "j@x.bo", false)]);
        Assert.Equal(2, contacto.Canales.Count);
    }

    [Fact]
    public void Comunidad_necesita_zona_y_poblacion_no_negativa()
    {
        Assert.Throws<ExcepcionDominio>(() => new Comunidad("X", null, 10, null, Guid.Empty));
        Assert.Throws<ExcepcionDominio>(() => new Comunidad("X", null, -1, null, Guid.NewGuid()));
    }

    [Fact]
    public void Usuario_normaliza_nombre_y_email()
    {
        var u = new Usuario("  Pedro ", "Pedro M", "Pedro@Mail.com", Rol.Operador);
        Assert.Equal("pedro", u.NombreUsuario);
        Assert.Equal("pedro@mail.com", u.Email);
        Assert.True(u.Activo);
    }
}
