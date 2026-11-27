namespace SistemaAlertas.Domain.Comunidades;

/// <summary>
/// Canales de un contacto. Los números son fijos: la lista de canales de una lista de destinos se guarda como
/// arreglo de enteros. El 2 era "Llamada", que se quitó en el Sprint 3 (el sistema no hace llamadas) y no se reutiliza.
/// </summary>
public enum TipoCanal { Sms = 0, WhatsApp = 1, Email = 3, Radio = 4 }
