using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Comunidades;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Zonas;

namespace SistemaAlertas.Application.Comunidades;

public sealed class ServicioComunidades(IAppDbContext db, IRepositorioContactos contactos, IUsuarioActual usuario)
{
    private static readonly GeometryFactory Geo = new(new PrecisionModel(), Zona.Srid);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IEnumerable<ComunidadDto>> Listar(Guid? zonaId, string? buscar, CancellationToken ct)
    {
        var q = db.Comunidades.AsNoTracking().Include(c => c.Zona).Include(c => c.Contactos).AsQueryable();
        if (zonaId is { } z) q = q.Where(c => c.ZonaId == z);
        if (!string.IsNullOrWhiteSpace(buscar)) q = q.Where(c => EF.Functions.Like(EF.Functions.Collate(c.Nombre, "Latin1_General_100_CI_AS"), $"%{buscar.Trim()}%"));
        return (await q.OrderBy(c => c.Nombre).ToListAsync(ct)).Select(c => ADto(c, incluirContactos: false));
    }

    public async Task<ComunidadDto> Obtener(Guid id, CancellationToken ct)
    {
        var c = await Cargar(id, ct, seguimiento: false);
        if (c is null) throw new KeyNotFoundException("Registro no encontrado.");
        // Proxy: los contactos llegan enmascarados si el usuario no tiene permiso de verlos.
        var visibles = await contactos.ListarPorComunidadAsync(id, ct);
        return ADto(c, incluirContactos: false) with { Contactos = visibles.Select(VistaADto).ToList() };
    }


    public async Task<ComunidadDto> Crear(GuardarComunidad dto, CancellationToken ct)
    {
        ValidacionDto.Validar(dto);
        var ubicacion = await ValidarUbicacion(dto, ct);
        var comunidad = new Comunidad(dto.Nombre, dto.Municipio, dto.Poblacion, ubicacion, dto.ZonaId);
        db.Comunidades.Add(comunidad);
        await db.SaveChangesAsync(ct);
        var creada = await Cargar(comunidad.Id, ct, seguimiento: false);
        return ADto(creada!, incluirContactos: true);
    }


    public async Task<ComunidadDto> Actualizar(Guid id, GuardarComunidad dto, CancellationToken ct)
    {
        ValidacionDto.Validar(dto);
        var comunidad = await Cargar(id, ct) ?? throw new KeyNotFoundException("Comunidad no encontrada.");
        var ubicacion = await ValidarUbicacion(dto, ct);
        comunidad.Actualizar(dto.Nombre, dto.Municipio, dto.Poblacion, ubicacion, dto.ZonaId);
        await db.SaveChangesAsync(ct);
        return ADto((await Cargar(id, ct, seguimiento: false))!, incluirContactos: true);
    }


    public async Task Eliminar(Guid id, CancellationToken ct)
    {
        var comunidad = await Cargar(id, ct) ?? throw new KeyNotFoundException("Comunidad no encontrada.");
        foreach (var contacto in comunidad.Contactos) Registrar(contacto, AccionContacto.Baja, "Se eliminó la comunidad.");
        db.Comunidades.Remove(comunidad);
        await db.SaveChangesAsync(ct);
        return;
    }


    public async Task<ContactoDto> AgregarContacto(Guid id, GuardarContacto dto, CancellationToken ct)
    {
        ValidacionDto.Validar(dto);
        var comunidad = await Cargar(id, ct) ?? throw new KeyNotFoundException("Comunidad no encontrada.");
        var contacto = comunidad.AgregarContacto(Datos(dto));
        contacto.ReemplazarCanales((dto.Canales ?? []).Select(c => (c.Tipo, c.Destino, c.Preferido)));
        db.Contactos.Add(contacto);
        Registrar(contacto, AccionContacto.Alta);
        await db.SaveChangesAsync(ct);
        return ContactoADto(contacto);
    }


    public async Task<ContactoDto> ActualizarContacto(Guid contactoId, GuardarContacto dto, CancellationToken ct)
    {
        ValidacionDto.Validar(dto);
        var contacto = await db.Contactos.FirstOrDefaultAsync(c => c.Id == contactoId, ct)
            ?? throw new KeyNotFoundException("Contacto no encontrado.");
        var antes = contacto.Instantanea();
        contacto.Actualizar(Datos(dto));
        contacto.ReemplazarCanales((dto.Canales ?? []).Select(c => (c.Tipo, c.Destino, c.Preferido)));
        var cambios = Cambios(antes, contacto.Instantanea());
        if (cambios is not null) Registrar(contacto, AccionContacto.Modificacion, cambios);
        await db.SaveChangesAsync(ct);
        return ContactoADto(contacto);
    }

    /// <summary>Historial del contacto (altas, cambios y baja). Se conserva aunque el contacto se haya eliminado.</summary>

