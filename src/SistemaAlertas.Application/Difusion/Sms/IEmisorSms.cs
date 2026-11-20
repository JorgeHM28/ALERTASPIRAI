namespace SistemaAlertas.Application.Difusion.Sms;

/// <param name="Destino">Teléfono en formato internacional, por ejemplo +59171234567.</param>
/// <param name="EsPrueba">El aviso es de prueba o simulacro (CAP status Test/Exercise): no debe llegar a la población.</param>
public sealed record MensajeSms(string Destino, string Texto, Guid? AvisoId = null, bool EsPrueba = false);

/// <param name="Recuperable">El fallo es pasajero (saturación, caída del proveedor, red): vale la pena reintentar.</param>
/// <param name="Bloqueado">No se envió a propósito (por ejemplo, un aviso de prueba con un proveedor real).</param>
/// <param name="Intentos">Cuántas veces se intentó enviar (lo completa el decorador de reintentos).</param>
public sealed record ResultadoEnvioSms(bool Exitoso, string? IdProveedor, string? Error, bool Recuperable,
    bool Bloqueado = false, int Intentos = 1)
{
    public static ResultadoEnvioSms Ok(string idProveedor) => new(true, idProveedor, null, false);
    public static ResultadoEnvioSms FalloRecuperable(string error) => new(false, null, error, true);
    public static ResultadoEnvioSms FalloDefinitivo(string error) => new(false, null, error, false);
    public static ResultadoEnvioSms BloqueadoPorPrueba(string motivo) => new(false, null, motivo, false, Bloqueado: true);
}

/// <summary>
/// Puerto de salida del canal SMS (RF-04). Es el objetivo del patrón Adapter (cada proveedor se adapta a él)
/// y el componente que envuelven los decoradores de reintentos, bitácora y modo prueba.
/// </summary>
public interface IEmisorSms
{
    /// <summary>Nombre del proveedor que está al final de la cadena (Simulador, Twilio).</summary>
    string Proveedor { get; }

    /// <summary>True si los mensajes llegan a teléfonos reales.</summary>
    bool EnviaReal { get; }

    Task<ResultadoEnvioSms> EnviarAsync(MensajeSms mensaje, CancellationToken ct = default);
}
