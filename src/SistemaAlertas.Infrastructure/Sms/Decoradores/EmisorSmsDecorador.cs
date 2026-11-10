using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Infrastructure.Sms.Decoradores;

/// <summary>
/// Patrón Decorator (decorador base). Implementa <see cref="IEmisorSms"/> y envuelve otro <see cref="IEmisorSms"/>:
/// cada decorador agrega una responsabilidad (reintentos, bitácora, modo prueba) sin tocar el emisor concreto,
/// y se pueden encadenar en cualquier orden desde el registro de DI.
/// </summary>
public abstract class EmisorSmsDecorador(IEmisorSms interno) : IEmisorSms
{
    protected IEmisorSms Interno { get; } = interno;

    public string Proveedor => Interno.Proveedor;
    public bool EnviaReal => Interno.EnviaReal;

    public abstract Task<ResultadoEnvioSms> EnviarAsync(MensajeSms mensaje, CancellationToken ct = default);
}
