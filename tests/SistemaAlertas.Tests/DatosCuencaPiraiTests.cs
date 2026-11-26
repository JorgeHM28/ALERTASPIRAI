using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Domain.Difusion;
using SistemaAlertas.Domain.Zonas;
using SistemaAlertas.Infrastructure.Persistence;

namespace SistemaAlertas.Tests;

/// <summary>
/// Sprint 3: los datos reales de la cuenca (datos/cuenca-pirai) pasan por las validaciones del dominio y son coherentes:
/// 22 municipios con geometría válida y cada localidad dentro de su municipio.
/// </summary>
public class DatosCuencaPiraiTests
{
    private static async Task<AlertasDbContext> Cargado()
    {
        var db = new AlertasDbContext(new DbContextOptionsBuilder<AlertasDbContext>().UseInMemoryDatabase($"cuenca-{Guid.NewGuid()}").Options);
        await SemillaCuencaPirai.CargarAsync(db, default);
        return db;
    }

    [Fact]
    public async Task Carga_los_22_municipios_de_la_cuenca_con_su_fuente()
    {
        await using var db = await Cargado();
        var zonas = await db.Zonas.ToListAsync();

        Assert.Equal(22, zonas.Count);
        Assert.Equal(22, zonas.Select(z => z.Nombre).Distinct().Count());
        Assert.All(zonas, z =>
        {
            Assert.True(z.Area.IsValid, $"{z.Nombre}: geometría no válida");
            Assert.Equal(Zona.Srid, z.Area.SRID);
            Assert.StartsWith("GeoBolivia", z.FuenteInstitucional, StringComparison.Ordinal);
            Assert.Contains("cuenca del río Piraí", z.Descripcion, StringComparison.Ordinal);
        });
        Assert.Contains(zonas, z => z.Nombre == "Santa Cruz de la Sierra");
        Assert.Contains(zonas, z => z.Nombre == "El Torno" && z.NivelRiesgo == NivelRiesgo.Alto);
    }

    [Fact]
    public async Task Cada_localidad_queda_dentro_de_su_municipio_y_sin_contactos()
    {
        await using var db = await Cargado();
        var zonas = await db.Zonas.ToDictionaryAsync(z => z.Id);
        var comunidades = await db.Comunidades.ToListAsync();

        Assert.Equal(48, comunidades.Count);
        Assert.All(comunidades, c =>
        {
            var zona = zonas[c.ZonaId];
            Assert.Equal(zona.Nombre, c.Municipio);
            Assert.True(zona.Area.Contains(c.Ubicacion), $"{c.Nombre} cae fuera de {zona.Nombre}");
        });
        Assert.Empty(await db.Contactos.ToListAsync());
        Assert.All(zonas.Values, z => Assert.Contains(comunidades, c => c.ZonaId == z.Id));
    }

    [Fact]
    public async Task No_vuelve_a_cargar_si_ya_hay_zonas()
    {
        await using var db = await Cargado();
        Assert.Equal(0, await SemillaCuencaPirai.CargarAsync(db, default));
        Assert.Equal(22, await db.Zonas.CountAsync());
    }

    [Fact]
    public void El_conteo_del_seguimiento_separa_cada_estado()
    {
        var c = ConteoEstados.De([EstadoTrabajo.Enviado, EstadoTrabajo.Enviado, EstadoTrabajo.Fallido, EstadoTrabajo.Pendiente,
            EstadoTrabajo.Bloqueado, EstadoTrabajo.Confirmada, EstadoTrabajo.NoRealizada]);
        Assert.Equal((7, 2, 1, 1, 1, 1, 1, 6), (c.Total, c.Enviados, c.Fallidos, c.Pendientes, c.Bloqueados, c.Confirmadas, c.NoRealizadas, c.Terminados));
        Assert.Equal("*******4567", ServicioSeguimiento.Enmascarar("+5917654567"));
    }
}
