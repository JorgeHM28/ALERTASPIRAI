using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Application.Difusion;

/// <summary>
/// Patrón Strategy. Cada canal manual redacta el mensaje a su manera: la radio lee el texto completo,
/// el altavoz necesita una frase corta y repetible, las redes llevan el enlace a la web pública.
/// El gestor de tareas elige la estrategia por canal en tiempo de ejecución y no tiene un switch.
/// Cambiar la redacción de un canal, o agregar uno (por ejemplo WhatsApp), es otra estrategia.
/// </summary>
public interface IRedaccionCanal
{
    CanalDifusion Canal { get; }
    string Redactar(VersionAprobada version);
}

public sealed class RedaccionRadio : IRedaccionCanal
{
    public CanalDifusion Canal => CanalDifusion.Radio;
    public string Redactar(VersionAprobada v) => $"Leer al aire en las radios comunitarias, dos veces: {v.TextoCompleto}";
}

public sealed class RedaccionAltavoz : IRedaccionCanal
{
    public CanalDifusion Canal => CanalDifusion.Altavoz;
    public string Redactar(VersionAprobada v) =>
        $"Anunciar por altavoz en las comunidades de las zonas afectadas y repetir cada 10 minutos: {v.TextoSms}";
}

public sealed class RedaccionRedesSociales : IRedaccionCanal
{
    public CanalDifusion Canal => CanalDifusion.RedesSociales;
    public string Redactar(VersionAprobada v) =>
        $"Publicar en las redes oficiales con el enlace al aviso (/?aviso={v.VersionId}): {v.TextoSms}";
}

public static class RedaccionesCanal
{
    public static IReadOnlyList<IRedaccionCanal> PorDefecto { get; } =
        [new RedaccionRadio(), new RedaccionAltavoz(), new RedaccionRedesSociales()];
}

/// <summary>
/// Contexto del patrón Strategy: elige la redacción por canal en tiempo de ejecución, sin un switch.
/// Un aviso de prueba lleva la marca [PRUEBA] en todos los canales.
/// </summary>
public sealed class RedactorTareas
{
    private readonly IReadOnlyDictionary<CanalDifusion, IRedaccionCanal> _redacciones;

    public RedactorTareas(IEnumerable<IRedaccionCanal>? redacciones = null)
    {
        var lista = redacciones?.ToList() is { Count: > 0 } l ? l : RedaccionesCanal.PorDefecto;
        _redacciones = lista.ToDictionary(r => r.Canal);
    }

    public string Redactar(CanalDifusion canal, VersionAprobada v) =>
        (v.EsPrueba ? "[PRUEBA] " : string.Empty) + (_redacciones.TryGetValue(canal, out var estrategia) ? estrategia.Redactar(v) : v.TextoSms);
}
