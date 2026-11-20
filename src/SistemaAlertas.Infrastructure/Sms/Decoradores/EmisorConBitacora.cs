using SistemaAlertas.Application.Comunidades;
using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Infrastructure.Sms.Decoradores;

/// <summary>
/// Decorator: deja en la bitácora cada acción del canal SMS (RNF-04): envío correcto, fallo, bloqueo por prueba
/// o excepción, con el destino enmascarado. Va por fuera de la cadena para registrar el resultado final.
/// </summary>
public sealed class EmisorConBitacora(IEmisorSms interno, IBitacoraEnvios bitacora, TimeProvider? reloj = null)
    : EmisorSmsDecorador(interno)
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public override async Task<ResultadoEnvioSms> EnviarAsync(MensajeSms mensaje, CancellationToken ct = default)
    {
        ResultadoEnvioSms resultado;
        try
        {
            resultado = await Interno.EnviarAsync(mensaje, ct);
        }
        catch (Exception ex)
        {
            await Registrar(mensaje, ResultadoEnvioSms.FalloDefinitivo($"Excepción: {ex.Message}"), CancellationToken.None);
            throw;
        }
        await Registrar(mensaje, resultado, ct);
        return resultado;
    }

    private Task Registrar(MensajeSms m, ResultadoEnvioSms r, CancellationToken ct) => bitacora.RegistrarAsync(new RegistroEnvioSms(
        _reloj.GetUtcNow(), Proveedor, EnmascaradorDatos.Telefono(m.Destino), m.AvisoId, m.EsPrueba,
        r.Exitoso, r.Bloqueado, r.Intentos, r.IdProveedor, r.Error), ct);
}
