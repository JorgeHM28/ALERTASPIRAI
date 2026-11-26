using SistemaAlertas.Domain.Difusion;

namespace SistemaAlertas.Application.Difusion;

/// <summary>Lo que se difunde: una versión aprobada del mensaje (RF-11) lista para los canales.</summary>
/// <param name="ZonaIds">Zonas que toca el área de la versión (informativo; los destinos salen de RF-07).</param>
/// <param name="EsPrueba">Aviso de prueba o simulacro: los canales lo marcan y el SMS real se bloquea.</param>
public sealed record VersionAprobada(Guid VersionId, Guid AvisoId, string Titular, string TextoCompleto, string TextoSms,
    IReadOnlyList<Guid> ZonaIds, bool EsPrueba);

public sealed record PublicacionWeb(Guid VersionId, string Url, DateTimeOffset PublicadaEn);

public sealed record DestinatarioSms(Guid ContactoId, Guid ComunidadId, string Nombre, string Telefono);

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

/// <summary>
/// Destinatarios con teléfono para SMS, tomados de la lista de destinos de la versión (RF-07).
/// Uso interno del sistema: devuelve los números completos.
/// </summary>
public interface IDestinatariosDifusion
{
    Task<IReadOnlyList<DestinatarioSms>> ObtenerSmsAsync(VersionAprobada version, CancellationToken ct = default);
}

/// <summary>
/// Cola de envíos SMS (RF-04): la difusión guarda un <see cref="EnvioSms"/> por destinatario y un proceso
/// en segundo plano los envía por lotes, con los reintentos de RNF-08. La cola vive en la base: sobrevive a un reinicio.
/// </summary>
public interface IColaSms
{
    Task<int> EncolarAsync(IReadOnlyCollection<EnvioSms> envios, CancellationToken ct = default);
}

/// <summary>Crea las tareas de los canales manuales (radio, altavoz, redes sociales) y las guarda.</summary>
public interface IGestorTareasManuales
{
    Task<IReadOnlyList<TareaManual>> CrearAsync(VersionAprobada version, IReadOnlyCollection<CanalDifusion> canales,
        CancellationToken ct = default);
}

/// <summary>
/// Genera, valida y archiva el CAP final de la versión aprobada (RF-04). Si el XML no es válido contra el XSD de CAP 1.2
/// lanza una excepción de dominio y no se difunde nada.
/// </summary>
public interface IArchivoCap
{
    Task<CapEmitido> EmitirAsync(VersionAprobada version, CancellationToken ct = default);
}

/// <summary>Incidencias de la difusión: un canal que falló o que no pudo ejecutarse.</summary>
public interface IRegistroIncidencias
{
    Task RegistrarAsync(IncidenciaDifusion incidencia, CancellationToken ct = default);
}
