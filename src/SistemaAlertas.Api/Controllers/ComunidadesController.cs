using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Api.Controllers;

public sealed record GuardarComunidad(string Nombre, string? Municipio, int? Poblacion, double? Latitud, double? Longitud, Guid ZonaId);
public sealed record CanalDto(TipoCanal Tipo, string Destino, bool Preferido);
public sealed record GuardarContacto(string Nombre, string? Cargo, bool Activo, IReadOnlyList<CanalDto> Canales);
public sealed record ContactoDto(Guid Id, Guid ComunidadId, string Nombre, string? Cargo, bool Activo, IReadOnlyList<CanalDto> Canales);
public sealed record ComunidadDto(Guid Id, string Nombre, string? Municipio, int? Poblacion, double? Latitud, double? Longitud,
    Guid ZonaId, string? Zona, int TotalContactos, IReadOnlyList<ContactoDto>? Contactos);

/// <summary>RF-12: comunidades, sus contactos y los canales para alcanzarlos.</summary>
[ApiController]
[Route("api/comunidades")]
[Authorize(Politicas.Lectura)]
public sealed class ComunidadesController(IAppDbContext db) : ControllerBase
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), Zona.Srid);

    [HttpGet]
    public async Task<IEnumerable<ComunidadDto>> Listar([FromQuery] Guid? zonaId, [FromQuery] string? buscar, CancellationToken ct)
    {
        var q = db.Comunidades.AsNoTracking().Include(c => c.Zona).Include(c => c.Contactos).AsQueryable();
        if (zonaId is { } z) q = q.Where(c => c.ZonaId == z);
        if (!string.IsNullOrWhiteSpace(buscar)) q = q.Where(c => EF.Functions.ILike(c.Nombre, $"%{buscar.Trim()}%"));
        return (await q.OrderBy(c => c.Nombre).ToListAsync(ct)).Select(c => ADto(c, incluirContactos: false));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ComunidadDto>> Obtener(Guid id, CancellationToken ct)
    {
        var c = await Cargar(id, ct, seguimiento: false);
        return c is null ? NotFound() : ADto(c, incluirContactos: true);
    }

    [HttpPost]
    [Authorize(Politicas.Edicion)]
    public async Task<ActionResult<ComunidadDto>> Crear(GuardarComunidad dto, CancellationToken ct)
    {
        var ubicacion = await ValidarUbicacion(dto, ct);
        var comunidad = new Comunidad(dto.Nombre, dto.Municipio, dto.Poblacion, ubicacion, dto.ZonaId);
        db.Comunidades.Add(comunidad);
        await db.SaveChangesAsync(ct);
        var creada = await Cargar(comunidad.Id, ct, seguimiento: false);
        return CreatedAtAction(nameof(Obtener), new { id = comunidad.Id }, ADto(creada!, incluirContactos: true));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Politicas.Edicion)]
    public async Task<ComunidadDto> Actualizar(Guid id, GuardarComunidad dto, CancellationToken ct)
    {
        var comunidad = await Cargar(id, ct) ?? throw new KeyNotFoundException("Comunidad no encontrada.");
        var ubicacion = await ValidarUbicacion(dto, ct);
        comunidad.Actualizar(dto.Nombre, dto.Municipio, dto.Poblacion, ubicacion, dto.ZonaId);
        await db.SaveChangesAsync(ct);
        return ADto((await Cargar(id, ct, seguimiento: false))!, incluirContactos: true);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Politicas.Edicion)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        var comunidad = await db.Comunidades.FindAsync([id], ct) ?? throw new KeyNotFoundException("Comunidad no encontrada.");
        db.Comunidades.Remove(comunidad);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/contactos")]
    [Authorize(Politicas.Edicion)]
    public async Task<ActionResult<ContactoDto>> AgregarContacto(Guid id, GuardarContacto dto, CancellationToken ct)
    {
        var comunidad = await Cargar(id, ct) ?? throw new KeyNotFoundException("Comunidad no encontrada.");
        var contacto = comunidad.AgregarContacto(dto.Nombre, dto.Cargo);
        contacto.Actualizar(dto.Nombre, dto.Cargo, dto.Activo);
        contacto.ReemplazarCanales((dto.Canales ?? []).Select(c => (c.Tipo, c.Destino, c.Preferido)));
        db.Contactos.Add(contacto);
        await db.SaveChangesAsync(ct);
        return Created($"/api/contactos/{contacto.Id}", ContactoADto(contacto));
    }

    [HttpPut("/api/contactos/{contactoId:guid}")]
    [Authorize(Politicas.Edicion)]
    public async Task<ContactoDto> ActualizarContacto(Guid contactoId, GuardarContacto dto, CancellationToken ct)
    {
        var contacto = await db.Contactos.FirstOrDefaultAsync(c => c.Id == contactoId, ct)
            ?? throw new KeyNotFoundException("Contacto no encontrado.");
        contacto.Actualizar(dto.Nombre, dto.Cargo, dto.Activo);
        contacto.ReemplazarCanales((dto.Canales ?? []).Select(c => (c.Tipo, c.Destino, c.Preferido)));
        await db.SaveChangesAsync(ct);
        return ContactoADto(contacto);
    }

    [HttpDelete("/api/contactos/{contactoId:guid}")]
    [Authorize(Politicas.Edicion)]
    public async Task<IActionResult> EliminarContacto(Guid contactoId, CancellationToken ct)
    {
        var contacto = await db.Contactos.FindAsync([contactoId], ct) ?? throw new KeyNotFoundException("Contacto no encontrado.");
        db.Contactos.Remove(contacto);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<Comunidad?> Cargar(Guid id, CancellationToken ct, bool seguimiento = true)
    {
        var q = db.Comunidades.Include(c => c.Zona).Include(c => c.Contactos);
        return (seguimiento ? q : q.AsNoTracking()).FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    private async Task<Point?> ValidarUbicacion(GuardarComunidad dto, CancellationToken ct)
    {
        var zona = await db.Zonas.AsNoTracking().FirstOrDefaultAsync(z => z.Id == dto.ZonaId, ct)
            ?? throw new ExcepcionDominio("La zona indicada no existe.");
        if (dto.Latitud is null && dto.Longitud is null) return null;
        if (dto.Latitud is not { } lat || dto.Longitud is not { } lon)
            throw new ExcepcionDominio("Indica latitud y longitud juntas.");
        if (lat is < -90 or > 90 || lon is < -180 or > 180) throw new ExcepcionDominio("Coordenadas fuera de rango.");
        var punto = Geo.CreatePoint(new Coordinate(lon, lat));
        if (!zona.Area.Covers(punto))
            throw new ExcepcionDominio($"La ubicación está fuera de la zona '{zona.Nombre}'.");
        return punto;
    }

    private static ComunidadDto ADto(Comunidad c, bool incluirContactos) => new(
        c.Id, c.Nombre, c.Municipio, c.Poblacion, c.Ubicacion?.Y, c.Ubicacion?.X, c.ZonaId, c.Zona?.Nombre,
        c.Contactos.Count, incluirContactos ? c.Contactos.OrderBy(x => x.Nombre).Select(ContactoADto).ToList() : null);

    private static ContactoDto ContactoADto(Contacto c) => new(c.Id, c.ComunidadId, c.Nombre, c.Cargo, c.Activo,
        c.Canales.Select(x => new CanalDto(x.Tipo, x.Destino, x.Preferido)).ToList());
}
