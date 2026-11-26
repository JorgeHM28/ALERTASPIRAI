using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Application.Avisos;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Domain.Avisos;

namespace SistemaAlertas.Api.Controllers;

public sealed record AvisoDetalleDto(AvisoResumen Resumen, string? Descripcion, string? Instruccion, DateTimeOffset? Vigente,
    JsonElement? Area, string XmlCap, string? ContenidoOriginal, string? TipoContenidoOriginal, string? HashOriginal);

/// <summary>RF-01: registro de avisos e importación de mensajes CAP 1.2. Lo que se aprueba (RF-11) son sus versiones: ver VersionesController.</summary>
[ApiController]
[Route("api/avisos")]
[Authorize(Politicas.Lectura)]
public sealed class AvisosController(IAppDbContext db, ServicioAvisos servicio, IModuloCap moduloCap,
    IConversorGeoJson geoJson, IBitacoraAvisos bitacora) : ControllerBase
{
    private const int TamanoMaximo = 1_000_000;

    [HttpGet]
    public async Task<IEnumerable<AvisoResumen>> Listar([FromQuery] EstadoAviso? estado, [FromQuery] bool? apto,
        [FromQuery] bool? vencido, [FromQuery] bool? incompleto, [FromQuery] int limite = 100, CancellationToken ct = default)
    {
        await servicio.RevisarVigenciaAsync(ct);
        var q = db.Avisos.AsNoTracking();
        if (estado is { } e) q = q.Where(a => a.Estado == e);
        if (apto is { } ap) q = q.Where(a => a.AptoParaDifusion == ap);
        if (vencido is { } v) q = q.Where(a => a.Vencido == v);
        if (incompleto is { } i) q = q.Where(a => a.Incompleto == i);
        var avisos = await q.OrderByDescending(a => a.Enviado).Take(Math.Clamp(limite, 1, 500)).ToListAsync(ct);
        return avisos.Select(AvisoMapeo.ResumenDe);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AvisoDetalleDto>> Obtener(Guid id, CancellationToken ct)
    {
        await servicio.RevisarVigenciaAsync(ct);
        var a = await db.Avisos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound();
        return new AvisoDetalleDto(AvisoMapeo.ResumenDe(a), a.Descripcion, a.Instruccion, a.Vigente,
            a.Area is null ? null : geoJson.AGeoJson(a.Area), a.XmlCap, a.ContenidoOriginal, a.TipoContenidoOriginal, a.HashOriginal);
    }

    /// <summary>
    /// El documento importado exactamente como llegó (XML oficial o JSON), con su SHA-256 en la cabecera X-Contenido-Sha256.
    /// 404 si el aviso se registró a mano.
    /// </summary>
    [HttpGet("{id:guid}/original")]
    public async Task<IActionResult> Original(Guid id, CancellationToken ct)
    {
        var a = await db.Avisos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Aviso no encontrado.");
        if (a.ContenidoOriginal is null)
            return Problem(statusCode: 404, title: "Sin documento original", detail: "El aviso se registró a mano: no hay documento importado.");
        Response.Headers["X-Contenido-Sha256"] = a.HashOriginal;
        var extension = a.TipoContenidoOriginal == "application/json" ? "json" : "xml";
        Response.Headers.ContentDisposition = $"inline; filename=\"{a.IdentificadorCap}.original.{extension}\"";
        return Content(a.ContenidoOriginal, $"{a.TipoContenidoOriginal}; charset=utf-8");
    }

    /// <summary>El mensaje CAP 1.2 del aviso en XML.</summary>
    [HttpGet("{id:guid}/cap")]
    [Produces("application/xml")]
    public async Task<IActionResult> Cap(Guid id, CancellationToken ct)
    {
        var a = await db.Avisos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? NotFound() : Content(a.XmlCap, "application/xml; charset=utf-8");
    }

    [HttpGet("formatos")]
    public IEnumerable<FormatoAviso> Formatos() => moduloCap.FormatosSoportados;

    /// <summary>Registro manual: el sistema genera el mensaje CAP con el Builder.</summary>
    [HttpPost]
    [Authorize(Politicas.Edicion)]
    public async Task<ActionResult<AvisoResumen>> Registrar(RegistrarAvisoManual dto, CancellationToken ct)
    {
        var aviso = await servicio.RegistrarManualAsync(dto, User.Id(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = aviso.Id }, AvisoMapeo.ResumenDe(aviso));
    }

    /// <summary>
    /// Importa un mensaje CAP enviado en el cuerpo (XML o JSON). Si no se indica ?formato=, se detecta por el contenido.
    /// </summary>
    [HttpPost("importar")]
    [Authorize(Politicas.Edicion)]
    [Consumes("application/xml", "text/xml", "application/json", "text/plain")]
    public async Task<ActionResult<AvisoResumen>> Importar([FromQuery] FormatoAviso? formato, CancellationToken ct)
    {
        using var lector = new StreamReader(Request.Body);
        var buffer = new char[TamanoMaximo + 1];
        var leidos = await lector.ReadBlockAsync(buffer, ct);
        if (leidos > TamanoMaximo) return Problem(statusCode: 413, title: "Mensaje demasiado grande", detail: "El límite es 1 MB.");
        var contenido = new string(buffer, 0, leidos);

        var aviso = await servicio.ImportarAsync(contenido, formato, User.Id(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = aviso.Id }, AvisoMapeo.ResumenDe(aviso));
    }

    /// <summary>Valida un mensaje CAP 1.2 en XML sin registrarlo. Devuelve la lista de errores (vacía si es válido).</summary>
    [HttpPost("validar-cap")]
    [Consumes("application/xml", "text/xml", "text/plain")]
    public async Task<ActionResult<ResultadoValidacionCap>> ValidarCap(CancellationToken ct)
    {
        using var lector = new StreamReader(Request.Body);
        var buffer = new char[TamanoMaximo + 1];
        var leidos = await lector.ReadBlockAsync(buffer, ct);
        if (leidos > TamanoMaximo) return Problem(statusCode: 413, title: "Mensaje demasiado grande", detail: "El límite es 1 MB.");
        return moduloCap.Validar(new string(buffer, 0, leidos));
    }

    [HttpPost("{id:guid}/descartar")]
    [Authorize(Politicas.Edicion)]
    public async Task<AvisoResumen> Descartar(Guid id, CancellationToken ct) =>
        AvisoMapeo.ResumenDe(await servicio.DescartarAsync(id, User.Id(), ct));

    /// <summary>Bitácora del aviso: registrado, aprobado, descartado, vencido, difundido (Observer). Se reinicia con la API.</summary>
    [HttpGet("{id:guid}/eventos")]
    public Task<IReadOnlyList<EventoAviso>> Eventos(Guid id, CancellationToken ct) => bitacora.DeAsync(id, ct);
}
