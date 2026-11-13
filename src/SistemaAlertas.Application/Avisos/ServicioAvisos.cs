using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;

namespace SistemaAlertas.Application.Avisos;

public sealed class OpcionesCap
{
    public const string Seccion = "Cap";
    /// <summary>Valor de &lt;sender&gt; para los avisos generados por el sistema.</summary>
    public string Remitente { get; set; } = "sat-pirai@santacruz.gob.bo";
    public string NombreRemitente { get; set; } = "SAT Río Piraí";
    /// <summary>Desfase horario de los mensajes generados (Bolivia: UTC-4).</summary>
    public int DesfaseHorasUtc { get; set; } = -4;
}

/// <summary>
/// Casos de uso de RF-01 (registrar o importar un aviso) y RF-11 (aprobar o descartar).
/// Cada cambio se publica como evento (Observer): la bitácora y las demás reacciones no se programan aquí.
/// </summary>
public sealed class ServicioAvisos(IAppDbContext db, IModuloCap moduloCap, OpcionesCap opciones,
    IPublicadorEventosAvisos eventos, TimeProvider? reloj = null)
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<Aviso> ImportarAsync(string contenido, FormatoAviso? formato, Guid usuarioId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contenido)) throw new ExcepcionDominio("El contenido del aviso está vacío.");

        // Facade: el módulo CAP elige el importador (Factory Method) y traduce el contenido (Adapter).
        var (mensaje, formatoLeido) = moduloCap.Importar(contenido, formato);

        if (await db.Avisos.AnyAsync(a => a.IdentificadorCap == mensaje.Identifier && a.Remitente == mensaje.Sender, ct))
            throw new ExcepcionDominio($"El aviso {mensaje.Identifier} de {mensaje.Sender} ya fue registrado.");

        // Se guarda el XML CAP normalizado, aunque haya llegado en JSON.
        var xml = formatoLeido == FormatoAviso.CapXml ? contenido : moduloCap.Generar(mensaje);
        var area = AreaDe(mensaje);

        // El original se guarda tal cual llegó (XML oficial o JSON exacto), con su hash SHA-256.
        var aviso = new Aviso(mensaje, formatoLeido, xml, area, usuarioId, contenidoOriginal: contenido);
        db.Avisos.Add(aviso);
        await db.SaveChangesAsync(ct);
        await Publicar(aviso, TipoEventoAviso.Registrado, usuarioId, $"Importado desde {formatoLeido}.", ct);
        return aviso;
    }

    public async Task<Aviso> RegistrarManualAsync(RegistrarAvisoManual dto, Guid usuarioId, CancellationToken ct)
    {
        var zonas = dto.ZonaIds is { Count: > 0 }
            ? await db.Zonas.Where(z => dto.ZonaIds.Contains(z.Id)).ToListAsync(ct)
            : [];
        if (dto.ZonaIds is { Count: > 0 } && zonas.Count != dto.ZonaIds.Distinct().Count())
            throw new ExcepcionDominio("Alguna de las zonas indicadas no existe.");

        // Builder: arma el mensaje CAP con validación de todas las reglas del estándar.
        var mensaje = new MensajeCapBuilder()
            .DeRemitente(opciones.Remitente)
            .EnviadoEn(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(opciones.DesfaseHorasUtc)))
            .ConTipo(TipoMensajeCap.Alert)
            .ConEstado(EstadoCap.Actual)
            .ConAlcance(AlcanceCap.Public)
            .AgregarInfo(i =>
            {
                i.ConCategoria(CategoriaCap.Met)
                 .ConEvento(dto.Evento)
                 .ConUrgencia(dto.Urgencia)
                 .ConSeveridad(dto.Severidad)
                 .ConCerteza(dto.Certeza)
                 .ConNombreRemitente(opciones.NombreRemitente)
                 .ConTitular(dto.Titular)
                 .ConDescripcion(dto.Descripcion)
                 .ConInstruccion(dto.Instruccion)
                 .VigenteDesde(dto.Vigente)
                 .ExpiraEn(dto.Expira);
                foreach (var zona in zonas)
                    i.AgregarArea(zona.Nombre, PoligonosDe(zona.Area).ToArray());
            })
            .Build();

        Geometry? area = zonas.Count switch
        {
            0 => null,
            1 => zonas[0].Area.Copy(),
            _ => zonas[0].Area.Factory.BuildGeometry(zonas.Select(z => z.Area)).Union()
        };

        var aviso = new Aviso(mensaje, FormatoAviso.Manual, moduloCap.Generar(mensaje), area, usuarioId);
        db.Avisos.Add(aviso);
        await db.SaveChangesAsync(ct);
        await Publicar(aviso, TipoEventoAviso.Registrado, usuarioId, "Registrado a mano.", ct);
        return aviso;
    }

    /// <summary>RF-11: el aprobador da el visto bueno. El estado actual (State) decide si se puede.</summary>
    public async Task<Aviso> AprobarAsync(Guid avisoId, Guid usuarioId, CancellationToken ct)
    {
        var aviso = await Buscar(avisoId, ct);
        aviso.EvaluarAptitud(_reloj.GetUtcNow());
        aviso.Aprobar();
        await db.SaveChangesAsync(ct);
        await Publicar(aviso, TipoEventoAviso.Aprobado, usuarioId, aviso.EsPrueba ? "Aprobado como aviso de prueba." : "Aprobado para difusión.", ct);
        return aviso;
    }

    public async Task<Aviso> DescartarAsync(Guid avisoId, Guid usuarioId, CancellationToken ct)
    {
        var aviso = await Buscar(avisoId, ct);
        var anterior = aviso.Estado;
        aviso.Descartar();
        await db.SaveChangesAsync(ct);
        await Publicar(aviso, TipoEventoAviso.Descartado, usuarioId, $"Descartado (estaba {anterior}).", ct);
        return aviso;
    }

    /// <summary>
    /// Marca como vencidos los avisos cuya expiración ya pasó (y evalúa los que vienen de antes de esta regla) y deja registrado que no son aptos para difusión.
    /// Devuelve cuántos cambiaron.
    /// </summary>
    public async Task<int> RevisarVigenciaAsync(CancellationToken ct)
    {
        var ahora = _reloj.GetUtcNow();
        var sinEvaluar = Aviso.SinEvaluar;
        var vencidos = await db.Avisos
            .Where(a => (!a.Vencido && a.Expira != null && a.Expira <= ahora) || a.AptitudEvaluadaEn <= sinEvaluar)
            .ToListAsync(ct);
        var yaVencidos = vencidos.Where(a => a.Vencido).Select(a => a.Id).ToHashSet();
        var cambiados = vencidos.Count(a => a.EvaluarAptitud(ahora));
        if (cambiados == 0) return 0;
        await db.SaveChangesAsync(ct);
        foreach (var a in vencidos.Where(a => a.Vencido && !yaVencidos.Contains(a.Id)))
            await Publicar(a, TipoEventoAviso.Vencido, null, $"Expiró el {a.Expira:yyyy-MM-dd HH:mm} UTC.", ct);
        return cambiados;
    }

    private async Task<Aviso> Buscar(Guid avisoId, CancellationToken ct) =>
        await db.Avisos.FirstOrDefaultAsync(a => a.Id == avisoId, ct) ?? throw new KeyNotFoundException("Aviso no encontrado.");

    private Task Publicar(Aviso aviso, TipoEventoAviso tipo, Guid? usuarioId, string detalle, CancellationToken ct) =>
        eventos.PublicarAsync(new EventoAviso(aviso.Id, tipo, usuarioId, detalle, _reloj.GetUtcNow()), ct);

    private static Geometry? AreaDe(MensajeCap mensaje)
    {
        var poligonos = mensaje.Infos.SelectMany(i => i.Areas).SelectMany(a => a.Polygons).ToList();
        return PoligonoCap.UnirAreas(poligonos);
    }

    private static IEnumerable<string> PoligonosDe(Geometry geometria) => geometria switch
    {
        Polygon p => [PoligonoCap.DesdeGeometria(p)],
        MultiPolygon mp => mp.Geometries.Cast<Polygon>().Select(PoligonoCap.DesdeGeometria),
        _ => []
    };
}
