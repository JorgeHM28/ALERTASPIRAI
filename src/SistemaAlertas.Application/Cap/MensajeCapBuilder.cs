using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Application.Cap;

/// <summary>
/// Patrón Builder: arma un <see cref="MensajeCap"/> paso a paso y valida las reglas de CAP 1.2
/// en <see cref="Build"/>. Así ningún mensaje inválido llega a guardarse ni a difundirse.
/// </summary>
/// <example>
/// var mensaje = new MensajeCapBuilder()
///     .DeRemitente("coe@santacruz.gob.bo")
///     .ConTipo(TipoMensajeCap.Alert)
///     .AgregarInfo(i => i
///         .ConEvento("Crecida del río Piraí")
///         .ConUrgencia(UrgenciaCap.Immediate).ConSeveridad(SeveridadCap.Severe).ConCerteza(CertezaCap.Likely)
///         .AgregarArea("Comunidad X", "-17.7,-63.2 -17.7,-63.1 -17.8,-63.1 -17.7,-63.2"))
///     .Build();
/// </example>
public sealed class MensajeCapBuilder
{
    private static readonly char[] CaracteresProhibidos = [' ', ',', '<', '&'];

    private string? _identifier;
    private string? _sender;
    private DateTimeOffset? _sent;
    private EstadoCap _status = EstadoCap.Actual;
    private TipoMensajeCap _msgType = TipoMensajeCap.Alert;
    private AlcanceCap _scope = AlcanceCap.Public;
    private string? _restriction;
    private string? _note;
    private string? _references;
    private readonly List<InfoCapBuilder> _infos = [];

    public MensajeCapBuilder ConIdentificador(string identifier) { _identifier = identifier?.Trim(); return this; }
    public MensajeCapBuilder DeRemitente(string sender) { _sender = sender?.Trim(); return this; }
    public MensajeCapBuilder EnviadoEn(DateTimeOffset sent) { _sent = sent; return this; }
    public MensajeCapBuilder ConEstado(EstadoCap status) { _status = status; return this; }
    public MensajeCapBuilder ConTipo(TipoMensajeCap msgType) { _msgType = msgType; return this; }
    public MensajeCapBuilder ConAlcance(AlcanceCap scope, string? restriction = null) { _scope = scope; _restriction = restriction; return this; }
    public MensajeCapBuilder ConNota(string? note) { _note = note; return this; }
    public MensajeCapBuilder ConReferencias(string? references) { _references = references; return this; }

    public MensajeCapBuilder AgregarInfo(Action<InfoCapBuilder> configurar)
    {
        var info = new InfoCapBuilder();
        configurar(info);
        _infos.Add(info);
        return this;
    }

    public MensajeCap Build()
    {
        var errores = new List<string>();

        var identifier = string.IsNullOrWhiteSpace(_identifier) ? $"SAT-PIRAI-{Guid.NewGuid():N}" : _identifier;
        if (identifier.IndexOfAny(CaracteresProhibidos) >= 0)
            errores.Add("identifier no puede contener espacios, comas, '<' ni '&'.");

        if (string.IsNullOrWhiteSpace(_sender)) errores.Add("sender es obligatorio.");
        else if (_sender.IndexOfAny(CaracteresProhibidos) >= 0)
            errores.Add("sender no puede contener espacios, comas, '<' ni '&'.");

        if (_scope == AlcanceCap.Restricted && string.IsNullOrWhiteSpace(_restriction))
            errores.Add("restriction es obligatorio cuando scope es Restricted.");

        if (_msgType is TipoMensajeCap.Update or TipoMensajeCap.Cancel or TipoMensajeCap.Ack or TipoMensajeCap.Error
            && string.IsNullOrWhiteSpace(_references))
            errores.Add($"references es obligatorio para msgType {_msgType}.");

        if (_msgType is TipoMensajeCap.Alert or TipoMensajeCap.Update && _infos.Count == 0)
            errores.Add("Un mensaje Alert o Update necesita al menos un bloque info.");

        var infos = new List<InfoCap>();
        for (var i = 0; i < _infos.Count; i++)
        {
            infos.Add(_infos[i].Build(errores, $"info[{i}]"));
        }

        if (errores.Count > 0)
            throw new ExcepcionDominio("Mensaje CAP inválido: " + string.Join(" ", errores));

        // CAP exige la hora con zona horaria y sin fracciones de segundo.
        var sent = _sent ?? DateTimeOffset.UtcNow;
        sent = new DateTimeOffset(sent.Year, sent.Month, sent.Day, sent.Hour, sent.Minute, sent.Second, sent.Offset);

        return new MensajeCap(identifier, _sender!, sent, _status, _msgType, _scope,
            _restriction, _note, _references, infos);
    }
}

