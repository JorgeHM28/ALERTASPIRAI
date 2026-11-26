namespace SistemaAlertas.Application.Difusion.Sms;

public enum ModoSms { Simulador, Twilio }

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
}

public sealed class OpcionesTwilio
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    /// <summary>Número remitente comprado en Twilio, en formato internacional.</summary>
    public string NumeroOrigen { get; set; } = string.Empty;
}
