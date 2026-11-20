using System.Text.Json;
using System.Text.Json.Serialization;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Application.Avisos.Importacion;

/// <summary>
/// Importador concreto: CAP representado en JSON (mismos nombres de campo que el XML),
/// el formato que exponen varias APIs de alertas. Sigue los mismos pasos que el XML (Template Method en
/// <see cref="ImportadorCapBase{TAlerta, TInfo}"/>) y termina en el mismo Builder.
/// </summary>
public sealed class ImportadorCapJson : ImportadorCapBase<ImportadorCapJson.AlertJson, ImportadorCapJson.InfoJson>
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public override FormatoAviso Formato => FormatoAviso.CapJson;

    protected override char PrimerCaracter => '{';

    protected override AlertJson Leer(string contenido)
    {
        try
        {
            return JsonSerializer.Deserialize<AlertJson>(contenido, Opciones)
                ?? throw new ExcepcionDominio("El JSON está vacío.");
        }
        catch (JsonException ex)
        {
            throw new ExcepcionDominio($"El JSON CAP no es válido: {ex.Message}");
        }
    }

    protected override void LlenarEncabezado(AlertJson alert, MensajeCapBuilder builder) => builder
        .ConIdentificador(alert.Identifier ?? string.Empty)
        .DeRemitente(alert.Sender ?? string.Empty)
        .EnviadoEn(alert.Sent ?? DateTimeOffset.UtcNow)
        .ConEstado(alert.Status ?? EstadoCap.Actual)
        .ConTipo(alert.MsgType ?? TipoMensajeCap.Alert)
        .ConAlcance(alert.Scope ?? AlcanceCap.Public, alert.Restriction)
        .ConNota(alert.Note)
        .ConReferencias(alert.References);

    protected override IEnumerable<InfoJson> Infos(AlertJson alert) => alert.Info ?? [];

    protected override void LlenarInfo(InfoJson info, InfoCapBuilder i)
    {
        i.EnIdioma(info.Language ?? "es-BO")
         .ConEvento(info.Event ?? string.Empty)
         .ConNombreRemitente(info.SenderName)
         .ConTitular(info.Headline)
         .ConDescripcion(info.Description)
         .ConInstruccion(info.Instruction)
         .VigenteDesde(info.Effective)
         .ExpiraEn(info.Expires);
        if (info.Urgency is { } u) i.ConUrgencia(u);
        if (info.Severity is { } s) i.ConSeveridad(s);
        if (info.Certainty is { } c) i.ConCerteza(c);
        foreach (var cat in info.Category ?? []) i.ConCategoria(cat);
        foreach (var area in info.Area ?? []) i.AgregarArea(area.AreaDesc ?? string.Empty, (area.Polygon ?? []).ToArray());
    }

    public sealed record AlertJson(string? Identifier, string? Sender, DateTimeOffset? Sent, EstadoCap? Status,
        TipoMensajeCap? MsgType, AlcanceCap? Scope, string? Restriction, string? Note, string? References, List<InfoJson>? Info);

    public sealed record InfoJson(string? Language, List<CategoriaCap>? Category, string? Event, UrgenciaCap? Urgency,
        SeveridadCap? Severity, CertezaCap? Certainty, string? SenderName, string? Headline, string? Description,
        string? Instruction, DateTimeOffset? Effective, DateTimeOffset? Expires, List<AreaJson>? Area);

    public sealed record AreaJson(string? AreaDesc, List<string>? Polygon);
}
