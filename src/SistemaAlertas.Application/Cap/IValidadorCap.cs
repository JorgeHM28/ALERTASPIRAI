namespace SistemaAlertas.Application.Cap;

public sealed record ResultadoValidacionCap(bool Valido, IReadOnlyList<string> Errores)
{
    public static readonly ResultadoValidacionCap Correcto = new(true, []);
}

/// <summary>Valida un mensaje CAP 1.2 en XML sin registrarlo.</summary>
public interface IValidadorCap
{
    ResultadoValidacionCap Validar(string xml);
}
