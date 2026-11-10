using System.Xml;
using System.Xml.Linq;
using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Application.Cap;

/// <summary>
/// Validación básica de estructura CAP 1.2: XML bien formado y sin DTD, raíz &lt;alert&gt; con el namespace de OASIS,
/// elementos obligatorios de &lt;alert&gt;, &lt;info&gt; y &lt;area&gt;, y polígonos bien escritos.
/// Si la estructura está bien, revisa los valores (enumeraciones, fechas, reglas) con el importador y el Builder.
/// No reemplaza la validación contra el XSD oficial, que puede agregarse detrás de esta misma interfaz.
/// </summary>
public sealed class ValidadorCap : IValidadorCap
{
    private static readonly string[] ObligatoriosAlert = ["identifier", "sender", "sent", "status", "msgType", "scope"];
    private static readonly string[] ObligatoriosInfo = ["category", "event", "urgency", "severity", "certainty"];

    private readonly ImportadorCapXml _importador = new();

    public ResultadoValidacionCap Validar(string xml)
    {
        XDocument doc;
        try
        {
            using var lector = XmlReader.Create(new StringReader(xml ?? string.Empty),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            doc = XDocument.Load(lector);
        }
        catch (XmlException ex)
        {
            return new(false, [$"El XML no está bien formado: {ex.Message}"]);
        }

        var alert = doc.Root!;
        if (alert.Name != CapXml.Ns + "alert")
            return new(false, ["La raíz debe ser <alert xmlns=\"urn:oasis:names:tc:emergency:cap:1.2\">."]);

        var errores = new List<string>();
        Faltantes(alert, "alert", ObligatoriosAlert, errores);
        var infos = alert.Elements(CapXml.Ns + "info").ToList();
        var tipo = alert.Element(CapXml.Ns + "msgType")?.Value.Trim();
        if (infos.Count == 0 && tipo is "Alert" or "Update")
            errores.Add("Un mensaje Alert o Update necesita al menos un <info>.");

        for (var i = 0; i < infos.Count; i++)
        {
            var donde = $"info[{i + 1}]";
            Faltantes(infos[i], donde, ObligatoriosInfo, errores);
            var areas = infos[i].Elements(CapXml.Ns + "area").ToList();
            for (var a = 0; a < areas.Count; a++)
            {
                if (string.IsNullOrWhiteSpace(areas[a].Element(CapXml.Ns + "areaDesc")?.Value))
                    errores.Add($"{donde}/area[{a + 1}]: falta <areaDesc>.");
                foreach (var poligono in areas[a].Elements(CapXml.Ns + "polygon"))
                    if (PoligonoCap.Validar(poligono.Value) is { } error)
                        errores.Add($"{donde}/area[{a + 1}]: polígono inválido, {error}");
            }
        }

        if (errores.Count == 0)
        {
            try { _importador.Importar(xml!); }
            catch (ExcepcionDominio ex) { errores.Add(ex.Message); }
        }
        return errores.Count == 0 ? ResultadoValidacionCap.Correcto : new(false, errores);
    }

    private static void Faltantes(XElement padre, string donde, IEnumerable<string> obligatorios, List<string> errores)
    {
        foreach (var nombre in obligatorios)
            if (string.IsNullOrWhiteSpace(padre.Element(CapXml.Ns + nombre)?.Value))
                errores.Add($"{donde}: falta <{nombre}>.");
    }
}
