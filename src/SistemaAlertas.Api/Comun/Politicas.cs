using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Api.Comun;

/// <summary>Matriz de permisos de RF-13. Cada endpoint declara una de estas políticas.</summary>
public static class Politicas
{
    /// <summary>Ver datos: todos los roles.</summary>
    public const string Lectura = nameof(Lectura);
    /// <summary>Crear y modificar zonas, comunidades, contactos y avisos: Administrador y Operador.</summary>
    public const string Edicion = nameof(Edicion);
    /// <summary>Gestionar usuarios y roles: solo Administrador.</summary>
    public const string Administracion = nameof(Administracion);

    public static readonly IReadOnlyDictionary<string, Rol[]> Matriz = new Dictionary<string, Rol[]>
    {
        [Lectura] = [Rol.Administrador, Rol.Operador, Rol.Aprobador, Rol.Consulta],
        [Edicion] = [Rol.Administrador, Rol.Operador],
        [Administracion] = [Rol.Administrador]
    };
}
