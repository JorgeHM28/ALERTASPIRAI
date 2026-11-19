using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Usuarios;
using SistemaAlertas.Domain.Usuarios;
namespace SistemaAlertas.Api.Dtos;

public sealed record SolicitudRestablecerContrasena(string ContrasenaNueva);

public sealed record RolDto(Rol Rol, IReadOnlyList<Permiso> Permisos);
