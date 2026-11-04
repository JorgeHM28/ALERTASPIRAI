using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaAlertas.Infrastructure.Persistence;

namespace SistemaAlertas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Alertas")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'Alertas'.");

        services.AddDbContext<AlertasDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite()));

        return services;
    }
}
