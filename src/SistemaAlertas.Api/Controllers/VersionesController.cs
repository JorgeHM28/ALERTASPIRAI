using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Mensajes;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Mensajes;
using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Api.Controllers;

/// <param name="Area">Área de la versión en GeoJSON, para el mapa (RF-06).</param>
/// <param name="TextoSms">Cómo se leerá en SMS y altavoz: el nivel va en palabras.</param>
/// <param name="TextoLectura">Texto completo para la radio y la web.</param>
public sealed record VersionDetalleDto(VersionDetalle Detalle, JsonElement? Area, string TextoSms, string TextoLectura);

/// <summary>
/// RF-02: versiones del mensaje (Alert, Update, Cancel) preparadas desde un aviso oficial.
/// RF-11: revisión, aprobación o rechazo con motivo. RF-07: destinos por comunidad.
/// </summary>
[ApiController]
[Route("api")]
[Authorize(Politicas.Lectura)]
public sealed class VersionesController(IAppDbContext db, ServicioVersiones servicio, ServicioDestinos destinos,
    IConversorGeoJson geoJson, TimeProvider reloj) : ControllerBase
{
    [HttpGet("versiones")]
    public async Task<IEnumerable<VersionResumen>> Listar([FromQuery] EstadoVersion? estado, [FromQuery] Guid? avisoId,
        [FromQuery] Guid? cadenaId, [FromQuery] int limite = 200, CancellationToken ct = default)
    {
        var q = db.VersionesMensaje.AsNoTracking();
        if (estado is { } e) q = q.Where(v => v.Estado == e);
        if (avisoId is { } a) q = q.Where(v => v.AvisoId == a || v.CadenaId == a);
        if (cadenaId is { } c) q = q.Where(v => v.CadenaId == c);
        var ahora = reloj.GetUtcNow();
        var lista = await q.OrderByDescending(v => v.CreadoEn).Take(Math.Clamp(limite, 1, 500)).ToListAsync(ct);
        return lista.Select(v => VersionMapeo.ResumenDe(v, ahora));
    }

    [HttpGet("versiones/{id:guid}")]
    public async Task<ActionResult<VersionDetalleDto>> Obtener(Guid id, CancellationToken ct)
    {
        var v = await db.VersionesMensaje.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return NotFound();
        return await DetalleAsync(v, ct);
    }

    /// <summary>El XML CAP 1.2 generado (existe desde que la versión pasó a revisión).</summary>
    [HttpGet("versiones/{id:guid}/cap")]
    [Produces("application/xml")]
    public async Task<IActionResult> Cap(Guid id, CancellationToken ct)
    {
        var v = await db.VersionesMensaje.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (v is null) return NotFound();
        if (v.XmlCap is null)
            return Problem(statusCode: 404, title: "Sin XML", detail: "El XML CAP se genera y valida al enviar la versión a revisión.");
        return Content(v.XmlCap, "application/xml; charset=utf-8");
    }

    /// <summary>Prepara la versión de un aviso: Alert, Update (si ya hay una aprobada o el aviso es un Update oficial) o Cancel.</summary>
    [HttpPost("avisos/{avisoId:guid}/versiones")]
    [Authorize(Politicas.Edicion)]
    public async Task<ActionResult<VersionDetalleDto>> Preparar(Guid avisoId, RedaccionVersion? redaccion, CancellationToken ct)
    {
        var v = await servicio.PrepararDesdeAvisoAsync(avisoId, redaccion ?? new(null, null, null), User.Id(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = v.Id }, await DetalleAsync(v, ct));
    }

    /// <summary>Corrige un borrador (solo la redacción: titular, descripción e instrucciones).</summary>
    [HttpPut("versiones/{id:guid}")]
    [Authorize(Politicas.Edicion)]
    public async Task<VersionDetalleDto> Redactar(Guid id, RedaccionVersion redaccion, CancellationToken ct) =>
        await DetalleAsync(await servicio.RedactarAsync(id, redaccion, User.Id(), ct), ct);

    /// <summary>Genera y valida el XML CAP 1.2. Si está incompleta o el XML no es válido, sigue en borrador (400).</summary>
    [HttpPost("versiones/{id:guid}/enviar-revision")]
    [Authorize(Politicas.Edicion)]
    public async Task<VersionDetalleDto> EnviarARevision(Guid id, CancellationToken ct) =>
        await DetalleAsync(await servicio.EnviarARevisionAsync(id, User.Id(), ct), ct);

    [HttpPost("versiones/{id:guid}/aprobar")]
    [Authorize(Politicas.Aprobacion)]
    public async Task<VersionDetalleDto> Aprobar(Guid id, CancellationToken ct) =>
        await DetalleAsync(await servicio.AprobarAsync(id, User.Id(), ct), ct);

    /// <summary>Rechaza con motivo obligatorio. La versión vuelve al operador, que la corrige con /corregir.</summary>
    [HttpPost("versiones/{id:guid}/rechazar")]
    [Authorize(Politicas.Aprobacion)]
    public async Task<VersionDetalleDto> Rechazar(Guid id, RechazoVersion rechazo, CancellationToken ct) =>
        await DetalleAsync(await servicio.RechazarAsync(id, rechazo.Motivo, User.Id(), ct), ct);

    /// <summary>Crea la versión siguiente (borrador) a partir de una rechazada, con el motivo a la vista.</summary>
    [HttpPost("versiones/{id:guid}/corregir")]
    [Authorize(Politicas.Edicion)]
    public async Task<ActionResult<VersionDetalleDto>> Corregir(Guid id, CancellationToken ct)
    {
        var v = await servicio.CorregirAsync(id, User.Id(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = v.Id }, await DetalleAsync(v, ct));
    }

    /// <summary>Elimina un borrador. Solo borradores, y solo quien lo preparó o un administrador.</summary>
    [HttpDelete("versiones/{id:guid}")]
    [Authorize(Politicas.Edicion)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EliminarBorrador(Guid id, CancellationToken ct)
    {
        await servicio.EliminarBorradorAsync(id, User.Id(), User.IsInRole(nameof(Rol.Administrador)), ct);
        return NoContent();
    }

    /// <summary>Corrige una versión aprobada: prepara un Update que la referencia.</summary>
    [HttpPost("versiones/{id:guid}/update")]
    [Authorize(Politicas.Edicion)]
    public Task<ActionResult<VersionDetalleDto>> Update(Guid id, RedaccionVersion? redaccion, CancellationToken ct) =>
        Derivar(id, TipoMensajeCap.Update, redaccion, ct);

    /// <summary>Cancela una versión aprobada: prepara un Cancel que la referencia.</summary>
    [HttpPost("versiones/{id:guid}/cancel")]
    [Authorize(Politicas.Edicion)]
    public Task<ActionResult<VersionDetalleDto>> Cancel(Guid id, RedaccionVersion? redaccion, CancellationToken ct) =>
        Derivar(id, TipoMensajeCap.Cancel, redaccion, ct);

    /// <summary>Canales de contacto aprobados para difundir (configuración Destinos:CanalesAprobados).</summary>
    [HttpGet("destinos/canales")]
    public IReadOnlyList<TipoCanal> CanalesAprobados() => destinos.CanalesAprobados;

    /// <summary>RF-07: calcula los destinos sin guardarlos (alcance para revisar). ?canales=Sms&amp;canales=Radio</summary>
    [HttpGet("versiones/{id:guid}/destinos/previa")]
    public Task<ListaDestinosVista> PreviaDestinos(Guid id, [FromQuery] List<TipoCanal>? canales, CancellationToken ct) =>
        destinos.PreviaAsync(id, canales, ct);

    /// <summary>RF-07: guarda la lista de destinos de una versión aprobada (reemplaza la anterior).</summary>
    [HttpPost("versiones/{id:guid}/destinos")]
    [Authorize(Politicas.Edicion)]
    public Task<ListaDestinosVista> GenerarDestinos(Guid id, SeleccionDestinosDto? seleccion, CancellationToken ct) =>
        destinos.GenerarAsync(id, seleccion?.Canales, User.Id(), ct);

    [HttpGet("versiones/{id:guid}/destinos")]
    public async Task<ActionResult<ListaDestinosVista>> Destinos(Guid id, CancellationToken ct) =>
        await destinos.ObtenerAsync(id, ct) is { } lista ? lista
            : Problem(statusCode: 404, title: "Sin lista de destinos", detail: "Todavía no se generó la lista de destinos de esta versión.");

    private async Task<ActionResult<VersionDetalleDto>> Derivar(Guid id, TipoMensajeCap tipo, RedaccionVersion? redaccion, CancellationToken ct)
    {
        var v = await servicio.DerivarAsync(id, tipo, redaccion ?? new(null, null, null), User.Id(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = v.Id }, await DetalleAsync(v, ct));
    }

    private async Task<VersionDetalleDto> DetalleAsync(VersionMensaje v, CancellationToken ct)
    {
        var aviso = await db.Avisos.AsNoTracking().FirstAsync(a => a.Id == v.AvisoId, ct);
        var c = v.Contenido;
        var detalle = new VersionDetalle(VersionMapeo.ResumenDe(v, reloj.GetUtcNow()), c.Urgencia, c.Severidad, c.Certeza,
            c.Descripcion, c.Instruccion, c.Vigente, v.IdentificadorCap, v.Remitente, v.EnviadoCap, v.Referencias,
            v.ReferenciaVersionId, v.VersionAnteriorId, v.MotivoDevolucion, v.EnviadaARevisionEn, v.RevisadoPorId,
            v.DifundidaEn, v.XmlCap, aviso.Remitente, aviso.IdentificadorCap, aviso.Enviado, aviso.TipoMensaje);
        var (sms, lectura) = TextosMensaje.De(v);
        return new VersionDetalleDto(detalle, v.Area is null ? null : geoJson.AGeoJson(v.Area), sms, lectura);
    }
}
