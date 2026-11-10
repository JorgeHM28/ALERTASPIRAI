using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Infrastructure.Difusion;

/// <summary>
/// Mientras no exista el flujo de aprobación con versiones (RF-11), la "versión aprobada" es el propio aviso:
/// el identificador de versión es el del aviso. Solo se difunde un aviso apto; uno de prueba o simulacro
/// (CAP status distinto de Actual) se acepta si está completo, y viaja marcado como prueba.
/// </summary>
public sealed class FuenteVersionesDesdeAvisos(IAppDbContext db) : IFuenteVersionesAprobadas
{
    public const int LargoMaximoSms = 320;

    public async Task<VersionAprobada?> ObtenerAsync(Guid versionAprobadaId, CancellationToken ct = default)
    {
        var aviso = await db.Avisos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == versionAprobadaId, ct);
        if (aviso is null) return null;

        var esPrueba = aviso.EstadoCap != EstadoCap.Actual;
        if (aviso.Estado == EstadoAviso.Descartado) throw new ExcepcionDominio("El aviso fue descartado: no se difunde.");
        if (!esPrueba && !aviso.AptoParaDifusion)
            throw new ExcepcionDominio("El aviso no es apto para difusión: " + string.Join(" ", aviso.MotivosNoApto));
        if (esPrueba && (aviso.Incompleto || aviso.Vencido))
            throw new ExcepcionDominio("El aviso de prueba está incompleto o vencido: " + string.Join(" ", aviso.MotivosNoApto));

        var zonas = aviso.Area is null ? []
            : await db.Zonas.AsNoTracking().Where(z => z.Area.Intersects(aviso.Area)).Select(z => z.Id).ToListAsync(ct);
        var titular = aviso.Titular ?? aviso.Evento;
        var sms = $"{titular}. {aviso.Instruccion}".Trim();
        return new VersionAprobada(aviso.Id, aviso.Id, titular,
            string.Join("\n\n", new[] { aviso.Titular, aviso.Descripcion, aviso.Instruccion }.Where(t => !string.IsNullOrWhiteSpace(t))),
            sms.Length <= LargoMaximoSms ? sms : sms[..(LargoMaximoSms - 1)] + "…", zonas, esPrueba);
    }
}
