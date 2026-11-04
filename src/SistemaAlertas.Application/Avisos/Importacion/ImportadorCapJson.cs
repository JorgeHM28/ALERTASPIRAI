using System.Text.Json;
using System.Text.Json.Serialization;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Application.Avisos.Importacion;

/// <summary>
/// Importador concreto: CAP representado en JSON (mismos nombres de campo que el XML),
/// el formato que exponen varias APIs de alertas. Reutiliza el mismo Builder que el importador XML.
/// </summary>
public sealed class ImportadorCapJson : IImportadorAviso
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public FormatoAviso Formato => FormatoAviso.CapJson;

    public bool Reconoce(string contenido) => contenido.TrimStart().StartsWith('{');

    public MensajeCap Importar(string contenido)
    {
        AlertJson alert;
        try
        {
            alert = JsonSerializer.Deserialize<AlertJson>(contenido, Opciones)
                ?? throw new ExcepcionDominio("El JSON está vacío.");
        }
        catch (JsonException ex)
        {
            throw new ExcepcionDominio($"El JSON CAP no es válido: {ex.Message}");
        }

        var builder = new MensajeCapBuilder()
            .ConIdentificador(alert.Identifier ?? string.Empty)
            .DeRemitente(alert.Sender ?? string.Empty)
            .EnviadoEn(alert.Sent ?? DateTimeOffset.UtcNow)
            .ConEstado(alert.Status ?? EstadoCap.Actual)
            .ConTipo(alert.MsgType ?? TipoMensajeCap.Alert)
            .ConAlcance(alert.Scope ?? AlcanceCap.Public, alert.Restriction)
            .ConNota(alert.Note)
            .ConReferencias(alert.References);

        foreach (var info in alert.Info ?? [])
        {
            builder.AgregarInfo(i =>
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
            });
        }

        return builder.Build();
    }

    private sealed record AlertJson(string? Identifier, string? Sender, DateTimeOffset? Sent, EstadoCap? Status,
        TipoMensajeCap? MsgType, AlcanceCap? Scope, string? Restriction, string? Note, string? References, List<InfoJson>? Info);

    private sealed record InfoJson(string? Language, List<CategoriaCap>? Category, string? Event, UrgenciaCap? Urgency,
        SeveridadCap? Severity, CertezaCap? Certainty, string? SenderName, string? Headline, string? Description,
        string? Instruction, DateTimeOffset? Effective, DateTimeOffset? Expires, List<AreaJson>? Area);

    private sealed record AreaJson(string? AreaDesc, List<string>? Polygon);
}
