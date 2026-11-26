using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Application.Difusion;

public sealed record ConteoEstados(int Total, int Pendientes, int Enviados, int Fallidos, int Bloqueados, int Confirmadas, int NoRealizadas)
{
    public int Terminados => Total - Pendientes;
    public static ConteoEstados De(IEnumerable<EstadoTrabajo> estados)
    {
        var l = estados.ToList();
        return new(l.Count, l.Count(e => e == EstadoTrabajo.Pendiente), l.Count(e => e == EstadoTrabajo.Enviado),
            l.Count(e => e == EstadoTrabajo.Fallido), l.Count(e => e == EstadoTrabajo.Bloqueado),
            l.Count(e => e == EstadoTrabajo.Confirmada), l.Count(e => e == EstadoTrabajo.NoRealizada));
    }
}

public sealed record CapEmitidoVista(Guid Id, string Identificador, string Tipo, string Estado, DateTimeOffset Enviado, string Sha256,
    bool Valido, Guid? ReferenciaCapId, DateTimeOffset EmitidoEn);

/// <summary>Una fila del resumen de seguimiento (RF-08).</summary>
/// <param name="Avance">Porcentaje de trabajos terminados (enviados, fallidos, confirmados...), de 0 a 100.</param>
public sealed record SeguimientoResumen(Guid VersionId, Guid AvisoId, int Numero, string Tipo, string Titular, bool EsPrueba,
    DateTimeOffset DifundidaEn, ConteoEstados Sms, ConteoEstados Tareas, int Avance, bool Completo);

public sealed record SeguimientoComunidad(Guid? ComunidadId, string Comunidad, string? Municipio, ConteoEstados Sms,
    EstadoTrabajo? Altavoz);

public sealed record TareaVista(Guid Id, CanalDifusion Canal, Guid? ComunidadId, string? Comunidad, EstadoTrabajo Estado,
    string TextoLegible, string? Observacion, string? ConfirmadoPor, DateTimeOffset CreadaEn, DateTimeOffset? ResultadoEn);

public sealed record EnvioSmsVista(Guid Id, Guid? ComunidadId, string? Comunidad, string Destino, EstadoTrabajo Estado, int Intentos,
    string? CodigoProveedor, string? UltimoError, DateTimeOffset? ProximoIntento, DateTimeOffset CreadoEn, DateTimeOffset? ResultadoEn);

/// <param name="SmsTerminadosEn">Cuándo terminó el último SMS (enviado, fallido o bloqueado), si ya no queda ninguno pendiente.</param>
/// <param name="DentroDeMeta">RNF-02: todos los SMS terminaron dentro de los 5 minutos desde la difusión. Null mientras falten.</param>
public sealed record SeguimientoDetalle(SeguimientoResumen Resumen, CapEmitidoVista? Cap, DateTimeOffset? PublicadaWebEn,
    IReadOnlyList<SeguimientoComunidad> Comunidades, IReadOnlyList<TareaVista> Tareas, IReadOnlyList<EnvioSmsVista> Envios,
    DateTimeOffset? SmsTerminadosEn, bool? DentroDeMeta, DateTimeOffset ActualizadoEn);

