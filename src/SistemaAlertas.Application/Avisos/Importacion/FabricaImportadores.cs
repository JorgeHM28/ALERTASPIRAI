using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Application.Avisos.Importacion;

/// <summary>
/// Implementación del Factory Method parametrizado (GoF, variante "parameterized factory method").
/// Recibe los importadores registrados en el contenedor de DI y se registra como Singleton:
/// no guarda estado por petición, así que una sola instancia sirve para toda la aplicación.
/// </summary>
public sealed class FabricaImportadores : IFabricaImportadores
{
    private readonly IReadOnlyDictionary<FormatoAviso, IImportadorAviso> _importadores;

    public FabricaImportadores(IEnumerable<IImportadorAviso> importadores)
    {
        _importadores = importadores.ToDictionary(i => i.Formato);
    }

    public IReadOnlyCollection<FormatoAviso> FormatosSoportados => _importadores.Keys.ToList();

    public IImportadorAviso Crear(FormatoAviso formato) =>
        _importadores.TryGetValue(formato, out var importador)
            ? importador
            : throw new ExcepcionDominio($"No hay un importador para el formato {formato}.");

    public IImportadorAviso CrearSegunContenido(string contenido) =>
        _importadores.Values.FirstOrDefault(i => i.Reconoce(contenido))
            ?? throw new ExcepcionDominio("No se reconoce el formato del aviso. Formatos soportados: "
                + string.Join(", ", _importadores.Keys) + ".");
}
