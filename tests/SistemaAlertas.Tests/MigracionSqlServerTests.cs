using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaAlertas.Infrastructure.Persistence;
namespace SistemaAlertas.Tests;

public sealed class MigracionSqlServerTests
{
    private static AlertasDbContext Contexto() => new(new DbContextOptionsBuilder<AlertasDbContext>()
        .UseSqlServer("Server=localhost;Database=SoloModelo;Integrated Security=true", o => o.UseNetTopologySuite())
        .UseSnakeCaseNamingConvention().Options);
    [Fact]
    public void ModeloYMigrationCoinciden()
    { using var db = Contexto(); Assert.False(db.Database.HasPendingModelChanges()); Assert.Single(db.Database.GetMigrations()); }
    [Fact]
    public void ScriptContieneClavesGeometriasYJsonSqlServer()
    { using var db = Contexto(); var sql = db.Database.GenerateCreateScript(); Assert.Contains(" geometry ", sql); Assert.Contains("ISJSON", sql); Assert.Contains("FOREIGN KEY", sql); Assert.DoesNotContain("jsonb", sql); Assert.DoesNotContain("USING gist", sql); }
    [Fact]
    public void AlcanceGeograficoSeTraduceEnServidor()
    { using var db = Contexto(); var punto = new Point(-63.2, -17.7) { SRID = 4326 }; var sql = db.Zonas.Where(z => z.Area.Intersects(punto)).ToQueryString(); Assert.Contains("STIntersects", sql); }
    [Fact]
    public void BusquedaIgnoraMayusculasSinCambiarClavesUnicas()
    { using var db = Contexto(); var sql = db.Comunidades.Where(c => EF.Functions.Like(EF.Functions.Collate(c.Nombre, "Latin1_General_100_CI_AS"), "%pirai%")).ToQueryString(); Assert.Contains("COLLATE Latin1_General_100_CI_AS", sql); }
}
