namespace SistemaAlertas.Domain.Cap;

// Valores definidos por el estándar OASIS CAP 1.2 (sección 3.2).
public enum EstadoCap { Actual, Exercise, System, Test, Draft }
public enum TipoMensajeCap { Alert, Update, Cancel, Ack, Error }
public enum AlcanceCap { Public, Restricted, Private }
public enum CategoriaCap { Geo, Met, Safety, Security, Rescue, Fire, Health, Env, Transport, Infra, CBRNE, Other }
public enum UrgenciaCap { Immediate, Expected, Future, Past, Unknown }
public enum SeveridadCap { Extreme, Severe, Moderate, Minor, Unknown }
public enum CertezaCap { Observed, Likely, Possible, Unlikely, Unknown }
