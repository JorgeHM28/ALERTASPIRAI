using Microsoft.Extensions.DependencyInjection;
using SistemaAlertas.Application.Avisos;
using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Application.Cap;
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
        services.AddSingleton<IFabricaImportadores, FabricaImportadores>();

        // Facade del subsistema CAP: importar, generar y validar.
        services.AddSingleton<IValidadorCap, ValidadorCap>();
        services.AddSingleton<IModuloCap, ModuloCap>();

        // Scoped: dependen del DbContext, que es una instancia por petición.
        services.AddScoped<ServicioAvisos>();
        services.AddScoped<ServicioUsuarios>();
        return services;
    }
}
