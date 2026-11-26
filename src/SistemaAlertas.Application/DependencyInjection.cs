using Microsoft.Extensions.DependencyInjection;
using SistemaAlertas.Application.Avisos;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Application.Mensajes;
using SistemaAlertas.Application.Usuarios;

namespace SistemaAlertas.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Singleton vía DI: importadores y fábrica no tienen estado por petición.
        // El contenedor garantiza una única instancia sin el Singleton clásico (constructor privado + Instance estático).
        services.AddSingleton<IImportadorAviso, ImportadorCapXml>();
        services.AddSingleton<IImportadorAviso, ImportadorCapJson>();
        services.AddSingleton<IImportadorAviso, ImportadorCapExcel>();
        services.AddSingleton<IFabricaImportadores, FabricaImportadores>();

        // Facade del subsistema CAP: importar, generar y validar.
        services.AddSingleton<IValidadorCap, ValidadorCap>();
        services.AddSingleton<IModuloCap, ModuloCap>();

        // Scoped: dependen del DbContext, que es una instancia por petición.
        services.AddScoped<ServicioAvisos>();
        services.AddScoped<ServicioUsuarios>();
        services.AddScoped<ServicioVersiones>();
        services.AddScoped<ServicioDestinos>();

        // Observer: los casos de uso publican eventos de aviso; los observadores se registran aparte (Infrastructure).
        services.AddScoped<IPublicadorEventosAvisos, PublicadorEventosAvisos>();

        // Chain of Responsibility: reglas que la versión aprobada debe pasar antes de difundirse. Sin estado: una sola instancia.
        services.AddSingleton(ReglaDifusion.CadenaPorDefecto());

        // Strategy: una redacción por canal manual.
        foreach (var redaccion in RedaccionesCanal.PorDefecto)
            services.AddSingleton(typeof(IRedaccionCanal), redaccion);

        // Command: un comando por canal, en el orden en que se ejecutan. Agregar un canal es una línea más.
        services.AddScoped<IComandoDifusion, PublicarEnWeb>();
        services.AddScoped<IComandoDifusion, EncolarSms>();
        services.AddScoped<IComandoDifusion, CrearTareasManuales>();

        // Facade de RF-04 e invocador de los comandos: una llamada difunde por todos los canales.
        services.AddScoped<IServicioDifusion, ServicioDifusion>();
        services.AddScoped<ServicioSeguimiento>();
        return services;
    }
}
