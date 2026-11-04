using SistemaAlertas.Domain.Avisos;

namespace SistemaAlertas.Application.Avisos.Importacion;

/// <summary>
/// Creador del patrón Factory Method. El caso de uso pide "un importador" sin conocer las clases concretas.
/// Agregar un formato nuevo = una clase nueva que implemente <see cref="IImportadorAviso"/>, sin tocar este código.
/// </summary>
public interface IFabricaImportadores
{
    IReadOnlyCollection<FormatoAviso> FormatosSoportados { get; }
    IImportadorAviso Crear(FormatoAviso formato);
    IImportadorAviso CrearSegunContenido(string contenido);
}
