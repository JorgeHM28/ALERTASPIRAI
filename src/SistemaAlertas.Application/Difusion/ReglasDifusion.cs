using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Application.Difusion;

/// <summary>
/// Patrón Chain of Responsibility. Antes de difundir, el aviso pasa por una cadena de reglas.
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
    public void Verificar(Aviso aviso, DateTimeOffset ahora)
    {
        var motivo = Revisar(aviso, ahora);
        if (motivo is not null) throw new ExcepcionDominio(motivo);
        _siguiente?.Verificar(aviso, ahora);
    }

    /// <returns>El motivo del rechazo, o null si esta regla deja pasar el aviso.</returns>
    protected abstract string? Revisar(Aviso aviso, DateTimeOffset ahora);

    /// <summary>Orden: primero lo que hace inútil revisar el resto (descartado, sin aprobar), luego vigencia y contenido.</summary>
    public static ReglaDifusion CadenaPorDefecto()
    {
        var inicio = new ReglaNoDescartado();
        inicio.Luego(new ReglaAprobado())
              .Luego(new ReglaVigente())
              .Luego(new ReglaCompleto())
              .Luego(new ReglaAptoSiEsReal());
        return inicio;
    }
}

public sealed class ReglaNoDescartado : ReglaDifusion
{
    protected override string? Revisar(Aviso aviso, DateTimeOffset ahora) =>
        aviso.Estado == EstadoAviso.Descartado ? "El aviso fue descartado: no se difunde." : null;
}

/// <summary>RF-11: solo se difunde lo que un aprobador aprobó. Lo decide el objeto State del aviso.</summary>
public sealed class ReglaAprobado : ReglaDifusion
{
    protected override string? Revisar(Aviso aviso, DateTimeOffset ahora) =>
        aviso.EstadoActual.PermiteDifundir ? null
            : $"El aviso está {aviso.Estado}: un aprobador debe aprobarlo antes de difundirlo (RF-11).";
}

/// <summary>Pudo vencer entre la aprobación y la difusión: se mira la fecha ahora, no la última evaluación.</summary>
public sealed class ReglaVigente : ReglaDifusion
{
    protected override string? Revisar(Aviso aviso, DateTimeOffset ahora) =>
        aviso.Expira is { } e && e <= ahora ? $"El aviso venció el {e:yyyy-MM-dd HH:mm} UTC: no se difunde." : null;
}

public sealed class ReglaCompleto : ReglaDifusion
{
    protected override string? Revisar(Aviso aviso, DateTimeOffset ahora) =>
        aviso.Incompleto ? "El aviso está incompleto: " + string.Join(" ", aviso.MotivosNoApto) : null;
}

/// <summary>Un aviso real debe ser apto. Uno de prueba no es "apto" por definición, pero sí puede difundirse marcado.</summary>
public sealed class ReglaAptoSiEsReal : ReglaDifusion
{
    protected override string? Revisar(Aviso aviso, DateTimeOffset ahora) =>
        !aviso.EsPrueba && !aviso.AptoParaDifusion
            ? "El aviso no es apto para difusión: " + string.Join(" ", aviso.MotivosNoApto)
            : null;
}
