using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Application.Abstracciones;

public interface IHasherContrasena
{
    string Hashear(Usuario usuario, string contrasena);
    bool Verificar(Usuario usuario, string contrasena);
}

public interface IGeneradorToken
{
    (string Token, DateTimeOffset Expira) Generar(Usuario usuario);
}
