using System.Globalization;
using System.Xml.Linq;
using SistemaAlertas.Domain.Cap;

namespace SistemaAlertas.Application.Cap;

/// <summary>Escribe un <see cref="MensajeCap"/> como XML CAP 1.2 (urn:oasis:names:tc:emergency:cap:1.2).</summary>
public static class CapXml
{
    public static readonly XNamespace Ns = "urn:oasis:names:tc:emergency:cap:1.2";

    public static string Serializar(MensajeCap m)
    {
        var alert = new XElement(Ns + "alert",
            new XElement(Ns + "identifier", m.Identifier),
            new XElement(Ns + "sender", m.Sender),
            new XElement(Ns + "sent", Fecha(m.Sent)),
            new XElement(Ns + "status", m.Status),
            new XElement(Ns + "msgType", m.MsgType),
            new XElement(Ns + "scope", m.Scope),
            Opcional("restriction", m.Restriction),
            Opcional("note", m.Note),
            Opcional("references", m.References),
            m.Infos.Select(Info));

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + alert.ToString(SaveOptions.None);
    }

    private static XElement Info(InfoCap i) => new(Ns + "info",
        new XElement(Ns + "language", i.Language),
        i.Categories.Select(c => new XElement(Ns + "category", c)),
        new XElement(Ns + "event", i.Event),
        new XElement(Ns + "urgency", i.Urgency),
        new XElement(Ns + "severity", i.Severity),
        new XElement(Ns + "certainty", i.Certainty),
        i.Effective is null ? null : new XElement(Ns + "effective", Fecha(i.Effective.Value)),
        i.Expires is null ? null : new XElement(Ns + "expires", Fecha(i.Expires.Value)),
        Opcional("senderName", i.SenderName),
        Opcional("headline", i.Headline),
        Opcional("description", i.Description),
        Opcional("instruction", i.Instruction),
        i.Areas.Select(a => new XElement(Ns + "area",
            new XElement(Ns + "areaDesc", a.AreaDesc),
            a.Polygons.Select(p => new XElement(Ns + "polygon", p)))));

    private static XElement? Opcional(string nombre, string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : new XElement(Ns + nombre, valor);

    /// <summary>
    /// Formato de fecha CAP: 2026-11-04T10:30:00-04:00 (sin fracciones, con zona).
    /// CAP 1.2 prohíbe "Z" y exige que UTC se escriba "-00:00".
    /// </summary>
    public static string Fecha(DateTimeOffset d)
    {
        var texto = d.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        return d.Offset == TimeSpan.Zero ? texto[..^6] + "-00:00" : texto;
    }
}
