using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Domain.Mensajes;

/// <summary>
/// RF-05: protocolo oficial del que salen las instrucciones a la población (por ejemplo, el plan de evacuación del COED).
/// Guarda de dónde viene: institución, documento y fecha. No se borra: si deja de valer se desactiva, porque las
/// versiones aprobadas siguen citándolo.
/// </summary>
public class ProtocoloOficial : Entidad
{
    public string Titulo { get; private set; } = null!;
    /// <summary>Texto oficial de las instrucciones, tal como se copia en la versión.</summary>
    public string Instruccion { get; private set; } = null!;
    /// <summary>Institución que emite el protocolo (COED, SEARPI, Defensa Civil…).</summary>
    public string Fuente { get; private set; } = null!;
    /// <summary>Documento de origen: número de resolución, plan o circular.</summary>
    public string Referencia { get; private set; } = null!;
    public DateOnly FechaDocumento { get; private set; }
    public bool Activo { get; private set; } = true;

    private ProtocoloOficial() { }

    public ProtocoloOficial(string titulo, string instruccion, string fuente, string referencia, DateOnly fechaDocumento) =>
        Actualizar(titulo, instruccion, fuente, referencia, fechaDocumento);

    public void Actualizar(string titulo, string instruccion, string fuente, string referencia, DateOnly fechaDocumento)
    {
        Titulo = Obligatorio(titulo, "el título", 200);
        Instruccion = Obligatorio(instruccion, "el texto de las instrucciones", 4000);
        Fuente = Obligatorio(fuente, "la institución que lo emite", 150);
        Referencia = Obligatorio(referencia, "la referencia del documento", 200);
        if (fechaDocumento == default) throw new ExcepcionDominio("La fecha del documento del protocolo es obligatoria.");
        if (fechaDocumento > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            throw new ExcepcionDominio("La fecha del documento del protocolo no puede ser futura.");
        FechaDocumento = fechaDocumento;
    }

    public void Desactivar() => Activo = false;
    public void Activar() => Activo = true;

    /// <summary>Cita corta para mostrar junto a las instrucciones: "COED Santa Cruz, Plan 01/2026, 15/01/2026".</summary>
    public string Cita => $"{Fuente}, {Referencia}, {FechaDocumento:dd/MM/yyyy}";

    private static string Obligatorio(string? valor, string nombre, int largo)
    {
        if (string.IsNullOrWhiteSpace(valor)) throw new ExcepcionDominio($"Falta {nombre} del protocolo.");
        valor = valor.Trim();
        if (valor.Length > largo) throw new ExcepcionDominio($"En el protocolo, {nombre} admite hasta {largo} caracteres.");
        return valor;
    }
}
