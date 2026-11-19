using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Infrastructure.Difusion;
using SistemaAlertas.Infrastructure.Geo;
using SistemaAlertas.Application.Comunidades;
using SistemaAlertas.Infrastructure.Persistence;
using SistemaAlertas.Infrastructure.Persistence.Repositorios;
using SistemaAlertas.Infrastructure.Seguridad;
using SistemaAlertas.Infrastructure.Sms;

namespace SistemaAlertas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Alertas")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'Alertas'.");

        // SqlClient administra el pool de conexiones.
        services.AddDbContext<AlertasDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.UseNetTopologySuite())
                   .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AlertasDbContext>());

        // Proxy de protección (RF-12): quien pide IRepositorioContactos recibe el proxy, que envuelve al repositorio real.
        // IUsuarioActual lo registra la capa que conoce la petición (la API).
        services.AddScoped<RepositorioContactos>();
        services.AddScoped<IRepositorioContactos>(sp => new ProxyRepositorioContactos(
            sp.GetRequiredService<RepositorioContactos>(), sp.GetRequiredService<IUsuarioActual>()));

        var jwt = configuration.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>() ?? new OpcionesJwt();
        if (jwt.Clave.Length < 32)
            throw new InvalidOperationException("Jwt:Clave debe tener al menos 32 caracteres.");
        services.AddSingleton(jwt);
        services.AddSingleton<IHasherContrasena, HasherContrasena>();
        services.AddSingleton<IGeneradorToken, GeneradorTokenJwt>();

        // Adapter: NetTopologySuite/SQL Server a GeoJSON para el mapa (RF-09).
        services.AddSingleton<IConversorGeoJson, ConversorGeoJson>();

        // Adapter: SimuladorEmisor o TwilioEmisor según Sms:Modo (RF-04).
        services.AddCanalSms(configuration);

        // Subsistema de difusión (RF-04) que coordina la fachada ServicioDifusion.
        // La difusión usa el repositorio real (sin proxy): es un proceso del sistema y necesita los números completos.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDestinatariosDifusion, DestinatariosDesdeLista>();
        services.AddScoped<IFuenteVersionesAprobadas, FuenteVersionesAprobadas>();
        services.AddSingleton<IPublicadorWeb, PublicadorWebEnMemoria>();
        services.AddSingleton<ColaSmsEnMemoria>();
        services.AddSingleton<IColaSms>(sp => sp.GetRequiredService<ColaSmsEnMemoria>());
        services.AddSingleton<IGestorTareasManuales, GestorTareasManualesEnMemoria>();
        services.AddSingleton<IRegistroIncidencias, RegistroIncidenciasEnLog>();

        // Observer: cada observador reacciona a los eventos de aviso por su cuenta.
        services.AddSingleton<BitacoraAvisosEnMemoria>();
        services.AddSingleton<IBitacoraAvisos>(sp => sp.GetRequiredService<BitacoraAvisosEnMemoria>());
        services.AddSingleton<IObservadorAvisos>(sp => sp.GetRequiredService<BitacoraAvisosEnMemoria>());
        services.AddSingleton<IObservadorAvisos, ObservadorLogAvisos>();
        services.AddScoped<IObservadorAvisos, MarcarAvisoDifundido>();
        return services;
    }
}
