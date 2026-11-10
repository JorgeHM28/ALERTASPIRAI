using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Infrastructure.Sms.Decoradores;

/// <summary>
/// Decorator: reintenta el envío solo si el fallo es recuperable (saturación, caída del proveedor, red),
/// con un máximo de 2 reintentos adicionales (RNF-08). Un fallo definitivo, como un número inválido, no se reintenta.
/// </summary>
public sealed class EmisorConReintentos : EmisorSmsDecorador
{
    public const int MaximoPermitido = 2;

    private readonly int _maxReintentos;
    private readonly Func<int, CancellationToken, Task> _esperar;

    /// <param name="esperar">Espera antes del reintento n (1, 2). Por defecto, esperaBaseMs × n.</param>
    public EmisorConReintentos(IEmisorSms interno, int maxReintentos = MaximoPermitido, int esperaBaseMs = 500,
        Func<int, CancellationToken, Task>? esperar = null) : base(interno)
    {
        _maxReintentos = Math.Clamp(maxReintentos, 0, MaximoPermitido);
        _esperar = esperar ?? ((n, ct) => Task.Delay(TimeSpan.FromMilliseconds(esperaBaseMs * n), ct));
    }

    public override async Task<ResultadoEnvioSms> EnviarAsync(MensajeSms mensaje, CancellationToken ct = default)
    {
        ResultadoEnvioSms resultado;
        var intento = 0;
        while (true)
        {
            intento++;
            try
            {
                resultado = await Interno.EnviarAsync(mensaje, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resultado = ResultadoEnvioSms.FalloRecuperable($"Error inesperado del proveedor: {ex.Message}");
            }

            if (resultado.Exitoso || !resultado.Recuperable || intento > _maxReintentos) break;
            await _esperar(intento, ct);
        }
        return resultado with { Intentos = intento };
    }
}
