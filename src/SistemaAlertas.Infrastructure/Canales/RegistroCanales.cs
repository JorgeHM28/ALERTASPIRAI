using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaAlertas.Application.Difusion.Canales;
using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Infrastructure.Canales;

public static class RegistroCanales
{
    /// <summary>
    /// Registra los canales automáticos de la cola: SMS siempre (con el proveedor de Sms:Modo), y WhatsApp a grupos (WAHA)
    /// y Email (SMTP) solo si su sección de configuración está completa. Un canal sin configurar no recibe envíos.
    /// </summary>
    public static IServiceCollection AddCanalesAutomaticos(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IEmisorCanal>(sp => new CanalSms(sp.GetRequiredService<IEmisorSms>()));

        var whatsapp = configuration.GetSection(OpcionesWhatsApp.Seccion).Get<OpcionesWhatsApp>() ?? new OpcionesWhatsApp();
        services.AddSingleton(whatsapp);
        if (whatsapp.Habilitado)
        {
            if (!Uri.TryCreate(whatsapp.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new InvalidOperationException($"WhatsApp:Url no es una URL válida: {whatsapp.Url}");
            var malos = whatsapp.GruposValidos.Where(g => !g.Id.EndsWith("@g.us", StringComparison.Ordinal)).Select(g => g.Id).ToList();
            if (malos.Count > 0)
                throw new InvalidOperationException($"El id de un grupo de WhatsApp termina en @g.us: revisa {string.Join(", ", malos)}.");
            services.AddSingleton<IEmisorCanal>(_ => new WahaEmisor(
                new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(1, whatsapp.TiempoEsperaSegundos)) }, whatsapp));
        }

        var email = configuration.GetSection(OpcionesEmail.Seccion).Get<OpcionesEmail>() ?? new OpcionesEmail();
        services.AddSingleton(email);
        if (email.Habilitado)
        {
            services.AddSingleton<IClienteSmtp>(new ClienteSmtp(email));
            services.AddSingleton<IEmisorCanal>(sp => new SmtpEmisor(sp.GetRequiredService<IClienteSmtp>(), email));
        }
        return services;
    }
}
