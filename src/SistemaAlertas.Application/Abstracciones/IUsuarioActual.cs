using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Application.Abstracciones;

/// <summary>Quién hace la petición. Sin sesión (web pública) no está autenticado y no tiene ningún permiso.</summary>
public interface IUsuarioActual
{
    Guid Id => Guid.Empty;
    bool Autenticado { get; }
    bool Tiene(Permiso permiso);
}
