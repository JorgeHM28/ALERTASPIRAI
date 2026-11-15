using Microsoft.Extensions.Logging;

namespace SistemaAlertas.Application.Avisos.Eventos;

public enum TipoEventoAviso
{
    Registrado, Aprobado, Descartado, Vencido, Difundido,
    // Sprint 2: versiones del mensaje (RF-02, RF-11) y destinos (RF-07).
    VersionPreparada, VersionEnviadaARevision, VersionRechazada, VersionCorregida, DestinosSeleccionados
}

/// <summary>Algo que le pasó a un aviso. Quien lo causó puede ser null si fue un proceso del sistema (vencimiento).</summary>
/// <param name="VersionId">Versión del mensaje involucrada, si el evento es de una versión (Sprint 2).</param>
public sealed record EventoAviso(Guid AvisoId, TipoEventoAviso Tipo, Guid? UsuarioId, string Detalle, DateTimeOffset Fecha,
    Guid? VersionId = null);

/// <summary>Observador del patrón Observer: reacciona a los eventos de los avisos sin que el emisor lo conozca.</summary>
public interface IObservadorAvisos
{
    Task AlOcurrirAsync(EventoAviso evento, CancellationToken ct = default);
}

/// <summary>Sujeto del patrón Observer: los casos de uso publican aquí y no saben quién escucha.</summary>
public interface IPublicadorEventosAvisos
{
    Task PublicarAsync(EventoAviso evento, CancellationToken ct = default);
}

/// <summary>Bitácora de eventos de los avisos (RNF-04): uno de los observadores.</summary>
public interface IBitacoraAvisos
{
    IReadOnlyList<EventoAviso> De(Guid avisoId);
}

/// <summary>
/// Patrón Observer. Avisa a todos los observadores registrados en el contenedor (bitácora, registro en el log,
/// cambio a "Difundido"). Agregar una reacción nueva, por ejemplo un correo al COED en el Sprint 3, es registrar
/// otro observador: los casos de uso no cambian. Si un observador falla, los demás reciben igual el evento
/// y la acción principal (aprobar, difundir) no se deshace.
/// </summary>
public sealed class PublicadorEventosAvisos(IEnumerable<IObservadorAvisos> observadores,
    ILogger<PublicadorEventosAvisos>? logger = null) : IPublicadorEventosAvisos
{
    public async Task PublicarAsync(EventoAviso evento, CancellationToken ct = default)
    {
        foreach (var observador in observadores)
        {
            try
            {
                await observador.AlOcurrirAsync(evento, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un observador caído no frena a los demás ni a quien publicó el evento.
                logger?.LogWarning(ex, "El observador {Observador} falló con el evento {Tipo} del aviso {AvisoId}",
                    observador.GetType().Name, evento.Tipo, evento.AvisoId);
            }
        }
    }
}
