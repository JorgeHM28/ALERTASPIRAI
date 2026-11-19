using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SistemaAlertas.Api.Comun;
using SistemaAlertas.Api.ViewModels;
using SistemaAlertas.Application.Abstracciones;
using SistemaAlertas.Application.Avisos;
using SistemaAlertas.Application.Comunidades;
using SistemaAlertas.Application.Difusion;
using SistemaAlertas.Application.Difusion.Sms;
using SistemaAlertas.Application.Mensajes;
using SistemaAlertas.Application.Usuarios;
using SistemaAlertas.Application.Zonas;
using SistemaAlertas.Domain.Avisos;
using SistemaAlertas.Domain.Cap;
using SistemaAlertas.Domain.Comun;
using SistemaAlertas.Domain.Comunidades;
using SistemaAlertas.Domain.Mensajes;
using SistemaAlertas.Domain.Usuarios;
using SistemaAlertas.Domain.Zonas;
namespace SistemaAlertas.Api.Controllers.Web;

[Authorize(Politicas.Lectura), ApiExplorerSettings(IgnoreApi = true), AutoValidateAntiforgeryToken]
public sealed class GestionController(IAppDbContext db, ServicioZonas zonas, ServicioComunidades comunidades,
    ServicioAvisos avisos, ServicioVersiones versiones, ServicioDestinos destinos, ServicioUsuarios usuarios,
    IServicioDifusion difusion, IGestorTareasManuales tareas, IBitacoraEnvios bitacora,
    IConversorGeoJson geo, IHasherContrasena hasher, IUsuarioActual actual) : Controller
{
    private bool Edita => actual.Tiene(Permiso.Edicion);
    private bool Aprueba => actual.Tiene(Permiso.Aprobacion);
    private static Campo C(string n, string e, string? v = null, string t = "text", bool obligatorio = false) => new(n,e,t,v,obligatorio);
    private static Campo H(string n, object? v) => C(n,n,v?.ToString(),"hidden");
    private static Campo EnumCampo<T>(string n, string e, object? valor = null, bool multiple = false) where T : struct, Enum =>
        new(n,e,multiple ? "multiple" : "select",valor?.ToString(),true,Enum.GetNames<T>().Select(v => new Opcion(v,v)));
    private static Campo SiNo(string n, string e, bool valor) => new(n,e,"select",valor ? "true":"false",true,[new("true","Sí"),new("false","No")]);
    private static string Fecha(DateTimeOffset? f) => f?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz",CultureInfo.InvariantCulture) ?? "";
    private static string? Numero(object? v) => Convert.ToString(v,CultureInfo.InvariantCulture);
    private static Formulario F(string titulo, string accion, params Campo[] campos) => new(titulo,"/gestion/"+accion,campos);
    private static void Borrar(Pagina p,string accion,Guid id) => p.Formularios.Add(new("Eliminar registro","/gestion/"+accion,[H("id",id)],"Eliminar"));
    private IActionResult Vista(Pagina p) => View("Pagina",p);
    private IActionResult Listo(string ruta) { TempData["Exito"]="Operación completada."; return Redirect(ruta); }
    private async Task<Campo> ZonasCampo(string n, string? seleccion = null, bool multiple = false) =>
        new(n,"Zona de riesgo",multiple?"multiple":"select",seleccion,true,(await zonas.Listar(HttpContext.RequestAborted)).Select(z=>new Opcion(z.Id.ToString(),z.Nombre)));

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode=400;
            context.Result=View("Error",string.Join(" ",ModelState.Values.SelectMany(v=>v.Errors).Select(e=>string.IsNullOrEmpty(e.ErrorMessage)?"Revisa los valores del formulario.":e.ErrorMessage)));
            return;
        }
        var result=await next();
        if(result.Exception is ExcepcionDominio or KeyNotFoundException or JsonException or DbUpdateException)
        {
            Response.StatusCode=result.Exception is KeyNotFoundException?404:result.Exception is DbUpdateException?409:400;
            result.Result=View("Error",result.Exception is DbUpdateException?"El registro está duplicado o relacionado con otros datos. Revisa la operación.":result.Exception.Message);
            result.ExceptionHandled=true;
        }
    }

    [HttpGet("/gestion/zonas")]
    public async Task<IActionResult> Zonas(Guid? id, CancellationToken ct)
    {
        var p=new Pagina("Zonas de riesgo"){Descripcion="Áreas oficiales, comunidades y procedencia institucional.",Columnas=["Nombre","Nivel","Comunidades","Área aproximada km²"]};
        foreach(var z in await zonas.Listar(ct)) p.Filas.Add(new([z.Nombre,z.NivelRiesgo.ToString(),z.Comunidades.ToString(),Numero(z.AreaKm2)!],[new("Ver / editar","/gestion/zonas?id="+z.Id),new("Comunidades","/gestion/comunidades?zonaId="+z.Id)]));
        var zona=id is Guid i?await zonas.Obtener(i,ct):null;
        p.GeoJson=zona?.Geometria.GetRawText() ?? (await zonas.GeoJsonColeccion(ct)).GetRawText();
        if(Edita)
        {
            p.Formularios.Add(F(zona is null?"Nueva zona":"Editar zona","zona-guardar",H("id",id),C("Nombre","Nombre",zona?.Nombre,obligatorio:true),C("Descripcion","Descripción",zona?.Descripcion,"textarea"),EnumCampo<NivelRiesgo>("NivelRiesgo","Nivel de riesgo",zona?.NivelRiesgo),C("GeometriaJson","Geometría GeoJSON",zona?.Geometria.GetRawText(),"textarea",true),C("FuenteInstitucional","Fuente institucional",zona?.FuenteInstitucional,obligatorio:true),C("ReferenciaDocumento","Referencia del documento",zona?.ReferenciaDocumento,obligatorio:true),C("FechaDocumento","Fecha del documento",zona?.FechaDocumento?.ToString("yyyy-MM-dd"),"date",true)));
            p.Formularios.Add(F("Importar zonas","zonas-importar",C("GeoJson","FeatureCollection GeoJSON",null,"textarea",true),C("FuenteInstitucional","Fuente institucional",obligatorio:true),C("ReferenciaDocumento","Referencia del documento",obligatorio:true),C("FechaDocumento","Fecha del documento",null,"date",true)));
            if(id is Guid zid) Borrar(p,"zona-eliminar",zid);
        }
        return Vista(p);
    }
    [HttpPost("/gestion/zona-guardar"),Authorize(Politicas.Edicion)]
    public async Task<IActionResult> GuardarZona(Guid? id,string nombre,string? descripcion,NivelRiesgo nivelRiesgo,string geometriaJson,string fuenteInstitucional,string referenciaDocumento,DateOnly fechaDocumento,CancellationToken ct)
    { var dto=new GuardarZona(nombre,descripcion,nivelRiesgo,JsonSerializer.Deserialize<JsonElement>(geometriaJson),fuenteInstitucional,referenciaDocumento,fechaDocumento);var z=id is Guid i?await zonas.Actualizar(i,dto,ct):await zonas.Crear(dto,ct);return Listo("/gestion/zonas?id="+z.Id); }
    [HttpPost("/gestion/zonas-importar"),Authorize(Politicas.Edicion)]
    public async Task<IActionResult> ImportarZonas(string geoJson,string fuenteInstitucional,string referenciaDocumento,DateOnly fechaDocumento,CancellationToken ct)
    { await zonas.Importar(JsonSerializer.Deserialize<JsonElement>(geoJson),fuenteInstitucional,referenciaDocumento,fechaDocumento,ct);return Listo("/gestion/zonas"); }
    [HttpPost("/gestion/zona-eliminar"),Authorize(Politicas.Edicion)] public async Task<IActionResult> EliminarZona(Guid id,CancellationToken ct){await zonas.Eliminar(id,ct);return Listo("/gestion/zonas");}

    [HttpGet("/gestion/comunidades")]
    public async Task<IActionResult> Comunidades(Guid? id,Guid? zonaId,string? buscar,CancellationToken ct)
    {
        var p=new Pagina("Comunidades"){Descripcion="Contactos autorizados y canales para llegar a cada comunidad.",Columnas=["Comunidad","Zona","Municipio","Contactos habilitados"]};
        foreach(var c in await comunidades.Listar(zonaId,buscar,ct))p.Filas.Add(new([c.Nombre,c.Zona??"",c.Municipio??"",c.ContactosHabilitados.ToString()],[new("Ver / editar","/gestion/comunidades?id="+c.Id)]));
        var comunidad=id is Guid i?await comunidades.Obtener(i,ct):null;
        if(comunidad is not null)
        {
            p.Textos.Add(("Contactos de "+comunidad.Nombre,string.Join("\n",(comunidad.Contactos??[]).Select(c=>$"{c.Nombre}: {(c.Habilitado?"Habilitado":string.Join(", ",c.MotivosNoHabilitado))}"))));
            foreach(var c in comunidad.Contactos??[])p.Enlaces.Add(new("Contacto: "+c.Nombre,"/gestion/contacto?id="+c.Id));
            if(Edita)p.Enlaces.Add(new("Agregar contacto","/gestion/contacto?comunidadId="+comunidad.Id));
        }
        if(Edita)
        {
            p.Formularios.Add(F(comunidad is null?"Nueva comunidad":"Editar comunidad","comunidad-guardar",H("id",id),C("Nombre","Nombre",comunidad?.Nombre,obligatorio:true),C("Municipio","Municipio",comunidad?.Municipio),C("Poblacion","Población",Numero(comunidad?.Poblacion),"number"),C("Latitud","Latitud",Numero(comunidad?.Latitud),"number"),C("Longitud","Longitud",Numero(comunidad?.Longitud),"number"),await ZonasCampo("ZonaId",(comunidad?.ZonaId??zonaId)?.ToString())));
            if(id is Guid cid)Borrar(p,"comunidad-eliminar",cid);
        }
        return Vista(p);
    }
    [HttpPost("/gestion/comunidad-guardar"),Authorize(Politicas.Edicion)] public async Task<IActionResult> GuardarComunidad(Guid? id,GuardarComunidad dto,CancellationToken ct)
    { var c=id is Guid i?await comunidades.Actualizar(i,dto,ct):await comunidades.Crear(dto,ct);return Listo("/gestion/comunidades?id="+c.Id); }
    [HttpPost("/gestion/comunidad-eliminar"),Authorize(Politicas.Edicion)] public async Task<IActionResult> EliminarComunidad(Guid id,CancellationToken ct){await comunidades.Eliminar(id,ct);return Listo("/gestion/comunidades");}

    [HttpGet("/gestion/contacto")]
    public async Task<IActionResult> Contacto(Guid? id,Guid? comunidadId,CancellationToken ct)
    {
        var c=id is Guid i?await comunidades.ObtenerContacto(i,ct):null;
        var p=new Pagina(c?.Nombre??"Nuevo contacto");
        if(c is not null)
        {
            p.Textos.Add(("Canales",string.Join("\n",c.Canales.Select(k=>$"{k.Tipo}: {k.Destino}{(k.Preferido?" (preferido)":"")}"))));
            p.Textos.Add(("Procedencia y autorización",$"{c.Procedencia}\n{c.ReferenciaAutorizacion}\n{c.FechaAutorizacion}\n{string.Join(", ",c.MotivosNoHabilitado)}"));
            p.Enlaces.Add(new("Historial","/gestion/historial-contacto?id="+c.Id));
        }
        if(Edita)
        {
            var campos=new List<Campo>{H("id",id),H("comunidadId",c?.ComunidadId??comunidadId),C("Nombre","Nombre",c?.Nombre,obligatorio:true),C("Cargo","Cargo",c?.Cargo),SiNo("Activo","Activo",c?.Activo??true),C("Procedencia","Procedencia",c?.Procedencia,obligatorio:true),SiNo("Autorizado","Autorizado",c?.Autorizado??false),C("ReferenciaAutorizacion","Referencia de autorización",c?.ReferenciaAutorizacion),C("FechaAutorizacion","Fecha de autorización",c?.FechaAutorizacion?.ToString("yyyy-MM-dd"),"date")};
            var canales=(c?.Canales??[]).ToList();var filas=Math.Max(5,canales.Count+1);
            for(var n=0;n<filas;n++){var k=canales.ElementAtOrDefault(n);campos.Add(EnumCampo<TipoCanal>($"Canales[{n}].Tipo",$"Canal {n+1}",k?.Tipo));campos.Add(C($"Canales[{n}].Destino","Teléfono, correo o frecuencia (vacío = no incluir)",k?.Destino));campos.Add(SiNo($"Canales[{n}].Preferido","Preferido",k?.Preferido??false));}
            p.Formularios.Add(new("Datos del contacto","/gestion/contacto-guardar",campos));
            if(id is Guid cid)Borrar(p,"contacto-eliminar",cid);
        }
        return Vista(p);
    }
    [HttpPost("/gestion/contacto-guardar"),Authorize(Politicas.Edicion)]
    public async Task<IActionResult> GuardarContacto(Guid? id,Guid comunidadId,string nombre,string? cargo,bool activo,string procedencia,bool autorizado,string? referenciaAutorizacion,DateOnly? fechaAutorizacion,List<CanalEntrada> canales,CancellationToken ct)
    {var dto=new GuardarContacto(nombre,cargo,activo,canales.Where(c=>!string.IsNullOrWhiteSpace(c.Destino)).Select(c=>new CanalDto(c.Tipo,c.Destino!,c.Preferido)).ToList(),procedencia,autorizado,referenciaAutorizacion,fechaAutorizacion);var c=id is Guid i?await comunidades.ActualizarContacto(i,dto,ct):await comunidades.AgregarContacto(comunidadId,dto,ct);return Listo("/gestion/contacto?id="+c.Id);}
    [HttpPost("/gestion/contacto-eliminar"),Authorize(Politicas.Edicion)]public async Task<IActionResult> EliminarContacto(Guid id,CancellationToken ct){await comunidades.EliminarContacto(id,ct);return Listo("/gestion/comunidades");}
    [HttpGet("/gestion/historial-contacto")]public async Task<IActionResult> HistorialContacto(Guid id,CancellationToken ct)
    {var p=new Pagina("Historial del contacto");foreach(var h in await comunidades.Historial(id,ct))p.Textos.Add(($"{h.Fecha:g} · {h.Accion} · {h.Usuario}",$"{h.Cambios}\n{h.Instantanea}"));return Vista(p);}
    [HttpGet("/gestion/destinatarios")]public async Task<IActionResult> Destinatarios(CancellationToken ct)
    {var d=await comunidades.Destinatarios(null,ct);var p=new Pagina("Destinatarios"){Columnas=["Contacto","Comunidad","Canales"]};foreach(var c in d.Destinatarios)p.Filas.Add(new([c.Nombre,c.Comunidad,string.Join(" · ",c.Canales.Select(k=>$"{k.Tipo}: {k.Destino}"))],[]));p.Textos.Add(("Excluidos",string.Join("\n",d.Excluidos.Select(c=>$"{c.Nombre}: {string.Join(", ",c.Motivos)}"))));p.Textos.Add(("Comunidades sin canal",string.Join("\n",d.ComunidadesSinCanal.Select(c=>$"{c.Nombre}: {c.Motivo}"))));return Vista(p);}

    [HttpGet("/gestion/avisos")]
    public async Task<IActionResult> Avisos(Guid? id,EstadoAviso? estado,CancellationToken ct)
    {
        await avisos.RevisarVigenciaAsync(ct);
        var p=new Pagina("Avisos oficiales"){Descripcion="Documentos oficiales y registro manual. El original se conserva sin modificar.",Columnas=["Evento","Titular","Estado","Expira"]};
        var q=db.Avisos.AsNoTracking();if(estado is not null)q=q.Where(a=>a.Estado==estado);
        foreach(var a in await q.OrderByDescending(a=>a.Enviado).Take(500).ToListAsync(ct))p.Filas.Add(new([a.Evento,a.Titular??"",a.Estado.ToString(),a.Expira?.ToString("g")??""],[new("Detalle","/gestion/avisos?id="+a.Id)]));
        if(id is Guid i)
        {
            var a=await db.Avisos.AsNoTracking().FirstOrDefaultAsync(a=>a.Id==i,ct)??throw new KeyNotFoundException("Aviso no encontrado.");
            p.Textos.Add((a.Evento,$"{a.Descripcion}\n{a.Instruccion}\n{a.Remitente}\n{string.Join("\n",a.MotivosNoPreparable(DateTimeOffset.UtcNow))}"));
            p.Textos.Add(("CAP XML",a.XmlCap));p.GeoJson=a.Area is null?null:geo.AGeoJson(a.Area).GetRawText();
            if(a.ContenidoOriginal is not null)p.Textos.Add(("Documento original · SHA-256 "+a.HashOriginal,a.ContenidoOriginal));
            p.Enlaces.Add(new("Versiones","/gestion/mensajes?avisoId="+a.Id));
            if(Edita){p.Formularios.Add(F("Preparar mensaje","mensaje-preparar",H("id",id)));p.Formularios.Add(F("Descartar aviso","aviso-descartar",H("id",id)));}
        }
        if(Edita)
        {
            p.Formularios.Add(F("Registrar aviso manual","aviso-crear",C("Evento","Evento",obligatorio:true),EnumCampo<UrgenciaCap>("Urgencia","Urgencia"),EnumCampo<SeveridadCap>("Severidad","Severidad"),EnumCampo<CertezaCap>("Certeza","Certeza"),C("Titular","Titular"),C("Descripcion","Descripción",null,"textarea"),C("Instruccion","Instrucciones",null,"textarea"),C("Vigente","Vigente desde (UTC)",Fecha(DateTimeOffset.UtcNow),"text"),C("Expira","Expira (UTC)",Fecha(DateTimeOffset.UtcNow.AddHours(24)),"text"),await ZonasCampo("ZonaIds",multiple:true)));
            p.Formularios.Add(F("Importar CAP","aviso-importar",C("Contenido","Documento XML o JSON",null,"textarea",true)));
        }
        return Vista(p);
    }
    [HttpPost("/gestion/aviso-crear"),Authorize(Politicas.Edicion)]public async Task<IActionResult> CrearAviso(RegistrarAvisoManual dto,CancellationToken ct){var a=await avisos.RegistrarManualAsync(dto,User.Id(),ct);return Listo("/gestion/avisos?id="+a.Id);}
    [HttpPost("/gestion/aviso-importar"),Authorize(Politicas.Edicion)]public async Task<IActionResult> ImportarAviso(string contenido,CancellationToken ct){if(contenido.Length>1_000_000)throw new ExcepcionDominio("El documento supera el límite de 1 MB.");var a=await avisos.ImportarAsync(contenido,null,User.Id(),ct);return Listo("/gestion/avisos?id="+a.Id);}
    [HttpPost("/gestion/aviso-descartar"),Authorize(Politicas.Edicion)]public async Task<IActionResult> DescartarAviso(Guid id,CancellationToken ct){await avisos.DescartarAsync(id,User.Id(),ct);return Listo("/gestion/avisos?id="+id);}
    [HttpPost("/gestion/mensaje-preparar"),Authorize(Politicas.Edicion)]public async Task<IActionResult> Preparar(Guid id,CancellationToken ct){var v=await versiones.PrepararDesdeAvisoAsync(id,new(null,null,null),User.Id(),ct);return Listo("/gestion/mensajes?id="+v.Id);}

    [HttpGet("/gestion/mensajes")]
    public async Task<IActionResult> Mensajes(Guid? id,Guid? avisoId,EstadoVersion? estado,CancellationToken ct)
    {
        var p=new Pagina("Mensajes"){Descripcion="Redacción, revisión, aprobación y alcance de cada versión.",Columnas=["Versión","Evento","Tipo","Estado","Nivel"]};
        var q=db.VersionesMensaje.AsNoTracking();if(avisoId is Guid aid)q=q.Where(v=>v.AvisoId==aid||v.CadenaId==aid);if(estado is not null)q=q.Where(v=>v.Estado==estado);
        foreach(var v in await q.OrderByDescending(v=>v.CreadoEn).Take(500).ToListAsync(ct))p.Filas.Add(new([v.Numero.ToString(),v.Contenido.Evento,v.Tipo.ToString(),v.Estado.ToString(),v.Nivel.Texto],[new("Detalle","/gestion/mensajes?id="+v.Id)]));
        if(id is Guid i)
        {
            var v=await db.VersionesMensaje.AsNoTracking().FirstOrDefaultAsync(v=>v.Id==i,ct)??throw new KeyNotFoundException("Mensaje no encontrado.");
            var a=await db.Avisos.AsNoTracking().FirstAsync(a=>a.Id==v.AvisoId,ct);
            p.Textos.Add(("Fuente oficial",$"{a.Remitente}\n{a.IdentificadorCap}\n{a.Enviado:O}\n{a.XmlCap}"));
            p.Textos.Add(("Contenido y revisión",$"{v.Contenido.Titular}\n{v.Contenido.Descripcion}\n{v.Contenido.Instruccion}\n{v.MotivoRechazo}\n{v.MotivoDevolucion}\nPreparado por: {v.PreparadoPorId}\nRevisado por: {v.RevisadoPorId}\nRevisado: {v.RevisadoEn:O}\nReferencias: {v.Referencias}"));
            p.Textos.Add(("SMS",TextosMensaje.De(v).Sms));p.Textos.Add(("Lectura",TextosMensaje.De(v).Completo));
            if(v.XmlCap is not null)p.Textos.Add(("CAP XML",v.XmlCap));
            p.GeoJson=v.Area is null?null:geo.AGeoJson(v.Area).GetRawText();
            p.Enlaces.Add(new("Alcance y destinos","/gestion/destinos?id="+id));
            p.Enlaces.Add(new("Aviso de origen","/gestion/avisos?id="+v.AvisoId));
            if(Edita)
            {
                if(v.Estado==EstadoVersion.Borrador){p.Formularios.Add(F("Redactar borrador","mensaje-redactar",H("id",id),C("Titular","Titular",v.Contenido.Titular),C("Descripcion","Descripción",v.Contenido.Descripcion,"textarea"),C("Instruccion","Instrucciones",v.Contenido.Instruccion,"textarea")));p.Formularios.Add(F("Enviar a revisión","mensaje-revision",H("id",id)));}
                if(v.Estado==EstadoVersion.Rechazada)p.Formularios.Add(F("Corregir rechazo","mensaje-corregir",H("id",id)));
                if(v.Estado is EstadoVersion.Aprobada or EstadoVersion.Difundida){p.Formularios.Add(F("Preparar actualización","mensaje-derivar",H("id",id),H("tipo",TipoMensajeCap.Update)));p.Formularios.Add(F("Preparar cancelación","mensaje-derivar",H("id",id),H("tipo",TipoMensajeCap.Cancel)));}
            }
            if(Aprueba&&v.Estado==EstadoVersion.EnRevision){p.Formularios.Add(F("Aprobar mensaje","mensaje-aprobar",H("id",id)));p.Formularios.Add(F("Rechazar mensaje","mensaje-rechazar",H("id",id),C("Motivo","Motivo obligatorio",null,"textarea",true)));}
            if(Aprueba&&v.Estado is EstadoVersion.Aprobada or EstadoVersion.Difundida)p.Formularios.Add(F("Difundir mensaje","mensaje-difundir",H("id",id)));
        }
        return Vista(p);
    }
    [HttpPost("/gestion/mensaje-redactar"),Authorize(Politicas.Edicion)]public async Task<IActionResult> Redactar(Guid id,RedaccionVersion dto,CancellationToken ct){await versiones.RedactarAsync(id,dto with {Titular=dto.Titular??"",Descripcion=dto.Descripcion??"",Instruccion=dto.Instruccion??""},User.Id(),ct);return Listo("/gestion/mensajes?id="+id);}
    [HttpPost("/gestion/mensaje-revision"),Authorize(Politicas.Edicion)]public async Task<IActionResult> Revision(Guid id,CancellationToken ct){await versiones.EnviarARevisionAsync(id,User.Id(),ct);return Listo("/gestion/mensajes?id="+id);}
    [HttpPost("/gestion/mensaje-aprobar"),Authorize(Politicas.Aprobacion)]public async Task<IActionResult> Aprobar(Guid id,CancellationToken ct){await versiones.AprobarAsync(id,User.Id(),ct);return Listo("/gestion/mensajes?id="+id);}
    [HttpPost("/gestion/mensaje-rechazar"),Authorize(Politicas.Aprobacion)]public async Task<IActionResult> Rechazar(Guid id,string motivo,CancellationToken ct){await versiones.RechazarAsync(id,motivo,User.Id(),ct);return Listo("/gestion/mensajes?id="+id);}
    [HttpPost("/gestion/mensaje-corregir"),Authorize(Politicas.Edicion)]public async Task<IActionResult> Corregir(Guid id,CancellationToken ct){var v=await versiones.CorregirAsync(id,User.Id(),ct);return Listo("/gestion/mensajes?id="+v.Id);}
    [HttpPost("/gestion/mensaje-derivar"),Authorize(Politicas.Edicion)]public async Task<IActionResult> Derivar(Guid id,TipoMensajeCap tipo,CancellationToken ct){if(tipo is not (TipoMensajeCap.Update or TipoMensajeCap.Cancel))throw new ExcepcionDominio("Tipo no permitido.");var v=await versiones.DerivarAsync(id,tipo,new(null,null,null),User.Id(),ct);return Listo("/gestion/mensajes?id="+v.Id);}
    [HttpPost("/gestion/mensaje-difundir"),Authorize(Politicas.Aprobacion)]public async Task<IActionResult> Difundir(Guid id,CancellationToken ct){var resultado=await difusion.DifundirAsync(id,ct);TempData["Exito"]=$"Difusión procesada: {resultado.TrabajosSms} SMS en cola; {resultado.Incidencias.Count} incidencias.";return Redirect("/gestion/difusion");}

    [HttpGet("/gestion/destinos")]
    public async Task<IActionResult> Destinos(Guid id,List<TipoCanal>? canales,CancellationToken ct)
    {
        var d=await destinos.PreviaAsync(id,canales,ct);var guardada=await destinos.ObtenerAsync(id,ct);
        var p=new Pagina("Alcance y destinos"){Descripcion=$"Vista previa: {d.Comunidades} comunidades. Lista guardada: {(guardada is null?"no":guardada.GeneradaEn?.ToString("g"))}.",Columnas=["Comunidad","Contacto","Canal","Destino"]};
        foreach(var c in d.Destinos)p.Filas.Add(new([c.Comunidad,c.Contacto,c.Canal.ToString(),c.Direccion],[]));
        p.Textos.Add(("Comunidades pendientes",string.Join("\n",d.Pendientes.Select(c=>$"{c.Comunidad}: {c.Motivo}"))));
        if(guardada is not null)p.Textos.Add(("Lista guardada",string.Join("\n",guardada.Destinos.Select(c=>$"{c.Comunidad} · {c.Canal}: {c.Direccion}"))));
        if(Edita)p.Formularios.Add(F("Guardar lista de destinos","destinos-guardar",H("id",id),new Campo("Canales","Canales aprobados","multiple",string.Join(",",d.Canales),true,destinos.CanalesAprobados.Select(c=>new Opcion(c.ToString(),c.ToString())))));
        p.Enlaces.Add(new("Volver al mensaje","/gestion/mensajes?id="+id));return Vista(p);
    }
    [HttpPost("/gestion/destinos-guardar"),Authorize(Politicas.Edicion)]public async Task<IActionResult> GuardarDestinos(Guid id,List<TipoCanal>? canales,CancellationToken ct){await destinos.GenerarAsync(id,canales,User.Id(),ct);return Listo("/gestion/destinos?id="+id);}
    [HttpGet("/gestion/difusion")]public IActionResult Difusion()
    {var p=new Pagina("Difusión"){Descripcion="Las tareas y bitácoras de este sprint son temporales y se reinician con la aplicación."};foreach(var t in tareas.Listar(null))p.Textos.Add(("Tarea · "+t.Canal,t.Instrucciones));p.Textos.Add(("Bitácora SMS",JsonSerializer.Serialize(bitacora.Recientes(100),new JsonSerializerOptions{WriteIndented=true})));return Vista(p);}

    [HttpGet("/gestion/usuarios"),Authorize(Politicas.Administracion)]
    public async Task<IActionResult> Usuarios(Guid? id,CancellationToken ct)
    {
        var p=new Pagina("Usuarios y permisos"){Columnas=["Usuario","Nombre","Roles","Activo"]};
        foreach(var u in await usuarios.ListarAsync(ct))p.Filas.Add(new([u.NombreUsuario,u.NombreCompleto,string.Join(", ",u.Roles),u.Activo?"Sí":"No"],[new("Editar","/gestion/usuarios?id="+u.Id)]));
        var usuario=id is Guid i?await usuarios.ObtenerAsync(i,ct)??throw new KeyNotFoundException("Usuario no encontrado."):null;
        var campos=new List<Campo>{H("id",id),C("NombreCompleto","Nombre completo",usuario?.NombreCompleto,obligatorio:true),C("Email","Correo electrónico",usuario?.Email,"email",true),EnumCampo<Rol>("Roles","Roles",usuario is null?"Consulta":string.Join(",",usuario.Roles),true)};
        if(usuario is null){campos.Add(C("NombreUsuario","Nombre de usuario",obligatorio:true));campos.Add(C("Contrasena","Contraseña (8 caracteres, letras y números)",null,"password",true));}else campos.Add(SiNo("Activo","Activo",usuario.Activo));
        p.Formularios.Add(new(usuario is null?"Crear usuario":"Editar usuario","/gestion/usuario-guardar",campos));
        if(usuario is not null){p.Formularios.Add(F("Restablecer contraseña","usuario-contrasena",H("id",id),C("Nueva","Nueva contraseña",null,"password",true)));foreach(var b in await usuarios.BitacoraAsync(usuario.Id,ct))p.Textos.Add(($"{b.Fecha:g} · {b.Accion}",$"{b.Rol} · {b.Detalle} · {b.Administrador}"));}
        return Vista(p);
    }
    [HttpPost("/gestion/usuario-guardar"),Authorize(Politicas.Administracion)]public async Task<IActionResult> GuardarUsuario(Guid? id,string nombreCompleto,string email,List<Rol> roles,string? nombreUsuario,string? contrasena,bool activo,CancellationToken ct)
    {if(id is Guid i)await usuarios.ActualizarAsync(i,new(nombreCompleto,email,roles,activo),User.Id(),ct);else await usuarios.CrearAsync(new(nombreUsuario??"",nombreCompleto,email,roles,contrasena??""),User.Id(),ct);return Listo("/gestion/usuarios");}
    [HttpPost("/gestion/usuario-contrasena"),Authorize(Politicas.Administracion)]public async Task<IActionResult> ContrasenaUsuario(Guid id,string nueva,CancellationToken ct){await usuarios.CambiarContrasenaAsync(id,nueva,User.Id(),ct);return Listo("/gestion/usuarios?id="+id);}
    [HttpGet("/gestion/cuenta")]public IActionResult Cuenta(){var p=new Pagina("Mi cuenta");p.Formularios.Add(F("Cambiar contraseña","cuenta-contrasena",C("Actual","Contraseña actual",null,"password",true),C("Nueva","Nueva contraseña",null,"password",true)));return Vista(p);}
    [HttpPost("/gestion/cuenta-contrasena")]public async Task<IActionResult> CambiarContrasena(string actual,string nueva,CancellationToken ct){var u=await usuarios.ObtenerAsync(User.Id(),ct)??throw new KeyNotFoundException();if(!hasher.Verificar(u,actual))throw new ExcepcionDominio("Contraseña actual incorrecta.");await usuarios.CambiarContrasenaAsync(u.Id,nueva,null,ct);return Listo("/gestion/cuenta");}
}
public sealed record CanalEntrada(TipoCanal Tipo,string? Destino,bool Preferido);
