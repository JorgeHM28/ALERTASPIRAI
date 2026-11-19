using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Usuarios;
namespace SistemaAlertas.Api.Dtos;

public sealed record SolicitudLogin(string NombreUsuario, string Contrasena);

public sealed record SolicitudCambioContrasena(string ContrasenaActual, string ContrasenaNueva);
