using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Application.Difusion;

/// <summary>Lo que los comandos de un mismo envío comparten: la versión y los resultados que van dejando.</summary>
public sealed class ContextoDifusion(VersionAprobada version, TimeProvider? reloj = null)
{
    public TimeProvider Reloj { get; } = reloj ?? TimeProvider.System;
    private readonly List<string> _anotaciones = [];

    public VersionAprobada Version { get; } = version;
    public PublicacionWeb? Publicacion { get; set; }
    public int TrabajosSms { get; set; }
    public int EnviosWhatsApp { get; set; }
    public int EnviosEmail { get; set; }
    /// <summary>Trabajos pendientes de versiones anteriores que se cancelaron (RF-04).</summary>
    public int TrabajosDetenidos { get; set; }
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

/// <summary>
/// RF-04: primer comando. Un Update o Cancel detiene los SMS y las tareas que seguían pendientes de las versiones
/// anteriores del mismo evento, para que no salga el mensaje viejo después del nuevo.
/// </summary>
public sealed class DetenerDespachosAnteriores(IDetenedorDespachosAnteriores detenedor) : IComandoDifusion
{
    public string Canal => "Versiones anteriores";

    public async Task EjecutarAsync(ContextoDifusion c, CancellationToken ct = default) =>
        c.TrabajosDetenidos = await detenedor.DetenerAsync(c.Version, ct);
}

public sealed class PublicarEnWeb(IPublicadorWeb publicador) : IComandoDifusion
{
    public string Canal => "Web";

    public async Task EjecutarAsync(ContextoDifusion c, CancellationToken ct = default) =>
        c.Publicacion = await publicador.PublicarAsync(c.Version, ct);
}

/// <summary>
/// Crea un trabajo SMS por destinatario de la lista de destinos de la versión (RF-07);
/// el envío (por lotes, con reintentos y bitácora) lo hace la cola en segundo plano.
/// </summary>
public sealed class EncolarSms(IDestinatariosDifusion destinatarios, IColaSms cola) : IComandoDifusion
{
    public string Canal => "SMS";

    public async Task EjecutarAsync(ContextoDifusion c, CancellationToken ct = default)
    {
        var v = c.Version;
        var lista = await destinatarios.ObtenerSmsAsync(v, ct);
        if (lista.Count == 0)
        {
            c.Anotar("La lista de destinos de la versión no tiene ningún SMS (comunidades sin contactos con SMS o canal no elegido).");
            return;
        }
        var ahora = c.Reloj.GetUtcNow();
        c.TrabajosSms = await cola.EncolarAsync(lista.Select(d =>
            new EnvioSms(v.VersionId, v.AvisoId, d.ComunidadId, d.ContactoId, d.Telefono, v.TextoSms, v.EsPrueba, ahora)).ToList(), ct);
    }
}

/// <summary>
/// WhatsApp (RF-04): un mensaje por grupo configurado, con el texto completo y el nivel en palabras. Si el canal no está
/// configurado (sin WAHA o sin grupos) no hace nada.
/// </summary>
public sealed class EncolarWhatsApp(Canales.OpcionesWhatsApp opciones, IColaSms cola) : IComandoDifusion
{
    public string Canal => "WhatsApp";

    public async Task EjecutarAsync(ContextoDifusion c, CancellationToken ct = default)
    {
        if (!opciones.Habilitado) return;
        var v = c.Version;
        var ahora = c.Reloj.GetUtcNow();
        c.EnviosWhatsApp = await cola.EncolarAsync(opciones.GruposValidos
            .Select(g => new EnvioWhatsApp(v.VersionId, v.AvisoId, g.Id, g.Nombre, v.TextoCompleto, v.EsPrueba, ahora)).ToList(), ct);
    }
}

/// <summary>Email (RF-04): un correo por contacto con correo en la lista de destinos. Si el canal no está configurado, no hace nada.</summary>
public sealed class EncolarEmail(Canales.OpcionesEmail opciones, IDestinatariosDifusion destinatarios, IColaSms cola) : IComandoDifusion
{
    public string Canal => "Email";

    public async Task EjecutarAsync(ContextoDifusion c, CancellationToken ct = default)
    {
        if (!opciones.Habilitado) return;
        var v = c.Version;
        var lista = await destinatarios.ObtenerEmailAsync(v, ct);
        if (lista.Count == 0) return;
        var ahora = c.Reloj.GetUtcNow();
        var asunto = (v.EsPrueba ? "[PRUEBA] " : string.Empty) + v.Titular;
        c.EnviosEmail = await cola.EncolarAsync(lista.Select(d =>
            new EnvioEmail(v.VersionId, v.AvisoId, d.ComunidadId, d.ContactoId, d.Correo, asunto, v.TextoCompleto, v.EsPrueba, ahora)).ToList(), ct);
    }
}

/// <summary>
/// Radio, altavoz y redes sociales: crea las tareas para que una persona las ejecute y las confirme.
/// La radio y las redes van una vez para toda el área; el altavoz, una por comunidad del alcance.
/// </summary>
public sealed class CrearTareasManuales(IGestorTareasManuales gestor) : IComandoDifusion
{
    public static readonly IReadOnlyList<CanalDifusion> Canales = [CanalDifusion.Radio, CanalDifusion.Altavoz, CanalDifusion.RedesSociales];

    public string Canal => "Tareas manuales";

    public async Task EjecutarAsync(ContextoDifusion c, CancellationToken ct = default) =>
        c.Tareas.AddRange(await gestor.CrearAsync(c.Version, Canales, ct));
}
