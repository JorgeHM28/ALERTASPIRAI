using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;

namespace SistemaAlertas.Application.Cap;

/// <summary>Fachada del subsistema CAP: delega en la fábrica de importadores, el serializador y el validador.</summary>
public sealed class ModuloCap(IFabricaImportadores fabrica, IValidadorCap validador) : IModuloCap
{
    public IReadOnlyCollection<FormatoAviso> FormatosSoportados => fabrica.FormatosSoportados;

    public CapImportado Importar(string contenido, FormatoAviso? formato = null)
    {
        var importador = formato is { } f ? fabrica.Crear(f) : fabrica.CrearSegunContenido(contenido);
        return new CapImportado(importador.Importar(contenido), importador.Formato);
    }

    public string Generar(MensajeCap mensaje) => CapXml.Serializar(mensaje);

    public ResultadoValidacionCap Validar(string xml) => validador.Validar(xml);
}
