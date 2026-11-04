using Microsoft.EntityFrameworkCore;

namespace SistemaAlertas.Infrastructure.Persistence;

public class AlertasDbContext(DbContextOptions<AlertasDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlertasDbContext).Assembly);
    }
}
