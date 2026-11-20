namespace SistemaAlertas.Domain.Comun;

/// <summary>Regla de negocio violada. La API la traduce a 400 Bad Request.</summary>
public class ExcepcionDominio(string mensaje) : Exception(mensaje);
