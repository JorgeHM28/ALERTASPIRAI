using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Zonas;
using SistemaAlertas.Infrastructure.Persistence;
namespace SistemaAlertas.Api.Comun;

public static class DatosDemostracion
{
    public static async Task Cargar(IServiceProvider servicios)
    {
        var db = servicios.GetRequiredService<AlertasDbContext>();
        if (await db.Zonas.AnyAsync() || await db.Comunidades.AnyAsync()) throw new InvalidOperationException("La demostración solo se carga en una base sin zonas ni comunidades. No se mezcla con datos importados.");
        var fabrica = new GeometryFactory(new PrecisionModel(), 4326);
        var poligono = fabrica.CreatePolygon([new(-63.3, -17.9), new(-63.1, -17.9), new(-63.1, -17.6), new(-63.3, -17.6), new(-63.3, -17.9)]);
        var zona = new Zona("Zona DEMO Piraí", "Datos ficticios para evaluación; no es un mapa oficial.", NivelRiesgo.Alto, poligono, new OrigenInstitucional("Ejercicio académico", "DEMO-001", new DateOnly(2026, 11, 18)));
        var comunidad = new Comunidad("Comunidad DEMO", "Santa Cruz", 100, fabrica.CreatePoint(new Coordinate(-63.2, -17.75)), zona.Id);
        db.Zonas.Add(zona); db.Comunidades.Add(comunidad); await db.SaveChangesAsync();
        Console.WriteLine("Demostración cargada: una zona y una comunidad. No se enviaron mensajes.");
    }
}
