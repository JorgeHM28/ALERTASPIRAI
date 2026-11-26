using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Infrastructure.Sms.Decoradores;
using Twilio.Clients;

namespace SistemaAlertas.Infrastructure.Sms;

public static class RegistroSms
{
    /// <summary>
    /// Registra el canal SMS. El proveedor se elige con "Sms:Modo" (Simulador por defecto) y queda envuelto
    /// en los decoradores de bitácora, modo prueba y reintentos.
    /// </summary>
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

        services.AddSingleton<IBitacoraEnvios, BitacoraEnviosEnMemoria>();

        // Decorator: la cadena se arma a mano, de afuera hacia adentro: bitácora → modo prueba → proveedor (Adapter).
        // Sprint 3: los reintentos de RNF-08 esperan 60 s entre intentos, así que ya no los hace EmisorConReintentos
        // dentro de la misma llamada (bloquearía la cola): los agenda la cola persistente (EnvioSms.RegistrarIntento).
        // Cada intento queda en la bitácora; el modo prueba va antes del proveedor para que un aviso de prueba no salga.
        services.AddSingleton<IEmisorSms>(sp =>
        {
            IEmisorSms proveedor = opciones.Modo == ModoSms.Twilio
                ? sp.GetRequiredService<TwilioEmisor>()
                : sp.GetRequiredService<SimuladorEmisor>();
            return new EmisorConBitacora(new EmisorModoPrueba(proveedor), sp.GetRequiredService<IBitacoraEnvios>());
        });
        return services;
    }
}
