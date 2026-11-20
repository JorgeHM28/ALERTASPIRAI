using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Infrastructure.Sms;

/// <summary>
/// Bitácora del canal SMS: escribe cada registro en el log estructurado y guarda los últimos 1000 en memoria.
/// La persistencia en la base de datos llega con la cola de envíos de RF-04.
/// </summary>
public sealed class BitacoraEnviosEnMemoria(ILogger<BitacoraEnviosEnMemoria> logger) : IBitacoraEnvios
{
    private const int Capacidad = 1000;
    private readonly ConcurrentQueue<RegistroEnvioSms> _registros = new();

    public Task RegistrarAsync(RegistroEnvioSms r, CancellationToken ct = default)
    {
        _registros.Enqueue(r);
        while (_registros.Count > Capacidad && _registros.TryDequeue(out _)) { }
        logger.LogInformation(
            "SMS {Resultado} por {Proveedor} a {Destino} (aviso {AvisoId}, prueba {EsPrueba}, intentos {Intentos}): {Detalle}",
            r.Exitoso ? "enviado" : r.Bloqueado ? "bloqueado" : "fallido", r.Proveedor, r.DestinoEnmascarado, r.AvisoId,
            r.EsPrueba, r.Intentos, r.Exitoso ? r.IdProveedor : r.Error);
        return Task.CompletedTask;
    }

    public IReadOnlyList<RegistroEnvioSms> Recientes(int cantidad = 100) => _registros.Reverse().Take(cantidad).ToList();
}
