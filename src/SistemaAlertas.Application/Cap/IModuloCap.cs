using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;

namespace SistemaAlertas.Application.Cap;

/// <param name="Formato">Formato con el que se leyó el contenido (indicado o detectado).</param>
public sealed record CapImportado(MensajeCap Mensaje, FormatoAviso Formato);

/// <summary>
/// Patrón Facade. Punto de entrada único para todo lo que tiene que ver con CAP 1.2: importar (Factory Method + Adapter),
/// generar (Builder + serializador) y validar. Los casos de uso no necesitan conocer la fábrica de importadores,
/// <see cref="CapXml"/> ni el validador.
/// </summary>
public interface IModuloCap
{
    IReadOnlyCollection<FormatoAviso> FormatosSoportados { get; }

    /// <summary>Lee un mensaje CAP en XML o JSON. Si no se indica el formato, se detecta por el contenido.</summary>
    CapImportado Importar(string contenido, FormatoAviso? formato = null);

    /// <summary>Escribe el mensaje como XML CAP 1.2.</summary>
    string Generar(MensajeCap mensaje);

    ResultadoValidacionCap Validar(string xml);
}
