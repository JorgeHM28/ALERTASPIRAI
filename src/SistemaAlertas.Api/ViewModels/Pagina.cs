namespace SistemaAlertas.Api.ViewModels;
public sealed record Opcion(string Valor, string Texto);
public sealed record Campo(string Nombre, string Etiqueta, string Tipo = "text", string? Valor = null,
    bool Obligatorio = false, IEnumerable<Opcion>? Opciones = null);
public sealed record Formulario(string Titulo, string Accion, IReadOnlyList<Campo> Campos, string Boton = "Guardar", string Metodo = "post");
public sealed record Enlace(string Texto, string Url);
public sealed record Fila(IReadOnlyList<string> Celdas, IReadOnlyList<Enlace> Enlaces);
public sealed class Pagina(string titulo)
{
    public string Titulo { get; } = titulo;
    public string? Descripcion { get; set; }
    public IReadOnlyList<string> Columnas { get; set; } = [];
    public List<Fila> Filas { get; } = [];
    public List<Formulario> Formularios { get; } = [];
    public List<Enlace> Enlaces { get; } = [];
    public List<(string Titulo, string Texto)> Textos { get; } = [];
    public string? GeoJson { get; set; }
}
