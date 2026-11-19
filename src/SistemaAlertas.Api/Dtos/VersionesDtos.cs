using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Mensajes;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Mensajes;
namespace SistemaAlertas.Api.Dtos;

public sealed record VersionDetalleDto(VersionDetalle Detalle, JsonElement? Area, string TextoSms, string TextoLectura);
