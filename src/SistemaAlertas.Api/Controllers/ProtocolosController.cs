using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Mensajes;

namespace SistemaAlertas.Api.Controllers;

public sealed record GuardarProtocolo(string Titulo, string Instruccion, string Fuente, string Referencia, DateOnly FechaDocumento);
public sealed record ProtocoloDto(Guid Id, string Titulo, string Instruccion, string Fuente, string Referencia,
    DateOnly FechaDocumento, string Cita, bool Activo, int Versiones);

/// <summary>
/// RF-05: catálogo de protocolos oficiales con su fuente y fecha. Lo consulta quien prepara mensajes; lo mantiene un
/// administrador. Un protocolo no se borra (las versiones lo citan): se desactiva.
/// </summary>
[ApiController]
[ProducesResponseType(401)]
[ProducesResponseType(403)]
[Route("api/protocolos")]
[Authorize(Politicas.Lectura)]
public sealed class ProtocolosController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ProtocoloDto>> Listar([FromQuery] bool? activos, CancellationToken ct)
    {
        var q = db.Protocolos.AsNoTracking();
        if (activos is { } a) q = q.Where(p => p.Activo == a);
        var lista = await q.OrderBy(p => p.Titulo).ToListAsync(ct);
        var usos = await db.VersionesMensaje.Where(v => v.ProtocoloId != null).GroupBy(v => v.ProtocoloId!.Value)
            .Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        return lista.Select(p => ADto(p, usos.GetValueOrDefault(p.Id)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProtocoloDto>> Obtener(Guid id, CancellationToken ct)
    {
        var p = await db.Protocolos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? NotFound() : ADto(p, await db.VersionesMensaje.CountAsync(v => v.ProtocoloId == id, ct));
    }

    [HttpPost]
    [Authorize(Politicas.Administracion)]
    [ProducesResponseType(typeof(ProtocoloDto), 201)]
    public async Task<ActionResult<ProtocoloDto>> Crear(GuardarProtocolo dto, CancellationToken ct)
    {
        var p = new ProtocoloOficial(dto.Titulo, dto.Instruccion, dto.Fuente, dto.Referencia, dto.FechaDocumento);
        db.Protocolos.Add(p);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Obtener), new { id = p.Id }, ADto(p, 0));
    }

    /// <summary>Corrige el protocolo. Las versiones que ya lo citan conservan el texto y la cita que copiaron.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Politicas.Administracion)]
    public async Task<ActionResult<ProtocoloDto>> Actualizar(Guid id, GuardarProtocolo dto, CancellationToken ct)
    {
        var p = await db.Protocolos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();
        p.Actualizar(dto.Titulo, dto.Instruccion, dto.Fuente, dto.Referencia, dto.FechaDocumento);
        await db.SaveChangesAsync(ct);
        return ADto(p, await db.VersionesMensaje.CountAsync(v => v.ProtocoloId == id, ct));
    }

    [HttpPost("{id:guid}/desactivar")]
    [Authorize(Politicas.Administracion)]
    public Task<ActionResult<ProtocoloDto>> Desactivar(Guid id, CancellationToken ct) => CambiarEstado(id, false, ct);

    [HttpPost("{id:guid}/activar")]
    [Authorize(Politicas.Administracion)]
    public Task<ActionResult<ProtocoloDto>> Activar(Guid id, CancellationToken ct) => CambiarEstado(id, true, ct);

    private async Task<ActionResult<ProtocoloDto>> CambiarEstado(Guid id, bool activo, CancellationToken ct)
    {
        var p = await db.Protocolos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();
        if (activo) p.Activar(); else p.Desactivar();
        await db.SaveChangesAsync(ct);
        return ADto(p, await db.VersionesMensaje.CountAsync(v => v.ProtocoloId == id, ct));
    }

    private static ProtocoloDto ADto(ProtocoloOficial p, int versiones) =>
        new(p.Id, p.Titulo, p.Instruccion, p.Fuente, p.Referencia, p.FechaDocumento, p.Cita, p.Activo, versiones);
}
