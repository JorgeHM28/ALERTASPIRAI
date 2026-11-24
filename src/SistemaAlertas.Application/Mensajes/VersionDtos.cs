using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Mensajes;

namespace SistemaAlertas.Application.Mensajes;

/// <summary>Redacción que escribe el operador. Lo que se deja en null en una versión nueva se toma del aviso oficial.</summary>
public sealed record RedaccionVersion(string? Titular, string? Descripcion, string? Instruccion);

public sealed record RechazoVersion(string? Motivo);

public sealed record NivelVista(string Codigo, string Texto, int Orden, string Color)
{
    public static NivelVista De(NivelAlerta n) => new(n.Codigo, n.Texto, n.Orden, n.Color);
}

public sealed record VersionResumen(
    Guid Id, Guid AvisoId, Guid CadenaId, int Numero, TipoMensajeCap Tipo, EstadoVersion Estado, bool EsPrueba,
    NivelVista Nivel, string Evento, string? Titular, string? DescripcionArea, DateTimeOffset? Expira,
    DateTimeOffset CreadoEn, Guid PreparadoPorId, DateTimeOffset? RevisadoEn, string? MotivoRechazo,
    Guid? ReemplazadaPorId, IReadOnlyList<string> Faltantes);

public sealed record VersionDetalle(
    VersionResumen Resumen, UrgenciaCap Urgencia, SeveridadCap Severidad, CertezaCap Certeza,
    string? Descripcion, string? Instruccion, DateTimeOffset? Vigente,
    string IdentificadorCap, string Remitente, DateTimeOffset? EnviadoCap, string? Referencias, Guid? ReferenciaVersionId,
    Guid? VersionAnteriorId, string? MotivoDevolucion, DateTimeOffset? EnviadaARevisionEn, Guid? RevisadoPorId,
    DateTimeOffset? DifundidaEn, string? XmlCap,
    // Fuente oficial: el aviso del que sale la versión (RF-11: el aprobador revisa la fuente).
    string FuenteRemitente, string FuenteIdentificador, DateTimeOffset FuenteEnviado, TipoMensajeCap FuenteTipo,
    // RF-05: protocolo oficial del que salen las instrucciones.
    Guid? ProtocoloId, string? ProtocoloTitulo, string? ProtocoloCita);

/// <param name="ProtocoloId">Protocolo oficial a vincular; null quita el vínculo.</param>
public sealed record VinculoProtocolo(Guid? ProtocoloId);

public static class VersionMapeo
{
    public static VersionResumen ResumenDe(VersionMensaje v, DateTimeOffset ahora) => new(
        v.Id, v.AvisoId, v.CadenaId, v.Numero, v.Tipo, v.Estado, v.EsPrueba, NivelVista.De(v.Nivel),
        v.Contenido.Evento, v.Contenido.Titular, v.Contenido.DescripcionArea, v.Contenido.Expira,
        v.CreadoEn, v.PreparadoPorId, v.RevisadoEn, v.MotivoRechazo, v.ReemplazadaPorId,
        v.Estado == EstadoVersion.Borrador ? v.Faltantes(ahora) : []);
}

/// <param name="Canales">Canales elegidos para el envío; vacío = todos los canales aprobados.</param>
public sealed record SeleccionDestinosDto(IReadOnlyList<TipoCanal>? Canales);

public sealed record DestinoVista(Guid ComunidadId, string Comunidad, Guid ContactoId, string Contacto, TipoCanal Canal, string Direccion);

public sealed record PendienteVista(Guid ComunidadId, string Comunidad, string Motivo);

/// <param name="Guardada">False en la vista previa: la lista se calculó pero no se guardó.</param>
public sealed record ListaDestinosVista(Guid VersionId, bool Guardada, DateTimeOffset? GeneradaEn,
    IReadOnlyList<TipoCanal> Canales, int Comunidades, IReadOnlyList<DestinoVista> Destinos, IReadOnlyList<PendienteVista> Pendientes);
