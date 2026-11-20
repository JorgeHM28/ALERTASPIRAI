namespace SistemaAlertas.Application.Difusion.Sms;

/// <summary>Un envío SMS tal como queda en la bitácora (RNF-04). El destino va enmascarado.</summary>
public sealed record RegistroEnvioSms(DateTimeOffset Fecha, string Proveedor, string DestinoEnmascarado, Guid? AvisoId,
    bool EsPrueba, bool Exitoso, bool Bloqueado, int Intentos, string? IdProveedor, string? Error);

/// <summary>Bitácora de las acciones del canal SMS (RNF-04).</summary>
public interface IBitacoraEnvios
{
    Task RegistrarAsync(RegistroEnvioSms registro, CancellationToken ct = default);

    /// <summary>Los últimos registros, del más reciente al más antiguo.</summary>
    IReadOnlyList<RegistroEnvioSms> Recientes(int cantidad = 100);
}
