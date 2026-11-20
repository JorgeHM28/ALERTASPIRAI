using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;

namespace SistemaAlertas.Application.Avisos.Importacion;

/// <summary>
/// Producto del patrón Factory Method: cada formato de entrada tiene su propio importador,
/// que convierte el contenido recibido en un <see cref="MensajeCap"/> válido.
/// </summary>
public interface IImportadorAviso
{
    FormatoAviso Formato { get; }

    /// <summary>Indica si el contenido parece estar en este formato (para la detección automática).</summary>
    bool Reconoce(string contenido);

    MensajeCap Importar(string contenido);
}
