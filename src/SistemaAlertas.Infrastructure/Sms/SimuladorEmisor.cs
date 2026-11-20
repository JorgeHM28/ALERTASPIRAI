using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SistemaAlertas.Application.Comunidades;
using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Infrastructure.Sms;

/// <summary>
/// Emisor para desarrollo y demos (Sms:Modo = Simulador, el valor por defecto): no envía nada,
/// registra el mensaje y responde como si el proveedor lo hubiera aceptado.
/// </summary>
public sealed class SimuladorEmisor(ILogger<SimuladorEmisor> logger) : IEmisorSms
{
    private readonly ConcurrentQueue<MensajeSms> _enviados = new();

    public string Proveedor => "Simulador";
    public bool EnviaReal => false;

    /// <summary>Mensajes "enviados" desde que arrancó la aplicación (para la demo y las pruebas).</summary>
    public IReadOnlyCollection<MensajeSms> Enviados => _enviados.ToArray();

    public Task<ResultadoEnvioSms> EnviarAsync(MensajeSms mensaje, CancellationToken ct = default)
    {
        _enviados.Enqueue(mensaje);
        logger.LogInformation("SMS simulado a {Destino}: {Texto}", EnmascaradorDatos.Telefono(mensaje.Destino), mensaje.Texto);
        return Task.FromResult(ResultadoEnvioSms.Ok($"SIM-{Guid.NewGuid():N}"));
    }
}
