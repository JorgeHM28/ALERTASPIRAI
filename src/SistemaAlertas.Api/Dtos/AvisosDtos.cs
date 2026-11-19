using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Application.Avisos;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Application.Avisos.Importacion;
using SistemaAlertas.Domain.Avisos;
namespace SistemaAlertas.Api.Dtos;

public sealed record AvisoDetalleDto(AvisoResumen Resumen, string? Descripcion, string? Instruccion, DateTimeOffset? Vigente,
    JsonElement? Area, string XmlCap, string? ContenidoOriginal, string? TipoContenidoOriginal, string? HashOriginal);
