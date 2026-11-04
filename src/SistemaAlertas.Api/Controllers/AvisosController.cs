using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Avisos;
using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Domain.Avisos;

namespace SistemaAlertas.Api.Controllers;

public sealed record AvisoDetalleDto(AvisoResumen Resumen, string? Descripcion, string? Instruccion, DateTimeOffset? Vigente,
    JsonElement? Area, string XmlCap);

/// <summary>RF-01: registro de avisos e importación de mensajes CAP 1.2.</summary>
[ApiController]
[Route("api/avisos")]
[Authorize(Politicas.Lectura)]
public sealed class AvisosController(IAppDbContext db, ServicioAvisos servicio, IFabricaImportadores fabrica) : ControllerBase
{
    private const int TamanoMaximo = 1_000_000;

    [HttpGet]
    public async Task<IEnumerable<AvisoResumen>> Listar([FromQuery] EstadoAviso? estado, [FromQuery] int limite = 100, CancellationToken ct = default)
    {
        var q = db.Avisos.AsNoTracking();
        if (estado is { } e) q = q.Where(a => a.Estado == e);
        var avisos = await q.OrderByDescending(a => a.Enviado).Take(Math.Clamp(limite, 1, 500)).ToListAsync(ct);
        return avisos.Select(AvisoMapeo.ResumenDe);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AvisoDetalleDto>> Obtener(Guid id, CancellationToken ct)
    {
        var a = await db.Avisos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound();
        return new AvisoDetalleDto(AvisoMapeo.ResumenDe(a), a.Descripcion, a.Instruccion, a.Vigente,
            a.Area is null ? null : GeoJson.Escribir(a.Area), a.XmlCap);
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
    public IEnumerable<FormatoAviso> Formatos() => fabrica.FormatosSoportados;

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

    [HttpPost("{id:guid}/descartar")]
    [Authorize(Politicas.Edicion)]
    public async Task<AvisoResumen> Descartar(Guid id, CancellationToken ct)
    {
        var a = await db.Avisos.FindAsync([id], ct) ?? throw new KeyNotFoundException("Aviso no encontrado.");
        a.Descartar();
        await db.SaveChangesAsync(ct);
        return AvisoMapeo.ResumenDe(a);
    }
}
