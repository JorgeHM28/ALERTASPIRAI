using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Api.Controllers;

/// <summary>RF-04: difusión multicanal de una versión aprobada.</summary>
[ApiController]
[Route("api/difusion")]
[Authorize(Politicas.Lectura)]
public sealed class DifusionController(IServicioDifusion difusion, IGestorTareasManuales tareas, IBitacoraEnvios bitacora) : ControllerBase
{
    /// <summary>Facade: difunde por web, SMS y canales manuales en una sola llamada.</summary>
    [HttpPost("{versionAprobadaId:guid}")]
    [Authorize(Politicas.Aprobacion)]
    public Task<ResultadoDifusion> Difundir(Guid versionAprobadaId, CancellationToken ct) =>
        difusion.DifundirAsync(versionAprobadaId, ct);

    [HttpGet("tareas")]
    public IEnumerable<TareaManual> Tareas([FromQuery] Guid? versionId) => tareas.Listar(versionId);

    /// <summary>Bitácora de los envíos SMS (RNF-04), con los destinos enmascarados.</summary>
    [HttpGet("bitacora-sms")]
    public IEnumerable<RegistroEnvioSms> BitacoraSms([FromQuery] int cantidad = 100) => bitacora.Recientes(Math.Clamp(cantidad, 1, 1000));
}