/// <summary>
/// RF-08: seguimiento de la difusión. Junta, para cada versión difundida, el CAP emitido, el estado de los SMS
/// y de las tareas manuales por comunidad y canal. El panel lo vuelve a pedir cada pocos segundos.
/// También confirma o marca como no realizadas las tareas manuales.
/// </summary>
public sealed class ServicioSeguimiento(IAppDbContext db, IUsuarioActual usuario, TimeProvider reloj,
    IPublicadorEventosAvisos? eventos = null)
{
    /// <summary>RNF-02: el lote de SMS debe quedar procesado en 5 minutos.</summary>
    public static readonly TimeSpan MetaLote = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<SeguimientoResumen>> ListarAsync(CancellationToken ct)
    {
        var versiones = await db.VersionesMensaje.AsNoTracking().Where(v => v.DifundidaEn != null)
            .OrderByDescending(v => v.DifundidaEn).Take(100).ToListAsync(ct);
        var ids = versiones.Select(v => v.Id).ToList();
        var trabajos = await db.TrabajosDifusion.AsNoTracking().Where(t => ids.Contains(t.VersionId))
            .Select(t => new { t.VersionId, t.Canal, t.Estado }).ToListAsync(ct);
        return versiones.Select(v =>
        {
            var propios = trabajos.Where(t => t.VersionId == v.Id).ToList();
            return Resumen(v.Id, v.AvisoId, v.Numero, v.Tipo.ToString(), v.Contenido.Titular ?? v.Contenido.Evento, v.EsPrueba,
                v.DifundidaEn!.Value, propios.Where(t => t.Canal == CanalDifusion.Sms).Select(t => t.Estado),
                propios.Where(t => t.Canal != CanalDifusion.Sms).Select(t => t.Estado));
        }).ToList();
    }

    public async Task<SeguimientoDetalle?> DetalleAsync(Guid versionId, CancellationToken ct)
    {
        var v = await db.VersionesMensaje.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId, ct);
        if (v?.DifundidaEn is null) return null;

        var cap = await db.CapsEmitidos.AsNoTracking().Where(c => c.VersionId == versionId)
            .Select(c => new CapEmitidoVista(c.Id, c.Identificador, c.Tipo, c.Estado, c.Enviado, c.Sha256, c.Valido, c.ReferenciaCapId, c.CreadoEn))
            .FirstOrDefaultAsync(ct);
        var envios = await db.TrabajosDifusion.AsNoTracking().OfType<EnvioSms>().Where(e => e.VersionId == versionId).ToListAsync(ct);
        var tareas = await db.TrabajosDifusion.AsNoTracking().OfType<TareaManual>().Where(t => t.VersionId == versionId).ToListAsync(ct);

        var comunidadIds = envios.Select(e => e.ComunidadId).Concat(tareas.Select(t => t.ComunidadId)).OfType<Guid>().Distinct().ToList();
        var comunidades = await db.Comunidades.AsNoTracking().Where(c => comunidadIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => (c.Nombre, c.Municipio), ct);
        var usuarioIds = tareas.Select(t => t.ConfirmadoPorId).OfType<Guid>().Distinct().ToList();
        var usuarios = await db.Usuarios.AsNoTracking().Where(u => usuarioIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.NombreCompleto, ct);

        string? Nombre(Guid? id) => id is { } x && comunidades.TryGetValue(x, out var c) ? c.Nombre : null;

        var porComunidad = comunidadIds.Select(id => new SeguimientoComunidad(id, comunidades.GetValueOrDefault(id).Nombre ?? "?",
                comunidades.GetValueOrDefault(id).Municipio,
                ConteoEstados.De(envios.Where(e => e.ComunidadId == id).Select(e => e.Estado)),
                tareas.FirstOrDefault(t => t.ComunidadId == id && t.Canal == CanalDifusion.Altavoz)?.Estado))
            .OrderBy(c => c.Municipio).ThenBy(c => c.Comunidad).ToList();

        var resumen = Resumen(v.Id, v.AvisoId, v.Numero, v.Tipo.ToString(), v.Contenido.Titular ?? v.Contenido.Evento, v.EsPrueba,
            v.DifundidaEn.Value, envios.Select(e => e.Estado), tareas.Select(t => t.Estado));

        DateTimeOffset? smsTerminados = envios.Count > 0 && envios.All(e => e.Terminado) ? envios.Max(e => e.ResultadoEn) : null;
        bool? dentroDeMeta = smsTerminados is { } fin ? fin - v.DifundidaEn.Value <= MetaLote : null;

        var mostrarDestino = usuario.Tiene(Domain.Usuarios.Permiso.Edicion);
        return new SeguimientoDetalle(resumen, cap, v.PublicadaWebEn, porComunidad,
            tareas.OrderBy(t => t.Canal).ThenBy(t => Nombre(t.ComunidadId)).Select(t => new TareaVista(t.Id, t.Canal, t.ComunidadId,
                Nombre(t.ComunidadId), t.Estado, t.TextoLegible, t.Observacion,
                t.ConfirmadoPorId is { } u ? usuarios.GetValueOrDefault(u) : null, t.CreadoEn, t.ResultadoEn)).ToList(),
            envios.OrderBy(e => Nombre(e.ComunidadId)).Select(e => new EnvioSmsVista(e.Id, e.ComunidadId, Nombre(e.ComunidadId),
                mostrarDestino ? e.Destino : Enmascarar(e.Destino), e.Estado, e.Intentos, e.CodigoProveedor, e.UltimoError,
                e.ProximoIntento, e.CreadoEn, e.ResultadoEn)).ToList(),
            smsTerminados, dentroDeMeta, reloj.GetUtcNow());
    }

    public Task<TareaVista> ConfirmarTareaAsync(Guid tareaId, string? observacion, Guid usuarioId, CancellationToken ct) =>
        CerrarTareaAsync(tareaId, usuarioId, (t, u, ahora) => t.Confirmar(u, observacion, ahora), TipoEventoAviso.TareaConfirmada, ct);

    public Task<TareaVista> MarcarNoRealizadaAsync(Guid tareaId, string? motivo, Guid usuarioId, CancellationToken ct) =>
        CerrarTareaAsync(tareaId, usuarioId, (t, u, ahora) => t.MarcarNoRealizada(u, motivo, ahora), TipoEventoAviso.TareaNoRealizada, ct);

    private async Task<TareaVista> CerrarTareaAsync(Guid tareaId, Guid usuarioId, Action<TareaManual, Guid, DateTimeOffset> accion,
        TipoEventoAviso tipo, CancellationToken ct)
    {
        var tarea = await db.TrabajosDifusion.OfType<TareaManual>().FirstOrDefaultAsync(t => t.Id == tareaId, ct)
            ?? throw new KeyNotFoundException("No existe la tarea indicada.");
        accion(tarea, usuarioId, reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);

        var comunidad = tarea.ComunidadId is { } c ? await db.Comunidades.AsNoTracking().Where(x => x.Id == c).Select(x => x.Nombre).FirstOrDefaultAsync(ct) : null;
        if (eventos is not null)
            await eventos.PublicarAsync(new EventoAviso(tarea.AvisoId, tipo, usuarioId,
                $"{tarea.Canal}{(comunidad is null ? "" : $" en {comunidad}")}: {tarea.Resultado}{(tarea.Observacion is null ? "" : $" {tarea.Observacion}")}",
                reloj.GetUtcNow(), tarea.VersionId), ct);
        var nombre = await db.Usuarios.AsNoTracking().Where(u => u.Id == usuarioId).Select(u => u.NombreCompleto).FirstOrDefaultAsync(ct);
        return new TareaVista(tarea.Id, tarea.Canal, tarea.ComunidadId, comunidad, tarea.Estado, tarea.TextoLegible, tarea.Observacion,
            nombre, tarea.CreadoEn, tarea.ResultadoEn);
    }

    private static SeguimientoResumen Resumen(Guid versionId, Guid avisoId, int numero, string tipo, string titular, bool esPrueba,
        DateTimeOffset difundidaEn, IEnumerable<EstadoTrabajo> sms, IEnumerable<EstadoTrabajo> tareas)
    {
        var s = ConteoEstados.De(sms);
        var t = ConteoEstados.De(tareas);
        var total = s.Total + t.Total;
        var avance = total == 0 ? 100 : (int)Math.Floor(100.0 * (s.Terminados + t.Terminados) / total);
        return new SeguimientoResumen(versionId, avisoId, numero, tipo, titular, esPrueba, difundidaEn, s, t, avance,
            s.Pendientes == 0 && t.Pendientes == 0);
    }

    public static string Enmascarar(string telefono) =>
        telefono.Length <= 4 ? "****" : new string('*', telefono.Length - 4) + telefono[^4..];
}
