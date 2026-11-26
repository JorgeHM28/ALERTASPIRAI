using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Infrastructure.Difusion;

// Observadores de los eventos de aviso (patrón Observer). Ninguno es conocido por quien publica el evento.

/// <summary>Bitácora de cada aviso en memoria. La API usa <see cref="BitacoraAvisosPersistente"/>; esta queda para las pruebas.</summary>
public sealed class BitacoraAvisosEnMemoria : IObservadorAvisos, IBitacoraAvisos
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<EventoAviso>> _porAviso = new();

    public Task AlOcurrirAsync(EventoAviso evento, CancellationToken ct = default)
    {
        _porAviso.GetOrAdd(evento.AvisoId, _ => new()).Enqueue(evento);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventoAviso>> DeAsync(Guid avisoId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<EventoAviso>>(_porAviso.TryGetValue(avisoId, out var cola) ? cola.ToList() : []);
}

/// <summary>
/// Bitácora de cada aviso (RNF-04), guardada en la base (Sprint 3): quién lo registró, preparó, aprobó, rechazó,
/// difundió, y las incidencias y tareas de la difusión. Sobrevive a un reinicio de la API.
/// </summary>
public sealed class BitacoraAvisosPersistente(IAppDbContext db) : IObservadorAvisos, IBitacoraAvisos
{
    public async Task AlOcurrirAsync(EventoAviso e, CancellationToken ct = default)
    {
        db.EventosAviso.Add(new RegistroEventoAviso(e.AvisoId, e.VersionId, e.Tipo.ToString(), e.UsuarioId, e.Detalle, e.Fecha));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EventoAviso>> DeAsync(Guid avisoId, CancellationToken ct = default)
    {
        var filas = await db.EventosAviso.AsNoTracking().Where(r => r.AvisoId == avisoId).OrderBy(r => r.Fecha).ToListAsync(ct);
        return filas.Select(r => new EventoAviso(r.AvisoId,
            Enum.TryParse<TipoEventoAviso>(r.Tipo, out var tipo) ? tipo : TipoEventoAviso.Incidencia,
            r.UsuarioId, r.Detalle, r.Fecha, r.VersionId)).ToList();
    }
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

/// <summary>
/// Cuando la difusión termina, pasa la versión a Difundida y el aviso a Difundido (cada transición la valida su State).
/// </summary>
public sealed class MarcarAvisoDifundido(IAppDbContext db, TimeProvider reloj) : IObservadorAvisos
{
    public async Task AlOcurrirAsync(EventoAviso e, CancellationToken ct = default)
    {
        if (e.Tipo != TipoEventoAviso.Difundido) return;
        if (e.VersionId is { } versionId)
        {
            var version = await db.VersionesMensaje.FirstOrDefaultAsync(v => v.Id == versionId, ct);
            if (version is not null && version.EstadoActual.PermiteDifundir) version.MarcarDifundida(reloj.GetUtcNow());
        }
        var aviso = await db.Avisos.FirstOrDefaultAsync(a => a.Id == e.AvisoId, ct);
        if (aviso is not null && aviso.EstadoActual.PermiteDifundir) aviso.MarcarDifundido();
        await db.SaveChangesAsync(ct);
    }
}
