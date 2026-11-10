using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaAlertas.Application.Abstracciones;
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

/// <summary>Casos de uso de RF-01: registrar un aviso a mano o importarlo desde un mensaje CAP.</summary>
public sealed class ServicioAvisos(IAppDbContext db, IModuloCap moduloCap, OpcionesCap opciones)
{
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
        return aviso;
    }

    /// <summary>
    /// Marca como vencidos los avisos cuya expiración ya pasó (y evalúa los que vienen de antes de esta regla) y deja registrado que no son aptos para difusión.
    /// Devuelve cuántos cambiaron.
    /// </summary>
    public async Task<int> RevisarVigenciaAsync(CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        var sinEvaluar = Aviso.SinEvaluar;
        var vencidos = await db.Avisos
            .Where(a => (!a.Vencido && a.Expira != null && a.Expira <= ahora) || a.AptitudEvaluadaEn <= sinEvaluar)
            .ToListAsync(ct);
        var cambiados = vencidos.Count(a => a.EvaluarAptitud(ahora));
        if (cambiados > 0) await db.SaveChangesAsync(ct);
        return cambiados;
    }

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
