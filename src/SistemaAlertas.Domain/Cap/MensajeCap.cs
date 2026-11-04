namespace SistemaAlertas.Domain.Cap;

// Modelo inmutable de un mensaje CAP 1.2. Se construye con MensajeCapBuilder (patrón Builder),
// que garantiza que todo MensajeCap cumpla las reglas del estándar.

public sealed record MensajeCap(
    string Identifier,
    string Sender,
    DateTimeOffset Sent,
    EstadoCap Status,
    TipoMensajeCap MsgType,
    AlcanceCap Scope,
    string? Restriction,
    string? Note,
    string? References,
    IReadOnlyList<InfoCap> Infos);

public sealed record InfoCap(
    string Language,
    IReadOnlyList<CategoriaCap> Categories,
    string Event,
    UrgenciaCap Urgency,
    SeveridadCap Severity,
    CertezaCap Certainty,
    string? SenderName,
    string? Headline,
    string? Description,
    string? Instruction,
    DateTimeOffset? Effective,
    DateTimeOffset? Expires,
    IReadOnlyList<AreaCap> Areas);

/// <param name="Polygons">Polígonos en formato CAP: pares "lat,lon" separados por espacios; el primero y el último son iguales.</param>
public sealed record AreaCap(string AreaDesc, IReadOnlyList<string> Polygons);
