using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;

namespace SistemaAlertas.Application.Avisos;

public sealed record RegistrarAvisoManual(
    string Evento,
    UrgenciaCap Urgencia,
    SeveridadCap Severidad,
    CertezaCap Certeza,
    string? Titular,
    string? Descripcion,
    string? Instruccion,
    DateTimeOffset? Vigente,
    DateTimeOffset? Expira,
    IReadOnlyList<Guid>? ZonaIds);

public sealed record AvisoResumen(
    Guid Id, string IdentificadorCap, string Remitente, DateTimeOffset Enviado, FormatoAviso Formato,
    EstadoAviso Estado, string Evento, UrgenciaCap Urgencia, SeveridadCap Severidad, CertezaCap Certeza,
    string? Titular, string? DescripcionArea, DateTimeOffset? Expira, DateTimeOffset RegistradoEn);

public static class AvisoMapeo
{
    public static AvisoResumen ResumenDe(Aviso a) => new(a.Id, a.IdentificadorCap, a.Remitente, a.Enviado, a.Formato,
        a.Estado, a.Evento, a.Urgencia, a.Severidad, a.Certeza, a.Titular, a.DescripcionArea, a.Expira, a.CreadoEn);
}
