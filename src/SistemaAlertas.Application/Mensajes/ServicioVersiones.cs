using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Avisos;
using SistemaAlertas.Application.Avisos.Eventos;
using SistemaAlertas.Application.Cap;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Mensajes;

namespace SistemaAlertas.Application.Mensajes;

/// <summary>
/// RF-02 (preparar y corregir versiones Alert, Update y Cancel) y RF-11 (aprobar o rechazar con motivo).
/// Las reglas de cada paso las decide el estado de la versión (State); aquí se cargan los datos, se genera y valida
/// el XML CAP 1.2 y se publican los eventos (Observer).
/// </summary>
public sealed class ServicioVersiones(IAppDbContext db, IModuloCap moduloCap, OpcionesCap opciones,
    IPublicadorEventosAvisos eventos, TimeProvider? reloj = null)
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    /// <summary>
    /// Prepara la versión que corresponde a un aviso oficial: Alert si es la primera de su cadena, Update si ya hay una
    /// aprobada (también cuando llega un Update del SENAMHI) y Cancel si el aviso oficial es un Cancel.
    /// Los hechos, el nivel y el ámbito se copian del aviso; la redacción que no se indique también.
    /// </summary>
    public async Task<VersionMensaje> PrepararDesdeAvisoAsync(Guid avisoId, RedaccionVersion redaccion, Guid usuarioId, CancellationToken ct)
    {
        var aviso = await db.Avisos.FirstOrDefaultAsync(a => a.Id == avisoId, ct) ?? throw new KeyNotFoundException("Aviso no encontrado.");
        var ahora = _reloj.GetUtcNow();
        var motivos = aviso.MotivosNoPreparable(ahora);
        if (motivos.Count > 0)
            throw new ExcepcionDominio("No se puede preparar un mensaje desde este aviso: " + string.Join(" ", motivos));

        var cadenaId = aviso.CadenaId!.Value;
        await ExigirSinVersionEnCursoAsync(cadenaId, ct);
        var vigente = await VigenteDeCadenaAsync(cadenaId, ct);

        TipoMensajeCap tipo;
        if (aviso.TipoMensaje == TipoMensajeCap.Cancel)
            tipo = vigente is null
                ? throw new ExcepcionDominio("El aviso oficial cancela un evento que no tiene ningún mensaje aprobado: no hay nada que cancelar.")
                : TipoMensajeCap.Cancel;
        else
            tipo = vigente is null ? TipoMensajeCap.Alert : TipoMensajeCap.Update;

        var contenido = new ContenidoMensaje(aviso.Evento, aviso.Urgencia, aviso.Severidad, aviso.Certeza,
                aviso.Titular, aviso.Descripcion, aviso.Instruccion,
                aviso.Vigente, aviso.Expira ?? vigente?.Contenido.Expira, aviso.DescripcionArea ?? vigente?.Contenido.DescripcionArea)
            .Redactar(redaccion.Titular ?? aviso.Titular, redaccion.Descripcion ?? aviso.Descripcion,
                redaccion.Instruccion ?? aviso.Instruccion);

        var version = new VersionMensaje(aviso.Id, cadenaId, await SiguienteNumeroAsync(cadenaId, ct), tipo, contenido,
            aviso.Area?.Copy() ?? vigente?.Area?.Copy(), aviso.EstadoCap, opciones.Remitente, usuarioId, vigente);
        db.VersionesMensaje.Add(version);
        await db.SaveChangesAsync(ct);
        await Publicar(version, TipoEventoAviso.VersionPreparada, usuarioId,
            $"Versión {version.Numero} ({tipo}) preparada desde el aviso {aviso.IdentificadorCap}.", ct);
        return version;
    }

    /// <summary>
    /// Corrige una versión aprobada o difundida: prepara un Update (o un Cancel) que la referencia.
    /// Clona el contenido aprobado (Prototype) y cambia solo la redacción indicada.
    /// </summary>
    public async Task<VersionMensaje> DerivarAsync(Guid referenciaVersionId, TipoMensajeCap tipo, RedaccionVersion redaccion,
        Guid usuarioId, CancellationToken ct)
    {
        if (tipo is not (TipoMensajeCap.Update or TipoMensajeCap.Cancel))
            throw new ExcepcionDominio("Desde una versión aprobada solo se prepara un Update o un Cancel.");
        var referencia = await db.VersionesMensaje.FirstOrDefaultAsync(v => v.Id == referenciaVersionId, ct)
            ?? throw new ExcepcionDominio("Referencia inexistente: la versión referenciada no existe.");
        var aviso = await db.Avisos.FirstAsync(a => a.Id == referencia.AvisoId, ct);
        if (aviso.Estado == EstadoAviso.Descartado) throw new ExcepcionDominio("El aviso fue descartado.");
        await ExigirSinVersionEnCursoAsync(referencia.CadenaId, ct);

        var contenido = referencia.Contenido.Redactar(redaccion.Titular ?? referencia.Contenido.Titular,
            redaccion.Descripcion ?? referencia.Contenido.Descripcion, redaccion.Instruccion ?? referencia.Contenido.Instruccion);
        var version = new VersionMensaje(referencia.AvisoId, referencia.CadenaId, await SiguienteNumeroAsync(referencia.CadenaId, ct),
            tipo, contenido, referencia.Area?.Copy(), referencia.EstadoCap, opciones.Remitente, usuarioId, referencia);
        db.VersionesMensaje.Add(version);
        await db.SaveChangesAsync(ct);
        await Publicar(version, TipoEventoAviso.VersionPreparada, usuarioId,
            $"Versión {version.Numero} ({tipo}) preparada sobre la versión {referencia.Numero}.", ct);
        return version;
    }

    /// <summary>
    /// Corrige un borrador: solo cambia la redacción (el estado decide si se puede editar).
    /// Un campo en null se conserva; un texto vacío lo borra.
    /// </summary>
    public async Task<VersionMensaje> RedactarAsync(Guid versionId, RedaccionVersion redaccion, Guid usuarioId, CancellationToken ct)
    {
        var version = await Buscar(versionId, ct);
        var c = version.Contenido;
        version.Redactar(redaccion.Titular ?? c.Titular, redaccion.Descripcion ?? c.Descripcion, redaccion.Instruccion ?? c.Instruccion);
        await db.SaveChangesAsync(ct);
        return version;
    }

    /// <summary>
    /// RF-05: vincula el borrador a un protocolo oficial (sus instrucciones pasan a ser las del protocolo)
    /// o, con null, quita el vínculo.
    /// </summary>
    public async Task<VersionMensaje> VincularProtocoloAsync(Guid versionId, Guid? protocoloId, CancellationToken ct)
    {
        var version = await Buscar(versionId, ct);
        if (protocoloId is { } id)
        {
            var protocolo = await db.Protocolos.FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new ExcepcionDominio("El protocolo indicado no existe.");
            version.VincularProtocolo(protocolo);
        }
        else version.QuitarProtocolo();
        await db.SaveChangesAsync(ct);
        return version;
    }

    /// <summary>
    /// Genera el XML CAP 1.2 de la versión y lo valida. Una versión incompleta, o cuyo XML no es válido, queda como borrador.
    /// </summary>
    public async Task<VersionMensaje> EnviarARevisionAsync(Guid versionId, Guid usuarioId, CancellationToken ct)
    {
        var version = await Buscar(versionId, ct);
        var ahora = _reloj.GetUtcNow();
        var faltan = version.Faltantes(ahora);
        if (version.Estado == EstadoVersion.Borrador && faltan.Count > 0)
            throw new ExcepcionDominio("La versión está incompleta y queda como borrador: " + string.Join(" ", faltan));

        // <sent> en UTC: así el valor guardado y el que aparece en el XML (y en los <references> que lo citan) son idénticos.
        var enviado = DateTimeOffset.FromUnixTimeSeconds(ahora.ToUnixTimeSeconds());
        var xml = GenerarXml(version, enviado);
        var validacion = moduloCap.Validar(xml);
        if (!validacion.Valido)
            throw new ExcepcionDominio("El XML CAP 1.2 generado no es válido y la versión queda como borrador: " + string.Join(" ", validacion.Errores));

        version.EnviarARevision(xml, enviado, ahora);
        await db.SaveChangesAsync(ct);
        await Publicar(version, TipoEventoAviso.VersionEnviadaARevision, usuarioId, $"Versión {version.Numero} enviada a revisión.", ct);
        return version;
    }

    /// <summary>RF-11: aprueba la versión. Si es un Update o Cancel, la versión que referencia deja de estar vigente.</summary>
    public async Task<VersionMensaje> AprobarAsync(Guid versionId, Guid usuarioId, CancellationToken ct)
    {
        var version = await Buscar(versionId, ct);
        var aviso = await db.Avisos.FirstAsync(a => a.Id == version.AvisoId, ct);
        if (aviso.Estado == EstadoAviso.Descartado) throw new ExcepcionDominio("El aviso fue descartado: la versión no se puede aprobar.");
        var referencia = version.ReferenciaVersionId is { } refId
            ? await db.VersionesMensaje.FirstOrDefaultAsync(v => v.Id == refId, ct)
            : null;

        version.Aprobar(usuarioId, _reloj.GetUtcNow(), referencia);
        if (aviso.Estado == EstadoAviso.Registrado) aviso.Aprobar();
        await db.SaveChangesAsync(ct);
        await Publicar(version, TipoEventoAviso.Aprobado, usuarioId,
            $"Versión {version.Numero} ({version.Tipo}) aprobada{(version.EsPrueba ? " como prueba" : "")}.", ct);
        return version;
    }

    /// <summary>RF-11: rechaza la versión con su motivo (obligatorio). Vuelve al operador, que la corrige.</summary>
    public async Task<VersionMensaje> RechazarAsync(Guid versionId, string? motivo, Guid usuarioId, CancellationToken ct)
    {
        var version = await Buscar(versionId, ct);
        version.Rechazar(usuarioId, motivo, _reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await Publicar(version, TipoEventoAviso.VersionRechazada, usuarioId, $"Versión {version.Numero} rechazada: {version.MotivoRechazo}", ct);
        return version;
    }

    /// <summary>Corrige una versión rechazada: crea la versión siguiente en borrador, con el motivo a la vista.</summary>
    public async Task<VersionMensaje> CorregirAsync(Guid versionId, Guid usuarioId, CancellationToken ct)
    {
        var rechazada = await Buscar(versionId, ct);
        await ExigirSinVersionEnCursoAsync(rechazada.CadenaId, ct);
        var nueva = rechazada.Corregir(await SiguienteNumeroAsync(rechazada.CadenaId, ct), usuarioId);
        db.VersionesMensaje.Add(nueva);
        await db.SaveChangesAsync(ct);
        await Publicar(nueva, TipoEventoAviso.VersionCorregida, usuarioId,
            $"Versión {nueva.Numero} creada para corregir la versión {rechazada.Numero}.", ct);
        return nueva;
    }

    /// <summary>
    /// Elimina un borrador que no se va a usar. Solo borradores, y solo quien lo preparó o un administrador.
    /// Queda en la bitácora del aviso. Si era la corrección de una rechazada, esa rechazada se puede volver a corregir.
    /// </summary>
    public async Task EliminarBorradorAsync(Guid versionId, Guid usuarioId, bool esAdministrador, CancellationToken ct)
    {
        var version = await Buscar(versionId, ct);
        version.VerificarEliminable(usuarioId, esAdministrador);
        db.VersionesMensaje.Remove(version);
        await db.SaveChangesAsync(ct);
        await Publicar(version, TipoEventoAviso.VersionEliminada, usuarioId,
            $"Borrador de la versión {version.Numero} ({version.Tipo}) eliminado.", ct);
    }

    /// <summary>Arma el mensaje CAP de la versión con el Builder y lo escribe como XML.</summary>
    public string GenerarXml(VersionMensaje v, DateTimeOffset enviado)
    {
        var c = v.Contenido;
        var poligonos = v.Area is null ? [] : PoligonosDe(v.Area).ToArray();
        var mensaje = new MensajeCapBuilder()
            .ConIdentificador(v.IdentificadorCap)
            .DeRemitente(v.Remitente)
            .EnviadoEn(enviado)
            .ConEstado(v.EstadoCap)
            .ConTipo(v.Tipo)
            .ConAlcance(AlcanceCap.Public)
            .ConReferencias(v.Referencias)
            .ConNota($"Versión {v.Numero}. {c.Nivel.Texto}.")
            .AgregarInfo(i =>
            {
                i.ConCategoria(CategoriaCap.Met)
                 .ConEvento(c.Evento)
                 .ConUrgencia(c.Urgencia)
                 .ConSeveridad(c.Severidad)
                 .ConCerteza(c.Certeza)
                 .ConNombreRemitente(opciones.NombreRemitente)
                 .ConTitular(c.Titular)
                 .ConDescripcion(c.Descripcion)
                 .ConInstruccion(c.Instruccion)
                 .VigenteDesde(c.Vigente)
                 .ExpiraEn(c.Expira);
                if (poligonos.Length > 0) i.AgregarArea(c.DescripcionArea ?? "Área del aviso", poligonos);
            })
            .Build();
        return moduloCap.Generar(mensaje);
    }

    private async Task<VersionMensaje> Buscar(Guid versionId, CancellationToken ct) =>
        await db.VersionesMensaje.FirstOrDefaultAsync(v => v.Id == versionId, ct) ?? throw new KeyNotFoundException("Versión no encontrada.");

    /// <summary>La versión aprobada que hoy está vigente en la cadena (no reemplazada), o null.</summary>
    private Task<VersionMensaje?> VigenteDeCadenaAsync(Guid cadenaId, CancellationToken ct) =>
        db.VersionesMensaje
            .Where(v => v.CadenaId == cadenaId && v.ReemplazadaPorId == null
                        && (v.Estado == EstadoVersion.Aprobada || v.Estado == EstadoVersion.Difundida))
            .OrderByDescending(v => v.Numero)
            .FirstOrDefaultAsync(ct);

    /// <summary>Una cadena tiene a lo sumo una versión en preparación: dos borradores a la vez se contradirían.</summary>
    private async Task ExigirSinVersionEnCursoAsync(Guid cadenaId, CancellationToken ct)
    {
        var enCurso = await db.VersionesMensaje
            .Where(v => v.CadenaId == cadenaId && (v.Estado == EstadoVersion.Borrador || v.Estado == EstadoVersion.EnRevision))
            .Select(v => new { v.Numero, v.Estado })
            .FirstOrDefaultAsync(ct);
        if (enCurso is not null)
            throw new ExcepcionDominio($"Ya hay una versión en curso de este aviso (versión {enCurso.Numero}, {enCurso.Estado}). Termínala antes de preparar otra.");
    }

    private async Task<int> SiguienteNumeroAsync(Guid cadenaId, CancellationToken ct) =>
        (await db.VersionesMensaje.Where(v => v.CadenaId == cadenaId).MaxAsync(v => (int?)v.Numero, ct) ?? 0) + 1;

    private Task Publicar(VersionMensaje v, TipoEventoAviso tipo, Guid? usuarioId, string detalle, CancellationToken ct) =>
        eventos.PublicarAsync(new EventoAviso(v.AvisoId, tipo, usuarioId, detalle, _reloj.GetUtcNow(), v.Id), ct);

    internal static IEnumerable<string> PoligonosDe(Geometry geometria) => geometria switch
    {
        Polygon p => [PoligonoCap.DesdeGeometria(p)],
        MultiPolygon mp => mp.Geometries.Cast<Polygon>().Select(PoligonoCap.DesdeGeometria),
        GeometryCollection gc => gc.Geometries.SelectMany(PoligonosDe),
        _ => []
    };
}
