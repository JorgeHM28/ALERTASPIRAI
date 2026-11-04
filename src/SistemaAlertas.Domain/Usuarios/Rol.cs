namespace SistemaAlertas.Domain.Usuarios;

public enum Rol
{
    /// <summary>Gestiona usuarios y toda la configuración.</summary>
    Administrador,
    /// <summary>Registra avisos y mantiene zonas, comunidades y contactos.</summary>
    Operador,
    /// <summary>Revisa y aprueba mensajes antes de difundirlos (flujo en Sprint 2).</summary>
    Aprobador,
    /// <summary>Solo lectura.</summary>
    Consulta
}
