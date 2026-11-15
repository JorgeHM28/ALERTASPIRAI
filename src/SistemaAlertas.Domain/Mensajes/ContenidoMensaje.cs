using SistemaAlertas.Domain.Cap;

namespace SistemaAlertas.Domain.Mensajes;

/// <summary>
/// Contenido de una versión del mensaje (RF-02). Es un <c>record</c> inmutable: la versión nueva se obtiene clonando
/// la anterior con <c>with</c> (patrón Prototype) y cambiando solo la redacción.
/// Los hechos (evento, urgencia, severidad, certeza), el nivel y el ámbito (vigencia y área) son los del aviso oficial
/// y no se editan; lo que el operador redacta es el titular, la descripción y la instrucción.
/// </summary>
public sealed record ContenidoMensaje(
    string Evento,
    UrgenciaCap Urgencia,
    SeveridadCap Severidad,
    CertezaCap Certeza,
    string? Titular,
    string? Descripcion,
    string? Instruccion,
    DateTimeOffset? Vigente,
    DateTimeOffset? Expira,
    string? DescripcionArea)
{
    public NivelAlerta Nivel => NivelAlerta.De(Severidad);

    /// <summary>Cambia solo la redacción; los hechos oficiales se conservan.</summary>
    public ContenidoMensaje Redactar(string? titular, string? descripcion, string? instruccion) => this with
    {
        Titular = Limpio(titular, 300),
        Descripcion = Limpio(descripcion, 4000),
        Instruccion = Limpio(instruccion, 4000)
    };

    private static string? Limpio(string? texto, int maximo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        texto = texto.Trim();
        return texto.Length <= maximo ? texto
            : throw new Comun.ExcepcionDominio($"El texto supera el máximo de {maximo} caracteres.");
    }
}
