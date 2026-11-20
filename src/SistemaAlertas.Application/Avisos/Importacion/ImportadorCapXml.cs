using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Application.Avisos.Importacion;

/// <summary>
/// Importador concreto: mensaje CAP 1.2 en XML (el formato estándar de OASIS).
/// <para>
/// Además de producto del Factory Method, cumple el rol de <b>Adapter</b>: el SENAMHI y otras fuentes oficiales
/// publican XML CAP 1.2 (interfaz ajena, el adaptado), mientras que el sistema trabaja con <see cref="IImportadorAviso"/>
/// (el objetivo) y con el modelo <see cref="MensajeCap"/>, del que <c>ServicioAvisos</c> crea la entidad
/// <see cref="Aviso"/> de RF-01. Traduce nombres de elementos, enumeraciones, fechas y polígonos CAP al modelo interno,
/// y los errores de XML a <see cref="ExcepcionDominio"/>, así que nadie más en el sistema conoce el XML de OASIS.
/// </para>
/// <para>Los pasos los fija <see cref="ImportadorCapBase{TAlerta, TInfo}"/> (Template Method); aquí solo está lo propio del XML.</para>
/// </summary>
public sealed class ImportadorCapXml : ImportadorCapBase<XElement, XElement>
{
    public override FormatoAviso Formato => FormatoAviso.CapXml;

    protected override char PrimerCaracter => '<';

    protected override XElement Leer(string contenido)
    {
        XDocument doc;
        try
        {
            // DTD deshabilitado para evitar ataques XXE.
            using var lector = XmlReader.Create(new StringReader(contenido),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            doc = XDocument.Load(lector);
        }
        catch (XmlException ex)
        {
            throw new ExcepcionDominio($"El XML no es válido: {ex.Message}");
        }

        var alert = doc.Root;
        if (alert is null || alert.Name != CapXml.Ns + "alert")
            throw new ExcepcionDominio("El documento no es un mensaje CAP 1.2: falta <alert xmlns=\"urn:oasis:names:tc:emergency:cap:1.2\">.");
        return alert;
    }

    protected override void LlenarEncabezado(XElement alert, MensajeCapBuilder builder) => builder
        .ConIdentificador(Texto(alert, "identifier") ?? string.Empty)
        .DeRemitente(Texto(alert, "sender") ?? string.Empty)
        .EnviadoEn(Fecha(Texto(alert, "sent"), "sent") ?? DateTimeOffset.UtcNow)
        .ConEstado(Enum<EstadoCap>(Texto(alert, "status"), "status"))
        .ConTipo(Enum<TipoMensajeCap>(Texto(alert, "msgType"), "msgType"))
        .ConAlcance(Enum<AlcanceCap>(Texto(alert, "scope"), "scope"), Texto(alert, "restriction"))
        .ConNota(Texto(alert, "note"))
        .ConReferencias(Texto(alert, "references"));

    protected override IEnumerable<XElement> Infos(XElement alert) => alert.Elements(CapXml.Ns + "info");

    protected override void LlenarInfo(XElement info, InfoCapBuilder i)
    {
        i.EnIdioma(Texto(info, "language") ?? "en-US")
         .ConEvento(Texto(info, "event") ?? string.Empty)
         .ConUrgencia(Enum<UrgenciaCap>(Texto(info, "urgency"), "urgency"))
         .ConSeveridad(Enum<SeveridadCap>(Texto(info, "severity"), "severity"))
         .ConCerteza(Enum<CertezaCap>(Texto(info, "certainty"), "certainty"))
         .ConNombreRemitente(Texto(info, "senderName"))
         .ConTitular(Texto(info, "headline"))
         .ConDescripcion(Texto(info, "description"))
         .ConInstruccion(Texto(info, "instruction"))
         .VigenteDesde(Fecha(Texto(info, "effective"), "effective"))
         .ExpiraEn(Fecha(Texto(info, "expires"), "expires"));

        foreach (var categoria in info.Elements(CapXml.Ns + "category"))
            i.ConCategoria(Enum<CategoriaCap>(categoria.Value.Trim(), "category"));

        foreach (var area in info.Elements(CapXml.Ns + "area"))
            i.AgregarArea(Texto(area, "areaDesc") ?? string.Empty,
                area.Elements(CapXml.Ns + "polygon").Select(p => p.Value).ToArray());
    }

    private static string? Texto(XElement padre, string nombre) => padre.Element(CapXml.Ns + nombre)?.Value.Trim();

    private static T Enum<T>(string? valor, string campo) where T : struct, System.Enum =>
        System.Enum.TryParse<T>(valor, ignoreCase: false, out var r) && System.Enum.IsDefined(r)
            ? r
            : throw new ExcepcionDominio($"Valor '{valor}' no permitido en <{campo}>. Valores CAP: {string.Join(", ", System.Enum.GetNames<T>())}.");

    private static DateTimeOffset? Fecha(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return DateTimeOffset.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new ExcepcionDominio($"La fecha '{valor}' en <{campo}> no tiene formato CAP (ej. 2026-11-04T10:30:00-04:00).");
    }
}
