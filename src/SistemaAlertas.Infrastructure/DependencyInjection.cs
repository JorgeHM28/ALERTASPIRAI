using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Infrastructure.Persistence;
using SistemaAlertas.Infrastructure.Seguridad;

namespace SistemaAlertas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Alertas")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'Alertas'.");

        // Npgsql ya mantiene su propio pool de conexiones: por eso no se implementa Object Pool.
        services.AddDbContext<AlertasDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AlertasDbContext>());

        var jwt = configuration.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();
        if (jwt.Clave.Length < 32)
            throw new InvalidOperationException("Jwt:Clave debe tener al menos 32 caracteres.");
        services.AddSingleton(jwt);
        services.AddSingleton<IHasherContrasena, HasherContrasena>();
        services.AddSingleton<IGeneradorToken, GeneradorTokenJwt>();
        return services;
    }
}
