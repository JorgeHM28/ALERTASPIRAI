using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Mensajes;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Mensajes;

namespace SistemaAlertas.Api.Controllers;

/// <summary>
/// Lo que ve cualquier persona en la web pública. Solo datos del mensaje aprobado: nunca contactos, usuarios
/// ni el aviso oficial interno.
/// </summary>
/// <param name="Vigente">False si la versión fue reemplazada por un Update/Cancel o ya expiró (se sigue mostrando si se abre su enlace).</param>
public sealed record AvisoPublico(Guid Id, int Numero, TipoMensajeCap Tipo, NivelVista Nivel, string Evento, string? Titular,
    string? Descripcion, string? Instruccion, string? DescripcionArea, DateTimeOffset? Desde, DateTimeOffset? Hasta,
    DateTimeOffset? AprobadoEn, bool Vigente, Guid? ReemplazadoPor, JsonElement? Area, string TextoLectura);

/// <summary>Web pública (página de inicio sin sesión): avisos aprobados con texto, nivel, instrucciones y mapa (RF-06).</summary>
[ApiController]
[Route("api/publico")]
[AllowAnonymous]
public sealed class PublicoController(IAppDbContext db, IConversorGeoJson geoJson, TimeProvider reloj) : ControllerBase
{
    /// <summary>
    /// Avisos vigentes: la última versión aprobada de cada evento, que no fue cancelada ni expiró.
    /// Los avisos de prueba o simulacro no se publican. Con ?cancelados=true incluye las cancelaciones de las últimas 24 h.
    /// </summary>
    [HttpGet("avisos")]
    public async Task<IEnumerable<AvisoPublico>> Vigentes([FromQuery] bool cancelados = false, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        var desde = ahora.AddHours(-24);
        var versiones = await Publicables()
            .Where(v => v.ReemplazadaPorId == null)
            .Where(v => (v.Tipo != TipoMensajeCap.Cancel && v.Contenido.Expira > ahora)
                        || (cancelados && v.Tipo == TipoMensajeCap.Cancel && v.RevisadoEn >= desde))
            .ToListAsync(ct);
        return versiones
            .OrderBy(v => v.Tipo == TipoMensajeCap.Cancel)
            .ThenByDescending(v => v.Nivel.Orden)
            .ThenByDescending(v => v.RevisadoEn)
            .Select(v => Vista(v, ahora));
    }

    /// <summary>Un aviso aprobado por su enlace (el que se publica en redes y SMS). 404 si no está aprobado.</summary>
    [HttpGet("avisos/{id:guid}")]
    public async Task<ActionResult<AvisoPublico>> Obtener(Guid id, CancellationToken ct)
    {
        var v = await Publicables().FirstOrDefaultAsync(x => x.Id == id, ct);
        return v is null ? NotFound() : Vista(v, reloj.GetUtcNow());
    }

    private IQueryable<VersionMensaje> Publicables() => db.VersionesMensaje.AsNoTracking()
        .Where(v => (v.Estado == EstadoVersion.Aprobada || v.Estado == EstadoVersion.Difundida) && v.EstadoCap == EstadoCap.Actual);

    private AvisoPublico Vista(VersionMensaje v, DateTimeOffset ahora)
    {
        var c = v.Contenido;
        var vigente = v.ReemplazadaPorId is null && v.Tipo != TipoMensajeCap.Cancel && c.Expira > ahora;
        return new AvisoPublico(v.Id, v.Numero, v.Tipo, NivelVista.De(c.Nivel), c.Evento, c.Titular, c.Descripcion,
            c.Instruccion, c.DescripcionArea, c.Vigente, c.Expira, v.RevisadoEn, vigente, v.ReemplazadaPorId,
            v.Area is null ? null : geoJson.AGeoJson(v.Area), TextosMensaje.De(v).Completo);
    }
}
