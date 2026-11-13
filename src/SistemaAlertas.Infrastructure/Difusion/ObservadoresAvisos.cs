using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Avisos.Eventos;

namespace SistemaAlertas.Infrastructure.Difusion;

// Observadores de los eventos de aviso (patrón Observer). Ninguno es conocido por quien publica el evento.

/// <summary>Bitácora de cada aviso (RNF-04): quién lo registró, aprobó, descartó o difundió y cuándo. En memoria hasta el Sprint 3.</summary>
public sealed class BitacoraAvisosEnMemoria : IObservadorAvisos, IBitacoraAvisos
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<EventoAviso>> _porAviso = new();

    public Task AlOcurrirAsync(EventoAviso evento, CancellationToken ct = default)
    {
        _porAviso.GetOrAdd(evento.AvisoId, _ => new()).Enqueue(evento);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EventoAviso> De(Guid avisoId) =>
        _porAviso.TryGetValue(avisoId, out var cola) ? cola.ToList() : [];
}

/// <summary>Deja cada evento en el log de la API, para el monitoreo.</summary>
public sealed class ObservadorLogAvisos(ILogger<ObservadorLogAvisos> logger) : IObservadorAvisos
{
    public Task AlOcurrirAsync(EventoAviso e, CancellationToken ct = default)
    {
        logger.LogInformation("Aviso {AvisoId} {Tipo}: {Detalle}", e.AvisoId, e.Tipo, e.Detalle);
        return Task.CompletedTask;
    }
}

/// <summary>Cuando la difusión termina, pasa el aviso al estado Difundido (la transición la valida su State).</summary>
public sealed class MarcarAvisoDifundido(IAppDbContext db) : IObservadorAvisos
{
    public async Task AlOcurrirAsync(EventoAviso e, CancellationToken ct = default)
    {
        if (e.Tipo != TipoEventoAviso.Difundido) return;
        var aviso = await db.Avisos.FirstOrDefaultAsync(a => a.Id == e.AvisoId, ct);
        if (aviso is null || !aviso.EstadoActual.PermiteDifundir) return;
        aviso.MarcarDifundido();
        await db.SaveChangesAsync(ct);
    }
}
