using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Api.Controllers;

public sealed record SolicitudCierreTarea(string? Observacion);

/// <summary>RF-04: difusión multicanal de una versión aprobada. RF-08: seguimiento de lo enviado.</summary>
[ApiController]
[Route("api/difusion")]
[Authorize(Politicas.Lectura)]
public sealed class DifusionController(IServicioDifusion difusion, ServicioSeguimiento seguimiento, IAppDbContext db,
    IBitacoraEnvios bitacora) : ControllerBase
{
    /// <summary>
    /// Facade: emite el CAP final (validado contra el XSD y archivado), publica en la web, encola los SMS y crea las tareas
    /// manuales, en una sola llamada. Una versión se difunde una sola vez.
    /// </summary>
    [HttpPost("{versionAprobadaId:guid}")]
    [Authorize(Politicas.Aprobacion)]
    public Task<ResultadoDifusion> Difundir(Guid versionAprobadaId, CancellationToken ct) =>
        difusion.DifundirAsync(versionAprobadaId, ct);

    /// <summary>RF-08: versiones difundidas con su avance (SMS y tareas).</summary>
    [HttpGet("seguimiento")]
    public Task<IReadOnlyList<SeguimientoResumen>> Seguimiento(CancellationToken ct) => seguimiento.ListarAsync(ct);

    /// <summary>RF-08: detalle por comunidad y canal. Los teléfonos se ven enmascarados sin permiso de edición.</summary>
    [HttpGet("seguimiento/{versionId:guid}")]
    public async Task<ActionResult<SeguimientoDetalle>> DetalleSeguimiento(Guid versionId, CancellationToken ct) =>
        await seguimiento.DetalleAsync(versionId, ct) is { } d ? d : NotFound();

    /// <summary>XML CAP 1.2 exacto que se emitió para la versión (RF-04).</summary>
    [HttpGet("{versionId:guid}/cap")]
    public async Task<IActionResult> Cap(Guid versionId, CancellationToken ct)
    {
        var cap = await db.CapsEmitidos.AsNoTracking().FirstOrDefaultAsync(c => c.VersionId == versionId, ct);
        return cap is null ? NotFound() : File(Encoding.UTF8.GetBytes(cap.Xml), "application/xml", $"{cap.Identificador}.xml");
    }

    [HttpPost("tareas/{tareaId:guid}/confirmar")]
    [Authorize(Politicas.Edicion)]
    public Task<TareaVista> ConfirmarTarea(Guid tareaId, SolicitudCierreTarea solicitud, CancellationToken ct) =>
        seguimiento.ConfirmarTareaAsync(tareaId, solicitud.Observacion, User.Id(), ct);

    [HttpPost("tareas/{tareaId:guid}/no-realizada")]
    [Authorize(Politicas.Edicion)]
    public Task<TareaVista> TareaNoRealizada(Guid tareaId, SolicitudCierreTarea solicitud, CancellationToken ct) =>
        seguimiento.MarcarNoRealizadaAsync(tareaId, solicitud.Observacion, User.Id(), ct);

    /// <summary>Bitácora de los intentos de envío SMS (RNF-04), con los destinos enmascarados.</summary>
    [HttpGet("bitacora-sms")]
    public IEnumerable<RegistroEnvioSms> BitacoraSms([FromQuery] int cantidad = 100) => bitacora.Recientes(Math.Clamp(cantidad, 1, 1000));
}
