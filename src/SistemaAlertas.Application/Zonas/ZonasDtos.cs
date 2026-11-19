using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Application.Zonas;

/// <param name="FuenteInstitucional">Obligatoria al crear. Al actualizar, si no se envían los tres datos institucionales, se conservan los anteriores.</param>
public sealed record GuardarZona([Required, StringLength(150)] string Nombre, [StringLength(1000)] string? Descripcion, NivelRiesgo NivelRiesgo, JsonElement Geometria,
    string? FuenteInstitucional, string? ReferenciaDocumento, DateOnly? FechaDocumento);
public sealed record ZonaDto(Guid Id, string Nombre, string? Descripcion, NivelRiesgo NivelRiesgo, double AreaKm2,
    int Comunidades, JsonElement Geometria, string? FuenteInstitucional, string? ReferenciaDocumento, DateOnly? FechaDocumento);
public sealed record ResultadoImportacionZonas(int Creadas, int Actualizadas, IReadOnlyList<string> Errores);

/// <summary>RF-09: zonas de riesgo en GeoJSON.</summary>



