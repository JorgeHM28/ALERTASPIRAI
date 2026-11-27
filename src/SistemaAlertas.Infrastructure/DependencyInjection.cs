using SistemaAlertas.Domain.Mensajes;
using SistemaAlertas.Domain.Zonas;
using SistemaAlertas.Domain.Comunidades;
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

        // Npgsql ya mantiene su propio pool de conexiones: por eso no se implementa Object Pool.
        services.AddDbContext<AlertasDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
                   .UseSnakeCaseNamingConvention()
                   .UseAsyncSeeding(async (context, _, ct) =>
                   {
                       var db = (AlertasDbContext)context;
                       // Datos reales de la cuenca del Piraí: 22 municipios (zonas) y sus localidades (comunidades).
                       if (configuration["Semilla:CuencaPirai"] == "true" && !await db.Zonas.AnyAsync(ct))
                       {
                           var connection = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
                           await connection.ReloadTypesAsync(ct);
                           await SemillaCuencaPirai.CargarAsync(db, ct);
                       }
                       if (configuration["Semilla:DatosDemo"] != "true") return;
                       // RF-05: dos protocolos de ejemplo (ficticios) para poder vincular instrucciones en la demo.
                       if (!await db.Protocolos.AnyAsync(ct))
                       {
                           db.Protocolos.Add(new ProtocoloOficial("Evacuación por crecida del río (demo)",
                               "Diríjase de inmediato a la zona alta o al punto de encuentro de su comunidad. Lleve documentos, agua y medicamentos. No cruce el río ni zonas inundadas.",
                               "Datos de demostración", "DEMO-PROT-01", new DateOnly(2026, 1, 15)));
                           db.Protocolos.Add(new ProtocoloOficial("Vigilancia ante posible desborde (demo)",
                               "Esté atento a los avisos oficiales. Prepare una mochila de emergencia y ubique la ruta de evacuación. Aleje a niños y animales de la orilla.",
                               "Datos de demostración", "DEMO-PROT-02", new DateOnly(2026, 1, 15)));
                           await db.SaveChangesAsync(ct);
                       }
                   }));
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

        // Adapter: NetTopologySuite/PostGIS a GeoJSON para el mapa (RF-09).
        services.AddSingleton<IConversorGeoJson, ConversorGeoJson>();

        // Adapter: SimuladorEmisor o TwilioEmisor según Sms:Modo (RF-04).
        services.AddCanalSms(configuration);

        // Subsistema de difusión (RF-04) que coordina la fachada ServicioDifusion.
        // La difusión usa el repositorio real (sin proxy): es un proceso del sistema y necesita los números completos.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDestinatariosDifusion, DestinatariosDesdeLista>();
        services.AddScoped<IFuenteVersionesAprobadas, FuenteVersionesAprobadas>();
        // Sprint 3: todo lo de la difusión se guarda en la base (CAP emitido, envíos SMS, tareas, incidencias y bitácora).
        services.AddScoped<IArchivoCap, ArchivoCapPersistente>();
        services.AddScoped<IVersionesDifusion, VersionesDifusionEf>();
        services.AddScoped<IDetenedorDespachosAnteriores, DetenedorDespachosAnteriores>();
        services.AddScoped<IPublicadorWeb, PublicadorWebPersistente>();
        services.AddScoped<IColaSms, ColaSmsPersistente>();
        services.AddScoped<DespachadorSms>();
        services.AddScoped<IGestorTareasManuales, GestorTareasPersistente>();
        services.AddScoped<IRegistroIncidencias, RegistroIncidenciasPersistente>();

        // Observer: cada observador reacciona a los eventos de aviso por su cuenta.
        services.AddScoped<BitacoraAvisosPersistente>();
        services.AddScoped<IBitacoraAvisos>(sp => sp.GetRequiredService<BitacoraAvisosPersistente>());
        services.AddScoped<IObservadorAvisos>(sp => sp.GetRequiredService<BitacoraAvisosPersistente>());
        services.AddSingleton<IObservadorAvisos, ObservadorLogAvisos>();
        services.AddScoped<IObservadorAvisos, MarcarAvisoDifundido>();
        return services;
    }
}
