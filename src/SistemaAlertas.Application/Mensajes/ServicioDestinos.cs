using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Application.Comunidades;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Mensajes;
using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Application.Mensajes;

/// <summary>Sección "Destinos" de la configuración: los canales de contacto aprobados para difundir alertas.</summary>
public sealed class OpcionesDestinos
{
    public const string Seccion = "Destinos";
    public List<TipoCanal> CanalesAprobados { get; set; } = [TipoCanal.Sms, TipoCanal.WhatsApp, TipoCanal.Llamada, TipoCanal.Radio];
}

/// <summary>
/// RF-07: selección por comunidad. Alcance = comunidades cuya zona toca el área de la versión y, si tienen ubicación,
/// que esa ubicación caiga dentro del área. De cada una entran sus contactos habilitados (activos y autorizados)
/// por los canales aprobados, sin repetir un destino (canal + dirección) en el envío. Las comunidades en alcance
/// que no aportan ningún destino quedan como pendientes, con el motivo.
/// </summary>
public sealed class ServicioDestinos(IAppDbContext db, OpcionesDestinos opciones, IUsuarioActual usuario,
    IPublicadorEventosAvisos eventos, TimeProvider? reloj = null)
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public IReadOnlyList<TipoCanal> CanalesAprobados => opciones.CanalesAprobados;

    /// <summary>Calcula la lista sin guardarla, para que el aprobador vea el alcance antes de decidir (RF-11).</summary>
    public async Task<ListaDestinosVista> PreviaAsync(Guid versionId, IReadOnlyList<TipoCanal>? canales, CancellationToken ct)
    {
        var version = await Buscar(versionId, ct);
        var (lista, comunidades) = await CalcularAsync(version, canales, Guid.Empty, ct);
        return await VistaAsync(lista, guardada: false, comunidades, ct);
    }

    /// <summary>Calcula y guarda la lista de destinos de una versión aprobada; reemplaza la anterior si la había.</summary>
    public async Task<ListaDestinosVista> GenerarAsync(Guid versionId, IReadOnlyList<TipoCanal>? canales, Guid usuarioId, CancellationToken ct)
    {
        var lista = await GenerarListaAsync(versionId, canales, usuarioId, ct);
        return await VistaAsync(lista.Lista, guardada: true, lista.Comunidades, ct);
    }

    public async Task<ListaDestinosVista?> ObtenerAsync(Guid versionId, CancellationToken ct)
    {
        var lista = await db.ListasDestinos.AsNoTracking().FirstOrDefaultAsync(l => l.VersionId == versionId, ct);
        return lista is null ? null : await VistaAsync(lista, guardada: true, null, ct);
    }

    /// <summary>Para la difusión: la lista guardada de la versión; si no hay, la genera con todos los canales aprobados.</summary>
    public async Task<ListaDestinos> ObtenerOGenerarAsync(Guid versionId, CancellationToken ct)
    {
        var lista = await db.ListasDestinos.AsNoTracking().FirstOrDefaultAsync(l => l.VersionId == versionId, ct);
        return lista ?? (await GenerarListaAsync(versionId, null, Guid.Empty, ct)).Lista;
    }

    private async Task<(ListaDestinos Lista, int Comunidades)> GenerarListaAsync(Guid versionId, IReadOnlyList<TipoCanal>? canales,
        Guid usuarioId, CancellationToken ct)
    {
        var version = await Buscar(versionId, ct);
        if (!version.EstadoActual.PermiteDifundir)
            throw new ExcepcionDominio($"La versión está {version.Estado}: los destinos se guardan para una versión aprobada.");

        var (lista, comunidades) = await CalcularAsync(version, canales, usuarioId, ct);
        var anterior = await db.ListasDestinos.FirstOrDefaultAsync(l => l.VersionId == versionId, ct);
        if (anterior is not null)
        {
            db.ListasDestinos.Remove(anterior);
            await db.SaveChangesAsync(ct);
        }
        db.ListasDestinos.Add(lista);
        await db.SaveChangesAsync(ct);
        await eventos.PublicarAsync(new EventoAviso(version.AvisoId, TipoEventoAviso.DestinosSeleccionados,
            usuarioId == Guid.Empty ? null : usuarioId,
            $"Versión {version.Numero}: {lista.Destinos.Count} destinos en {comunidades} comunidades, {lista.Pendientes.Count} pendientes ({string.Join(", ", lista.Canales)}).",
            _reloj.GetUtcNow(), version.Id), ct);
        return (lista, comunidades);
    }

    private async Task<(ListaDestinos Lista, int Comunidades)> CalcularAsync(VersionMensaje version, IReadOnlyList<TipoCanal>? canales,
        Guid usuarioId, CancellationToken ct)
    {
        var elegidos = canales is { Count: > 0 } ? canales.Distinct().ToList() : opciones.CanalesAprobados;
        var noAprobados = elegidos.Except(opciones.CanalesAprobados).ToList();
        if (noAprobados.Count > 0)
            throw new ExcepcionDominio($"Canal no aprobado para difusión: {string.Join(", ", noAprobados)}. Aprobados: {string.Join(", ", opciones.CanalesAprobados)}.");
        if (version.Area is null || version.Area.IsEmpty)
            throw new ExcepcionDominio("La versión no tiene área: no se puede saber qué comunidades alcanza.");

        var area = version.Area;
        var comunidades = await db.Comunidades.AsNoTracking()
            .Where(c => db.Zonas.Any(z => z.Id == c.ZonaId && z.Area.Intersects(area))
                        && (c.Ubicacion == null || c.Ubicacion.Intersects(area)))
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Nombre })
            .ToListAsync(ct);
        var ids = comunidades.Select(c => c.Id).ToList();
        var contactos = await db.Contactos.AsNoTracking().Where(c => ids.Contains(c.ComunidadId)).OrderBy(c => c.Nombre).ToListAsync(ct);

        var lista = new ListaDestinos(version.Id, elegidos, usuarioId);
        foreach (var comunidad in comunidades)
        {
            var suyos = contactos.Where(c => c.ComunidadId == comunidad.Id).ToList();
            var habilitados = suyos.Where(c => c.Habilitado).ToList();
            var conCanal = false;
            foreach (var contacto in habilitados)
                foreach (var canal in contacto.Canales.Where(k => elegidos.Contains(k.Tipo)).OrderByDescending(k => k.Preferido))
                {
                    conCanal = true;
                    lista.AgregarDestino(comunidad.Id, contacto.Id, canal.Tipo, canal.Destino);
                }

            if (conCanal) continue;
            var motivo = suyos.Count == 0 ? "Sin contactos registrados."
                : habilitados.Count == 0 ? "Ningún contacto habilitado (inactivos o sin autorización)."
                : $"Ningún contacto tiene un canal elegido ({string.Join(", ", elegidos)}).";
            lista.AgregarPendiente(comunidad.Id, comunidad.Nombre, motivo);
        }
        return (lista, comunidades.Count);
    }

    /// <summary>Los destinos son datos personales: sin permiso de edición se ven enmascarados (como en el Proxy de contactos).</summary>
    private async Task<ListaDestinosVista> VistaAsync(ListaDestinos lista, bool guardada, int? comunidades, CancellationToken ct)
    {
        var verCompleto = usuario.Tiene(Permiso.Edicion);
        var comunidadIds = lista.Destinos.Select(d => d.ComunidadId).Concat(lista.Pendientes.Select(p => p.ComunidadId)).Distinct().ToList();
        var nombresComunidad = await db.Comunidades.AsNoTracking().Where(c => comunidadIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nombre, ct);
        var contactoIds = lista.Destinos.Select(d => d.ContactoId).Distinct().ToList();
        var nombresContacto = await db.Contactos.AsNoTracking().Where(c => contactoIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nombre, ct);

        var destinos = lista.Destinos
            .Select(d => new DestinoVista(d.ComunidadId, nombresComunidad.GetValueOrDefault(d.ComunidadId, "?"), d.ContactoId,
                nombresContacto.GetValueOrDefault(d.ContactoId, "?"), d.Canal,
                verCompleto ? d.Direccion : EnmascaradorDatos.Destino(d.Canal, d.Direccion)))
            .OrderBy(d => d.Comunidad).ThenBy(d => d.Contacto).ThenBy(d => d.Canal)
            .ToList();
        var pendientes = lista.Pendientes.Select(p => new PendienteVista(p.ComunidadId, p.Comunidad, p.Motivo)).OrderBy(p => p.Comunidad).ToList();
        return new ListaDestinosVista(lista.VersionId, guardada, guardada ? lista.CreadoEn : null, lista.Canales,
            comunidades ?? destinos.Select(d => d.ComunidadId).Concat(pendientes.Select(p => p.ComunidadId)).Distinct().Count(),
            destinos, pendientes);
    }

    private async Task<VersionMensaje> Buscar(Guid versionId, CancellationToken ct) =>
        await db.VersionesMensaje.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new KeyNotFoundException("Versión no encontrada.");
}
