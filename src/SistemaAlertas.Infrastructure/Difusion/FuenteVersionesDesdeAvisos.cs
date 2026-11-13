using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Difusion;

namespace SistemaAlertas.Infrastructure.Difusion;

/// <summary>
/// Mientras no existan las versiones del mensaje (RF-11 con Prototype), la "versión aprobada" es el propio aviso
/// aprobado: el identificador de versión es el del aviso. Antes de entregarlo, el aviso recorre la cadena de reglas
/// de difusión (Chain of Responsibility): no descartado, aprobado, vigente, completo y apto (o de prueba).
/// Un aviso de prueba o simulacro (CAP status distinto de Actual) viaja marcado como prueba.
/// </summary>
public sealed class FuenteVersionesDesdeAvisos(IAppDbContext db, ReglaDifusion reglas, TimeProvider reloj) : IFuenteVersionesAprobadas
{
    public const int LargoMaximoSms = 320;

    public async Task<VersionAprobada?> ObtenerAsync(Guid versionAprobadaId, CancellationToken ct = default)
    {
        var aviso = await db.Avisos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == versionAprobadaId, ct);
        if (aviso is null) return null;

        reglas.Verificar(aviso, reloj.GetUtcNow());
        var esPrueba = aviso.EsPrueba;

        var zonas = aviso.Area is null ? []
            : await db.Zonas.AsNoTracking().Where(z => z.Area.Intersects(aviso.Area)).Select(z => z.Id).ToListAsync(ct);
        var titular = aviso.Titular ?? aviso.Evento;
        var sms = $"{titular}. {aviso.Instruccion}".Trim();
        return new VersionAprobada(aviso.Id, aviso.Id, titular,
            string.Join("\n\n", new[] { aviso.Titular, aviso.Descripcion, aviso.Instruccion }.Where(t => !string.IsNullOrWhiteSpace(t))),
            sms.Length <= LargoMaximoSms ? sms : sms[..(LargoMaximoSms - 1)] + "…", zonas, esPrueba);
    }
}
