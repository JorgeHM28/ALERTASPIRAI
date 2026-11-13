namespace SistemaAlertas.Domain.Avisos;

/// <summary>
/// Ciclo de vida del aviso (RF-11). Es el valor que se guarda en la base; el comportamiento de cada estado
/// vive en <see cref="EstadoAvisoBase"/> (patrón State).
/// Registrado → Aprobado → Difundido. Registrado o Aprobado → Descartado.
/// </summary>
public enum EstadoAviso { Registrado, Aprobado, Difundido, Descartado }
