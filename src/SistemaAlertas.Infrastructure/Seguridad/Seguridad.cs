using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Usuarios;

namespace SistemaAlertas.Infrastructure.Seguridad;

/// <summary>Nombres cortos de los claims del token (estándar JWT/OIDC).</summary>
public static class NombresClaim
{
    public const string Id = "sub";
    public const string Usuario = "preferred_username";
    public const string Nombre = "name";
    public const string Rol = "role";
}

public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";
    public string Emisor { get; set; } = "SistemaAlertas";
    public string Audiencia { get; set; } = "SistemaAlertas.Web";
    /// <summary>Clave HMAC de al menos 32 caracteres. En producción va en una variable de entorno, nunca en el repo.</summary>
    public string Clave { get; set; } = string.Empty;
    public int MinutosValidez { get; set; } = 480;
}

/// <summary>Hash PBKDF2 con sal, el mismo algoritmo de ASP.NET Core Identity.</summary>
public sealed class HasherContrasena : IHasherContrasena
{
    private readonly PasswordHasher<Usuario> _hasher = new();

    public string Hashear(Usuario usuario, string contrasena) => _hasher.HashPassword(usuario, contrasena);

    public bool Verificar(Usuario usuario, string contrasena) =>
        _hasher.VerifyHashedPassword(usuario, usuario.HashContrasena, contrasena) != PasswordVerificationResult.Failed;
}

public sealed class GeneradorTokenJwt(OpcionesJwt opciones) : IGeneradorToken
{
    public (string Token, DateTimeOffset Expira) Generar(Usuario usuario)
    {
        var expira = DateTimeOffset.UtcNow.AddMinutes(opciones.MinutosValidez);
        // Un claim "role" por cada rol: el usuario puede tener varios.
        var claims = new List<Claim>
        {
            new(NombresClaim.Id, usuario.Id.ToString()),
            new(NombresClaim.Usuario, usuario.NombreUsuario),
            new(NombresClaim.Nombre, usuario.NombreCompleto)
        };
        claims.AddRange(usuario.Roles.Select(r => new Claim(NombresClaim.Rol, r.ToString())));
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.Clave)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(opciones.Emisor, opciones.Audiencia, claims,
            expires: expira.UtcDateTime, signingCredentials: credenciales);
        var handler = new JwtSecurityTokenHandler();
        handler.OutboundClaimTypeMap.Clear();
        return (handler.WriteToken(token), expira);
    }
}