public sealed class InfoCapBuilder
{
    private string _language = "es-BO";
    private readonly List<CategoriaCap> _categories = [];
    private string? _event;
    private UrgenciaCap? _urgency;
    private SeveridadCap? _severity;
    private CertezaCap? _certainty;
    private string? _senderName, _headline, _description, _instruction;
    private DateTimeOffset? _effective, _expires;
    private readonly List<AreaCap> _areas = [];

    public InfoCapBuilder EnIdioma(string language) { _language = language; return this; }
    public InfoCapBuilder ConCategoria(CategoriaCap categoria) { _categories.Add(categoria); return this; }
    public InfoCapBuilder ConEvento(string evento) { _event = evento?.Trim(); return this; }
    public InfoCapBuilder ConUrgencia(UrgenciaCap urgencia) { _urgency = urgencia; return this; }
    public InfoCapBuilder ConSeveridad(SeveridadCap severidad) { _severity = severidad; return this; }
    public InfoCapBuilder ConCerteza(CertezaCap certeza) { _certainty = certeza; return this; }
    public InfoCapBuilder ConNombreRemitente(string? nombre) { _senderName = nombre; return this; }
    public InfoCapBuilder ConTitular(string? titular) { _headline = titular; return this; }
    public InfoCapBuilder ConDescripcion(string? descripcion) { _description = descripcion; return this; }
    public InfoCapBuilder ConInstruccion(string? instruccion) { _instruction = instruccion; return this; }
    public InfoCapBuilder VigenteDesde(DateTimeOffset? efectivo) { _effective = efectivo; return this; }
    public InfoCapBuilder ExpiraEn(DateTimeOffset? expira) { _expires = expira; return this; }

    public InfoCapBuilder AgregarArea(string descripcion, params string[] poligonos)
    {
        _areas.Add(new AreaCap(descripcion?.Trim() ?? string.Empty, poligonos.Select(p => p.Trim()).ToList()));
        return this;
    }

    internal InfoCap Build(List<string> errores, string ruta)
    {
        if (string.IsNullOrWhiteSpace(_event)) errores.Add($"{ruta}.event es obligatorio.");
        if (_urgency is null) errores.Add($"{ruta}.urgency es obligatorio.");
        if (_severity is null) errores.Add($"{ruta}.severity es obligatorio.");
        if (_certainty is null) errores.Add($"{ruta}.certainty es obligatorio.");
        if (_effective is not null && _expires is not null && _expires <= _effective)
            errores.Add($"{ruta}.expires debe ser posterior a effective.");

        var categorias = _categories.Count == 0 ? [CategoriaCap.Met] : _categories.Distinct().ToList();

        for (var a = 0; a < _areas.Count; a++)
        {
            var area = _areas[a];
            if (string.IsNullOrWhiteSpace(area.AreaDesc)) errores.Add($"{ruta}.area[{a}].areaDesc es obligatorio.");
            foreach (var poligono in area.Polygons)
            {
                var error = PoligonoCap.Validar(poligono);
                if (error is not null) errores.Add($"{ruta}.area[{a}].polygon: {error}");
            }
        }

        return new InfoCap(_language, categorias, _event ?? string.Empty,
            _urgency ?? UrgenciaCap.Unknown, _severity ?? SeveridadCap.Unknown, _certainty ?? CertezaCap.Unknown,
            _senderName, _headline, _description, _instruction, _effective, _expires, _areas.ToList());
    }
}
