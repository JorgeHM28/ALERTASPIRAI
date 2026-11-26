using System.Globalization;
using ClosedXML.Excel;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Application.Avisos.Importacion;

/// <summary>
/// Importador concreto (Sprint 3): aviso en una planilla Excel (.xlsx) con la plantilla del sistema
/// (docs/ejemplos/sprint-3/plantilla-aviso.xlsx). Las instituciones comparten sus datos en planillas; esta plantilla
/// fija las celdas para que se puedan importar sin transcribir. Sigue los mismos pasos que XML y JSON
/// (Template Method en <see cref="ImportadorCapBase{TAlerta, TInfo}"/>) y termina en el mismo Builder.
///
/// Plantilla: hoja "Aviso", columna A con el campo y columna B con el valor, una fila por campo. Las filas
/// "area" y "poligono" se repiten en pares para más de un área. El archivo llega a la API en base64 (texto).
/// </summary>
public sealed class ImportadorCapExcel : ImportadorCapBase<ImportadorCapExcel.Planilla, ImportadorCapExcel.Planilla>
{
    public const string Hoja = "Aviso";

    /// <summary>Un .xlsx es un ZIP: en base64 siempre empieza con "UEsDB" (los bytes PK\x03\x04).</summary>
    private const string FirmaBase64 = "UEsDB";

    public override FormatoAviso Formato => FormatoAviso.Excel;

    protected override char PrimerCaracter => 'U';

    public override bool Reconoce(string contenido) => contenido.TrimStart().StartsWith(FirmaBase64, StringComparison.Ordinal);

    protected override Planilla Leer(string contenido)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(contenido.Trim()); }
        catch (FormatException) { throw new ExcepcionDominio("El Excel debe llegar codificado en base64."); }

        try
        {
            using var libro = new XLWorkbook(new MemoryStream(bytes));
            if (!libro.TryGetWorksheet(Hoja, out var hoja))
                throw new ExcepcionDominio($"El Excel no tiene la hoja \"{Hoja}\" de la plantilla de avisos.");
            var campos = new List<(string Campo, string Valor)>();
            foreach (var fila in hoja.RowsUsed())
            {
                var campo = fila.Cell(1).GetString().Trim();
                if (campo.Length == 0 || campo.StartsWith('#')) continue;
                var celda = fila.Cell(2);
                var valor = celda.DataType == XLDataType.DateTime
                    ? new DateTimeOffset(DateTime.SpecifyKind(celda.GetDateTime(), DateTimeKind.Unspecified), TimeSpan.FromHours(-4)).ToString("o")
                    : celda.GetString().Trim();
                campos.Add((Normalizar(campo), valor));
            }
            // La fila de títulos ("Campo | Valor") es opcional.
            return new Planilla(campos.Where(c => c.Campo != "campo").ToList());
        }
        catch (ExcepcionDominio) { throw; }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or InvalidOperationException or FormatException)
        {
            throw new ExcepcionDominio($"El archivo no es un Excel (.xlsx) válido: {ex.Message}");
        }
    }

    protected override void LlenarEncabezado(Planilla p, MensajeCapBuilder builder) => builder
        .ConIdentificador(p.Valor("identificador") ?? string.Empty)
        .DeRemitente(p.Valor("remitente") ?? string.Empty)
        .EnviadoEn(p.Fecha("enviado") ?? DateTimeOffset.UtcNow)
        .ConEstado(p.Enum("estado", EstadoCap.Actual))
        .ConTipo(p.Enum("tipo", TipoMensajeCap.Alert))
        .ConAlcance(p.Enum("alcance", AlcanceCap.Public), p.Valor("restriccion"))
        .ConNota(p.Valor("nota"))
        .ConReferencias(p.Valor("referencias"));

    /// <summary>La plantilla tiene un solo bloque &lt;info&gt;.</summary>
    protected override IEnumerable<Planilla> Infos(Planilla p) => [p];

    protected override void LlenarInfo(Planilla p, InfoCapBuilder i)
    {
        i.EnIdioma(p.Valor("idioma") ?? "es-BO")
         .ConEvento(p.Valor("evento") ?? string.Empty)
         .ConNombreRemitente(p.Valor("nombreremitente"))
         .ConTitular(p.Valor("titular"))
         .ConDescripcion(p.Valor("descripcion"))
         .ConInstruccion(p.Valor("instruccion"))
         .VigenteDesde(p.Fecha("vigentedesde"))
         .ExpiraEn(p.Fecha("expira"));
        if (p.Valor("urgencia") is not null) i.ConUrgencia(p.Enum("urgencia", UrgenciaCap.Unknown));
        if (p.Valor("severidad") is not null) i.ConSeveridad(p.Enum("severidad", SeveridadCap.Unknown));
        if (p.Valor("certeza") is not null) i.ConCerteza(p.Enum("certeza", CertezaCap.Unknown));
        foreach (var c in (p.Valor("categoria") ?? "Met").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            i.ConCategoria(Planilla.Parsear<CategoriaCap>(c, "categoria"));

        // Pares "area" + "poligono", en el orden de las filas.
        string? area = null;
        foreach (var (campo, valor) in p.Campos)
        {
            if (campo == "area") area = valor;
            else if (campo == "poligono")
            {
                i.AgregarArea(area ?? string.Empty, valor.Length == 0 ? [] : [valor]);
                area = null;
            }
        }
        if (area is not null) i.AgregarArea(area);
    }

    private static string Normalizar(string campo) =>
        new string(campo.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && !char.IsWhiteSpace(c) && c != '_')
            .ToArray()).ToLowerInvariant();

    /// <summary>Los pares campo-valor leídos de la hoja.</summary>
    public sealed record Planilla(IReadOnlyList<(string Campo, string Valor)> Campos)
    {
        public string? Valor(string campo) =>
            Campos.FirstOrDefault(c => c.Campo == campo).Valor is { Length: > 0 } v ? v : null;

        public DateTimeOffset? Fecha(string campo) => Valor(campo) is not { } v ? null
            : DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f
            : throw new ExcepcionDominio($"La fecha de \"{campo}\" no es válida: {v} (usa AAAA-MM-DDTHH:MM-04:00).");

        public T Enum<T>(string campo, T porDefecto) where T : struct, System.Enum =>
            Valor(campo) is { } v ? Parsear<T>(v, campo) : porDefecto;

        public static T Parsear<T>(string valor, string campo) where T : struct, System.Enum =>
            System.Enum.TryParse<T>(valor, true, out var r) && System.Enum.IsDefined(r) ? r
                : throw new ExcepcionDominio($"\"{valor}\" no es un valor válido para {campo}. Valores: {string.Join(", ", System.Enum.GetNames<T>())}.");
    }
}
