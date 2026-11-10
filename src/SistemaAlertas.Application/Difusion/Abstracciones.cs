using SistemaAlertas.Application.Difusion.Sms;

namespace SistemaAlertas.Application.Difusion;

/// <summary>Lo que se difunde: una versión aprobada del mensaje (RF-11) lista para los canales.</summary>
/// <param name="EsPrueba">Aviso de prueba o simulacro: los canales lo marcan y el SMS real se bloquea.</param>
public sealed record VersionAprobada(Guid VersionId, Guid AvisoId, string Titular, string TextoCompleto, string TextoSms,
    IReadOnlyList<Guid> ZonaIds, bool EsPrueba);

public sealed record PublicacionWeb(Guid VersionId, string Url, DateTimeOffset PublicadaEn);

public sealed record DestinatarioSms(Guid ContactoId, string Nombre, string Telefono);

public sealed record TrabajoSms(Guid Id, Guid VersionId, Guid ContactoId, MensajeSms Mensaje);

/// <summary>Canales que no se automatizan: una persona los ejecuta y marca la tarea como hecha.</summary>
public enum CanalManual { Radio, Altavoz, RedesSociales }

public enum EstadoTarea { Pendiente, Hecha }

public sealed record TareaManual(Guid Id, Guid VersionId, CanalManual Canal, string Instrucciones, EstadoTarea Estado,
    DateTimeOffset CreadaEn);

public sealed record IncidenciaDifusion(Guid VersionId, string Canal, string Detalle, DateTimeOffset Fecha);

/// <summary>Obtiene la versión aprobada que se va a difundir. Null si no existe.</summary>
public interface IFuenteVersionesAprobadas
{
    Task<VersionAprobada?> ObtenerAsync(Guid versionAprobadaId, CancellationToken ct = default);
}

/// <summary>Publica el aviso en la web pública (RF-04, canal web).</summary>
public interface IPublicadorWeb
{
    Task<PublicacionWeb> PublicarAsync(VersionAprobada version, CancellationToken ct = default);
}

/// <summary>Destinatarios con teléfono para SMS. Uso interno del sistema: devuelve los números completos.</summary>
public interface IDestinatariosDifusion
{
    Task<IReadOnlyList<DestinatarioSms>> ObtenerSmsAsync(IReadOnlyCollection<Guid> zonaIds, CancellationToken ct = default);
}

/// <summary>Cola de trabajos SMS: la difusión los crea y un proceso en segundo plano los envía.</summary>
public interface IColaSms
{
    Task<int> EncolarAsync(IReadOnlyCollection<TrabajoSms> trabajos, CancellationToken ct = default);
}

/// <summary>Crea las tareas de los canales manuales (radio, altavoz, redes sociales).</summary>
public interface IGestorTareasManuales
{
    Task<IReadOnlyList<TareaManual>> CrearAsync(VersionAprobada version, IReadOnlyCollection<CanalManual> canales,
        CancellationToken ct = default);

    IReadOnlyList<TareaManual> Listar(Guid? versionId = null);
}

/// <summary>Incidencias de la difusión: un canal que falló o que no pudo ejecutarse.</summary>
public interface IRegistroIncidencias
{
    Task RegistrarAsync(IncidenciaDifusion incidencia, CancellationToken ct = default);
}
