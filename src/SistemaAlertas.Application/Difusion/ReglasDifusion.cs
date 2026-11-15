using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Mensajes;

namespace SistemaAlertas.Application.Difusion;

/// <summary>
/// Patrón Chain of Responsibility. Antes de difundir, la versión aprobada (y su aviso) pasa por una cadena de reglas.
/// Cada eslabón revisa una sola cosa: si encuentra un problema corta la cadena con su motivo; si no, pasa al siguiente.
/// Agregar una regla (por ejemplo, "el aprobador no es quien registró", o un horario de silencio) es una clase nueva
/// enganchada en <see cref="CadenaPorDefecto"/>, sin tocar las demás ni a quien difunde.
/// </summary>
public abstract class ReglaDifusion
{
    private ReglaDifusion? _siguiente;

    /// <summary>Engancha la regla siguiente y la devuelve, para encadenar: a.Luego(b).Luego(c).</summary>
    public ReglaDifusion Luego(ReglaDifusion siguiente)
    {
        _siguiente = siguiente;
        return siguiente;
    }

    /// <summary>Recorre la cadena. Lanza <see cref="ExcepcionDominio"/> con el motivo del primer eslabón que rechaza.</summary>
    public void Verificar(VersionMensaje version, Aviso aviso, DateTimeOffset ahora)
    {
        var motivo = Revisar(version, aviso, ahora);
        if (motivo is not null) throw new ExcepcionDominio(motivo);
        _siguiente?.Verificar(version, aviso, ahora);
    }

    /// <returns>El motivo del rechazo, o null si esta regla deja pasar la versión.</returns>
    protected abstract string? Revisar(VersionMensaje version, Aviso aviso, DateTimeOffset ahora);

    /// <summary>Orden: primero lo que hace inútil revisar el resto (descartado, sin aprobar, reemplazada), luego vigencia y contenido.</summary>
    public static ReglaDifusion CadenaPorDefecto()
    {
        var inicio = new ReglaNoDescartado();
        inicio.Luego(new ReglaAprobado())
              .Luego(new ReglaNoReemplazada())
              .Luego(new ReglaVigente())
              .Luego(new ReglaCompleto());
        return inicio;
    }
}

public sealed class ReglaNoDescartado : ReglaDifusion
{
    protected override string? Revisar(VersionMensaje version, Aviso aviso, DateTimeOffset ahora) =>
        aviso.Estado == EstadoAviso.Descartado ? "El aviso fue descartado: no se difunde." : null;
}

/// <summary>RF-11: solo se difunde una versión que un aprobador aprobó. Lo decide el objeto State de la versión.</summary>
public sealed class ReglaAprobado : ReglaDifusion
{
    protected override string? Revisar(VersionMensaje version, Aviso aviso, DateTimeOffset ahora) =>
        version.EstadoActual.PermiteDifundir ? null
            : $"La versión está {version.Estado}: un aprobador debe aprobarla antes de difundirla (RF-11).";
}

/// <summary>Si un Update o Cancel aprobado la reemplazó, se difunde ese, no esta.</summary>
public sealed class ReglaNoReemplazada : ReglaDifusion
{
    protected override string? Revisar(VersionMensaje version, Aviso aviso, DateTimeOffset ahora) =>
        version.ReemplazadaPorId is null ? null
            : "La versión fue reemplazada por un Update o Cancel aprobado: se difunde la versión vigente.";
}

/// <summary>Pudo vencer entre la aprobación y la difusión: se mira la fecha ahora. Un Cancel se difunde aunque haya vencido.</summary>
public sealed class ReglaVigente : ReglaDifusion
{
    protected override string? Revisar(VersionMensaje version, Aviso aviso, DateTimeOffset ahora) =>
        version.Tipo != TipoMensajeCap.Cancel && version.Contenido.Expira is { } e && e <= ahora
            ? $"La versión venció el {e:yyyy-MM-dd HH:mm} UTC: no se difunde." : null;
}

public sealed class ReglaCompleto : ReglaDifusion
{
    protected override string? Revisar(VersionMensaje version, Aviso aviso, DateTimeOffset ahora) =>
        version.Faltantes(ahora) is { Count: > 0 } faltan ? "La versión está incompleta: " + string.Join(" ", faltan) : null;
}
