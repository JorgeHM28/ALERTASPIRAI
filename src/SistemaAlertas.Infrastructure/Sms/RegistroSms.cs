using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaAlertas.Application.Difusion.Sms;
using Twilio.Clients;

namespace SistemaAlertas.Infrastructure.Sms;

public static class RegistroSms
{
    /// <summary>Registra el canal SMS. El proveedor se elige con "Sms:Modo" (Simulador por defecto).</summary>
    public static IServiceCollection AddCanalSms(this IServiceCollection services, IConfiguration configuration)
    {
        var opciones = configuration.GetSection(OpcionesSms.Seccion).Get<OpcionesSms>() ?? new OpcionesSms();
        services.AddSingleton(opciones);
        services.AddSingleton<SimuladorEmisor>();

        if (opciones.Modo == ModoSms.Twilio)
        {
            var t = opciones.Twilio;
            if (string.IsNullOrWhiteSpace(t.AccountSid) || string.IsNullOrWhiteSpace(t.AuthToken) || string.IsNullOrWhiteSpace(t.NumeroOrigen))
                throw new InvalidOperationException(
                    "Sms:Modo es Twilio pero faltan Sms:Twilio:AccountSid, AuthToken o NumeroOrigen (configúralos con dotnet user-secrets).");
            services.AddSingleton<ITwilioRestClient>(_ => new TwilioRestClient(t.AccountSid, t.AuthToken));
            services.AddSingleton(sp => new TwilioEmisor(sp.GetRequiredService<ITwilioRestClient>(), t));
        }

        services.AddSingleton<IEmisorSms>(sp => opciones.Modo == ModoSms.Twilio
            ? sp.GetRequiredService<TwilioEmisor>()
            : sp.GetRequiredService<SimuladorEmisor>());
        return services;
    }
}
