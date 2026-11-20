using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Infrastructure.Sms.Decoradores;

/// <summary>
/// Decorator: protege a la población de los avisos de prueba o simulacro. Si el aviso es de prueba y el proveedor
/// envía a teléfonos reales, bloquea el envío; si el proveedor es el simulador, deja pasar el mensaje marcado con "[PRUEBA]".
/// </summary>
public sealed class EmisorModoPrueba(IEmisorSms interno) : EmisorSmsDecorador(interno)
{
    public const string Marca = "[PRUEBA] ";

    public override Task<ResultadoEnvioSms> EnviarAsync(MensajeSms mensaje, CancellationToken ct = default)
    {
        if (!mensaje.EsPrueba) return Interno.EnviarAsync(mensaje, ct);
        if (Interno.EnviaReal)
            return Task.FromResult(ResultadoEnvioSms.BloqueadoPorPrueba(
                $"Aviso de prueba: no se envía por {Interno.Proveedor} a teléfonos reales."));
        var texto = mensaje.Texto.StartsWith(Marca, StringComparison.Ordinal) ? mensaje.Texto : Marca + mensaje.Texto;
        return Interno.EnviarAsync(mensaje with { Texto = texto }, ct);
    }
}