    public async Task<IEnumerable<HistorialContactoDto>> Historial(Guid contactoId, CancellationToken ct)
    {
        var registros = await contactos.HistorialAsync(contactoId, ct);
        if (registros.Count == 0 && !await db.Contactos.AnyAsync(c => c.Id == contactoId, ct))
            throw new KeyNotFoundException("Contacto no encontrado.");
        var ids = registros.Select(r => r.UsuarioId).Distinct().ToList();
        var usuarios = await db.Usuarios.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.NombreUsuario, ct);
        return registros.Select(r => new HistorialContactoDto(r.Id, r.Fecha, r.Accion, r.UsuarioId, usuarios.GetValueOrDefault(r.UsuarioId),
            r.Cambios, JsonSerializer.SerializeToElement(r.Instantanea, Json)));
    }

    /// <summary>
    /// Destinatarios disponibles para difundir una alerta: solo contactos activos, autorizados y con al menos un canal.
    /// También devuelve los contactos excluidos (con el motivo) y las comunidades que se quedarían sin canal.
    /// </summary>

    public async Task<DestinatariosDto> Destinatarios(Guid[]? zonaIds, CancellationToken ct)
    {
        var q = db.Comunidades.AsNoTracking().Include(c => c.Zona).Include(c => c.Contactos).AsQueryable();
        if (zonaIds is { Length: > 0 }) q = q.Where(c => zonaIds.Contains(c.ZonaId));
        var comunidades = await q.OrderBy(c => c.Nombre).ToListAsync(ct);

        // Proxy: los canales salen del repositorio protegido, enmascarados si el usuario no tiene permiso.
        var canales = (await contactos.ListarHabilitadosAsync(zonaIds, ct)).ToDictionary(x => x.Id, x => x.Canales);
        var destinatarios = comunidades.SelectMany(c => c.Contactos.Where(x => x.Habilitado && canales.ContainsKey(x.Id)).OrderBy(x => x.Nombre)
            .Select(x => new DestinatarioDto(x.Id, x.Nombre, x.Cargo, c.Id, c.Nombre, c.ZonaId, c.Zona?.Nombre,
                canales[x.Id].Select(k => new CanalDto(k.Tipo, k.Destino, k.Preferido)).ToList()))).ToList();
        var excluidos = comunidades.SelectMany(c => c.Contactos.Where(x => !x.Habilitado).OrderBy(x => x.Nombre)
            .Select(x => new ContactoExcluidoDto(x.Id, x.Nombre, c.Nombre, x.MotivosNoHabilitado))).ToList();
        var sinCanal = comunidades.Where(c => !c.Contactos.Any(x => x.Habilitado))
            .Select(c => new ComunidadSinCanalDto(c.Id, c.Nombre, c.ZonaId, c.Zona?.Nombre, c.Contactos.Count,
                c.Contactos.Count == 0 ? "No tiene contactos registrados." : "Ningún contacto está activo, autorizado y con canal."))
            .ToList();
        return new DestinatariosDto(destinatarios, excluidos, sinCanal);
    }


    public async Task EliminarContacto(Guid contactoId, CancellationToken ct)
    {
        var contacto = await db.Contactos.FirstOrDefaultAsync(c => c.Id == contactoId, ct)
            ?? throw new KeyNotFoundException("Contacto no encontrado.");
        Registrar(contacto, AccionContacto.Baja);
        db.Contactos.Remove(contacto);
        await db.SaveChangesAsync(ct);
        return;
    }

    public async Task<ContactoDto> ObtenerContacto(Guid id, CancellationToken ct)
    {
        var comunidadId = await db.Contactos.Where(c => c.Id == id).Select(c => (Guid?)c.ComunidadId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Contacto no encontrado.");
        return VistaADto((await contactos.ListarPorComunidadAsync(comunidadId, ct)).Single(c => c.Id == id));
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

    private void Registrar(Contacto contacto, AccionContacto accion, string? cambios = null) =>
        db.HistorialContactos.Add(new HistorialContacto(contacto, accion, usuario.Id,
            JsonSerializer.Serialize(contacto.Instantanea(), Json), cambios));

    private static DatosContacto Datos(GuardarContacto d) => new(d.Nombre, d.Cargo, d.Activo, d.Procedencia, d.Autorizado,
        d.ReferenciaAutorizacion, d.FechaAutorizacion);

    /// <summary>Lista legible de lo que cambió, o null si no cambió nada.</summary>
    private static string? Cambios(InstantaneaContacto a, InstantaneaContacto b)
    {
        var c = new List<string>();
        if (a.Nombre != b.Nombre) c.Add("nombre");
        if (a.Cargo != b.Cargo) c.Add("cargo");
        if (a.Activo != b.Activo) c.Add(b.Activo ? "activado" : "desactivado");
        if (a.Procedencia != b.Procedencia) c.Add("procedencia");
        if (a.Autorizado != b.Autorizado) c.Add(b.Autorizado ? "autorizado" : "autorización retirada");
        if (a.ReferenciaAutorizacion != b.ReferenciaAutorizacion || a.FechaAutorizacion != b.FechaAutorizacion) c.Add("referencia de autorización");
        if (!a.Canales.SequenceEqual(b.Canales)) c.Add("canales");
        return c.Count == 0 ? null : string.Join(", ", c);
    }

    private static ComunidadDto ADto(Comunidad c, bool incluirContactos)
    {
        var habilitados = c.Contactos.Count(x => x.Habilitado);
        return new(c.Id, c.Nombre, c.Municipio, c.Poblacion, c.Ubicacion?.Y, c.Ubicacion?.X, c.ZonaId, c.Zona?.Nombre,
            c.Contactos.Count, habilitados, habilitados == 0,
            incluirContactos ? c.Contactos.OrderBy(x => x.Nombre).Select(ContactoADto).ToList() : null);
    }

    private static ContactoDto VistaADto(ContactoVista c) => new(c.Id, c.ComunidadId, c.Nombre, c.Cargo, c.Activo,
        c.Canales.Select(x => new CanalDto(x.Tipo, x.Destino, x.Preferido)).ToList(),
        c.Procedencia, c.Autorizado, c.ReferenciaAutorizacion, c.FechaAutorizacion, c.Habilitado, c.MotivosNoHabilitado, c.DatosOcultos);

    private static ContactoDto ContactoADto(Contacto c) => new(c.Id, c.ComunidadId, c.Nombre, c.Cargo, c.Activo,
        c.Canales.Select(x => new CanalDto(x.Tipo, x.Destino, x.Preferido)).ToList(),
        c.Procedencia, c.Autorizado, c.ReferenciaAutorizacion, c.FechaAutorizacion, c.Habilitado, c.MotivosNoHabilitado);
}
