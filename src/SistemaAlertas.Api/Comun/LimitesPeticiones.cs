using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SistemaAlertas.Api.Comun;

/// <summary>
/// Límite de intentos para el login y el registro propio, por dirección IP. Frena la prueba de contraseñas
/// por fuerza bruta y la creación masiva de cuentas. El tope se configura en LimiteAcceso:PorMinuto.
/// </summary>
public static class LimitesPeticiones
{
    public const string Acceso = "acceso";

    public static IServiceCollection AddLimitesPeticiones(this IServiceCollection services, IConfiguration configuration)
    {
        var porMinuto = configuration.GetValue("LimiteAcceso:PorMinuto", 10);
        return services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(Acceso, contexto => RateLimitPartition.GetFixedWindowLimiter(
                contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = porMinuto, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }
}
