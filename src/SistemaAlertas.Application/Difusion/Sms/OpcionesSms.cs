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

    /// <summary>Espera base entre reintentos; crece con cada intento.</summary>
    public int EsperaReintentoMs { get; set; } = 500;

    public OpcionesTwilio Twilio { get; set; } = new();
}

public sealed class OpcionesTwilio
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    /// <summary>Número remitente comprado en Twilio, en formato internacional.</summary>
    public string NumeroOrigen { get; set; } = string.Empty;
}
