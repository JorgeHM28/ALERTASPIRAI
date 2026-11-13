using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Application.Difusion;

/// <summary>Lo que los comandos de un mismo envío comparten: la versión y los resultados que van dejando.</summary>
public sealed class ContextoDifusion(VersionAprobada version)
{
    private readonly List<string> _anotaciones = [];

    public VersionAprobada Version { get; } = version;
    public PublicacionWeb? Publicacion { get; set; }
    public int TrabajosSms { get; set; }
    public List<TareaManual> Tareas { get; } = [];

    /// <summary>Un comando que no falla pero no pudo hacer su trabajo deja aquí el motivo; se registra como incidencia.</summary>
    public void Anotar(string detalle) => _anotaciones.Add(detalle);

    internal IReadOnlyList<string> TomarAnotaciones()
    {
        var copia = _anotaciones.ToList();
        _anotaciones.Clear();
        return copia;
    }
}

/// <summary>
/// Patrón Command. Cada canal de RF-04 es un objeto con una sola operación, <see cref="EjecutarAsync"/>.
/// <c>ServicioDifusion</c> (el invocador) los ejecuta en orden sin saber qué hace cada uno, y aísla los fallos.
/// Agregar un canal es registrar un comando nuevo. El trabajo SMS (<see cref="TrabajoSms"/>) también es un comando:
/// se encola y lo ejecuta más tarde <c>ProcesadorColaSms</c>.
/// </summary>
public interface IComandoDifusion
{
    /// <summary>Nombre del canal para las incidencias.</summary>
    string Canal { get; }
    Task EjecutarAsync(ContextoDifusion contexto, CancellationToken ct = default);
}

public sealed class PublicarEnWeb(IPublicadorWeb publicador) : IComandoDifusion
{
    public string Canal => "Web";

    public async Task EjecutarAsync(ContextoDifusion c, CancellationToken ct = default) =>
        c.Publicacion = await publicador.PublicarAsync(c.Version, ct);
}

/// <summary>Crea un trabajo SMS por destinatario; el envío (con reintentos y bitácora) lo hace la cola.</summary>
public sealed class EncolarSms(IDestinatariosDifusion destinatarios, IColaSms cola) : IComandoDifusion
{
    public string Canal => "SMS";

    public async Task EjecutarAsync(ContextoDifusion c, CancellationToken ct = default)
    {
        var v = c.Version;
        if (v.ZonaIds.Count == 0)
        {
            c.Anotar("El aviso no cubre ninguna zona: no se crearon trabajos SMS.");
            return;
        }
        var lista = await destinatarios.ObtenerSmsAsync(v.ZonaIds, ct);
        if (lista.Count == 0)
        {
            c.Anotar("No hay contactos habilitados con SMS en las zonas del aviso.");
            return;
        }
        c.TrabajosSms = await cola.EncolarAsync(lista.Select(d => new TrabajoSms(Guid.NewGuid(), v.VersionId, d.ContactoId,
            new MensajeSms(d.Telefono, v.TextoSms, v.AvisoId, v.EsPrueba))).ToList(), ct);
    }
}

/// <summary>Radio, altavoz y redes sociales: crea una tarea por canal para que una persona la ejecute.</summary>
public sealed class CrearTareasManuales(IGestorTareasManuales gestor) : IComandoDifusion
{
    public static readonly IReadOnlyList<CanalManual> Canales = [CanalManual.Radio, CanalManual.Altavoz, CanalManual.RedesSociales];

    public string Canal => "Tareas manuales";

    public async Task EjecutarAsync(ContextoDifusion c, CancellationToken ct = default) =>
        c.Tareas.AddRange(await gestor.CrearAsync(c.Version, Canales, ct));
}
