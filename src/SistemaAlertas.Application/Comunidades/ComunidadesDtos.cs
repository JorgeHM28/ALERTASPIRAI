using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Comunidades;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Application.Comunidades;

public sealed record GuardarComunidad([Required, StringLength(150)] string Nombre, [StringLength(150)] string? Municipio, [Range(0, int.MaxValue)] int? Poblacion, [Range(-90d, 90d)] double? Latitud, [Range(-180d, 180d)] double? Longitud, Guid ZonaId);
public sealed record CanalDto(TipoCanal Tipo, string Destino, bool Preferido);
public sealed record GuardarContacto([Required, StringLength(150)] string Nombre, [StringLength(100)] string? Cargo, bool Activo, IReadOnlyList<CanalDto> Canales,
    string Procedencia, bool Autorizado, string? ReferenciaAutorizacion, DateOnly? FechaAutorizacion);
public sealed record ContactoDto(Guid Id, Guid ComunidadId, string Nombre, string? Cargo, bool Activo, IReadOnlyList<CanalDto> Canales,
    string Procedencia, bool Autorizado, string? ReferenciaAutorizacion, DateOnly? FechaAutorizacion,
    bool Habilitado, IReadOnlyList<string> MotivosNoHabilitado, bool DatosOcultos = false);
/// <param name="ContactosHabilitados">Contactos activos, autorizados y con canal: los únicos que reciben alertas.</param>
/// <param name="SinCanal">La comunidad no tiene ningún contacto habilitado: no recibirá alertas hasta que se corrija.</param>
public sealed record ComunidadDto(Guid Id, string Nombre, string? Municipio, int? Poblacion, double? Latitud, double? Longitud,
    Guid ZonaId, string? Zona, int TotalContactos, int ContactosHabilitados, bool SinCanal, IReadOnlyList<ContactoDto>? Contactos);
public sealed record HistorialContactoDto(Guid Id, DateTimeOffset Fecha, AccionContacto Accion, Guid UsuarioId, string? Usuario,
    string? Cambios, JsonElement Instantanea);
public sealed record DestinatarioDto(Guid ContactoId, string Nombre, string? Cargo, Guid ComunidadId, string Comunidad,
    Guid ZonaId, string? Zona, IReadOnlyList<CanalDto> Canales);
public sealed record ContactoExcluidoDto(Guid ContactoId, string Nombre, string Comunidad, IReadOnlyList<string> Motivos);
public sealed record ComunidadSinCanalDto(Guid ComunidadId, string Nombre, Guid ZonaId, string? Zona, int TotalContactos, string Motivo);
public sealed record DestinatariosDto(IReadOnlyList<DestinatarioDto> Destinatarios, IReadOnlyList<ContactoExcluidoDto> Excluidos,
    IReadOnlyList<ComunidadSinCanalDto> ComunidadesSinCanal);

/// <summary>RF-12: comunidades, sus contactos y los canales para alcanzarlos.</summary>



/// <remarks>
/// Las lecturas de contactos pasan por <see cref="IRepositorioContactos"/>, que en ejecución es el Proxy de protección:
/// solo quien tiene permiso de edición ve los teléfonos completos.
/// </remarks>
