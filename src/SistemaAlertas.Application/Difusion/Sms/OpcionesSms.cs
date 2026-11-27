namespace SistemaAlertas.Application.Difusion.Sms;

/// <summary>
/// Proveedor de SMS. Simulador: no envía nada. Twilio: servicio en la nube (sin cuentas de prueba en Bolivia).
/// SmsGateway: un celular Android con la app "SMS Gateway for Android" envía con su chip.
/// </summary>
public enum ModoSms { Simulador, Twilio, SmsGateway }

/// <summary>Sección "Sms" de la configuración. Las credenciales van en user-secrets o variables de entorno, nunca en el repo.</summary>
public sealed class OpcionesSms
{
    public const string Seccion = "Sms";

    /// <summary>Proveedor activo. Por defecto el simulador, que no envía nada real.</summary>
    public ModoSms Modo { get; set; } = ModoSms.Simulador;

    /// <summary>Reintentos adicionales ante un fallo recuperable (RNF-08: como máximo 2).</summary>
    public int MaxReintentos { get; set; } = 2;

    /// <summary>Espera entre reintentos de la cola (RNF-08: 60 s).</summary>
    public int EsperaReintentoSegundos { get; set; } = 60;

    /// <summary>Cada cuánto revisa la cola si hay envíos pendientes.</summary>
    public int IntervaloColaSegundos { get; set; } = 5;

    /// <summary>Cuántos envíos toma la cola en cada vuelta.</summary>
    public int TamanoLote { get; set; } = 200;

    /// <summary>Cuántos SMS se mandan a la vez al proveedor.</summary>
    public int EnviosSimultaneos { get; set; } = 10;

    public OpcionesTwilio Twilio { get; set; } = new();

    public OpcionesSmsGateway SmsGateway { get; set; } = new();
}

/// <summary>Datos que muestra la app SMS Gateway for Android en su sección "Local Server" o "Cloud Server".</summary>
public sealed class OpcionesSmsGateway
{
    /// <summary>Modo local: http://IP-del-celular:8080. Modo nube: https://api.sms-gate.app/3rdparty/v1.</summary>
    public string Url { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    /// <summary>Cuánto se espera la respuesta del celular antes de dar el envío como fallo pasajero.</summary>
    public int TiempoEsperaSegundos { get; set; } = 15;
}

public sealed class OpcionesTwilio
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    /// <summary>Número remitente comprado en Twilio, en formato internacional.</summary>
    public string NumeroOrigen { get; set; } = string.Empty;
}
