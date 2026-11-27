const BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'
const CLAVE_SESION = 'sat-pirai-sesion'

export type Rol = 'Administrador' | 'Operador' | 'Aprobador' | 'Consulta'
export type NivelRiesgo = 'Bajo' | 'Medio' | 'Alto' | 'MuyAlto'
export const ETIQUETA_NIVEL_RIESGO: Record<NivelRiesgo, string> = { Bajo: 'Bajo', Medio: 'Medio', Alto: 'Alto', MuyAlto: 'Muy alto' }
export type TipoCanal = 'Sms' | 'WhatsApp' | 'Llamada' | 'Email' | 'Radio'
export type Permiso = 'Lectura' | 'Edicion' | 'Aprobacion' | 'Administracion'

export interface Usuario {
  id: string; nombreUsuario: string; nombreCompleto: string; email: string
  roles: Rol[]; permisos: Permiso[]; activo: boolean; creadoEn: string; ultimoAcceso: string | null
}
export interface RegistroBitacora {
  id: string; fecha: string; accion: string; rol: Rol | null; detalle: string | null
  administradorId: string | null; administrador: string | null
}
export interface Sesion { token: string; expira: string; usuario: Usuario }

export interface Zona {
  id: string; nombre: string; descripcion: string | null; nivelRiesgo: NivelRiesgo
  areaKm2: number; comunidades: number; geometria: GeoJSON.Geometry
  fuenteInstitucional: string | null; referenciaDocumento: string | null; fechaDocumento: string | null
}
export interface Canal { tipo: TipoCanal; destino: string; preferido: boolean }
export interface Contacto {
  id: string; comunidadId: string; nombre: string; cargo: string | null; activo: boolean; canales: Canal[]
  procedencia: string; autorizado: boolean; referenciaAutorizacion: string | null; fechaAutorizacion: string | null
  habilitado: boolean; motivosNoHabilitado: string[]
}
export interface HistorialContacto {
  id: string; fecha: string; accion: 'Alta' | 'Modificacion' | 'Baja'; usuario: string | null; cambios: string | null
}
export interface Comunidad {
  id: string; nombre: string; municipio: string | null; poblacion: number | null
  latitud: number | null; longitud: number | null; zonaId: string; zona: string | null
  totalContactos: number; contactosHabilitados: number; sinCanal: boolean; contactos: Contacto[] | null
}
export interface AvisoResumen {
  id: string; identificadorCap: string; remitente: string; enviado: string; formato: string; estado: string
  evento: string; urgencia: string; severidad: string; certeza: string; titular: string | null
  descripcionArea: string | null; expira: string | null; registradoEn: string
  estadoCap: string; incompleto: boolean; vencido: boolean; aptoParaDifusion: boolean; motivosNoApto: string[]
  aptitudEvaluadaEn: string; tieneOriginal: boolean
  tipoMensaje: TipoMensaje; referencias: string | null; cadenaId: string | null
  preparable: boolean; motivosNoPreparable: string[]
}
export interface AvisoDetalle {
  resumen: AvisoResumen; descripcion: string | null; instruccion: string | null; vigente: string | null
  area: GeoJSON.Geometry | null; xmlCap: string
  contenidoOriginal: string | null; tipoContenidoOriginal: string | null; hashOriginal: string | null
}

// ---------- Sprint 2: versiones del mensaje (RF-02, RF-11), destinos (RF-07) y web pública (RF-06) ----------
export type TipoMensaje = 'Alert' | 'Update' | 'Cancel' | 'Ack' | 'Error'
export type EstadoVersion = 'Borrador' | 'EnRevision' | 'Aprobada' | 'Rechazada' | 'Difundida'
export interface Nivel { codigo: string; texto: string; orden: number; color: string }
export interface VersionResumen {
  id: string; avisoId: string; cadenaId: string; numero: number; tipo: TipoMensaje; estado: EstadoVersion; esPrueba: boolean
  nivel: Nivel; evento: string; titular: string | null; descripcionArea: string | null; expira: string | null
  creadoEn: string; preparadoPorId: string; revisadoEn: string | null; motivoRechazo: string | null
  reemplazadaPorId: string | null; faltantes: string[]
}
export interface VersionDetalle {
  detalle: {
    resumen: VersionResumen; urgencia: string; severidad: string; certeza: string
    descripcion: string | null; instruccion: string | null; vigente: string | null
    identificadorCap: string; remitente: string; enviadoCap: string | null; referencias: string | null; referenciaVersionId: string | null
    versionAnteriorId: string | null; motivoDevolucion: string | null; enviadaARevisionEn: string | null; revisadoPorId: string | null
    difundidaEn: string | null; xmlCap: string | null
    fuenteRemitente: string; fuenteIdentificador: string; fuenteEnviado: string; fuenteTipo: TipoMensaje
    protocoloId: string | null; protocoloTitulo: string | null; protocoloCita: string | null
  }
  area: GeoJSON.Geometry | null; textoSms: string; textoLectura: string
}
/** RF-05: protocolo oficial del que salen las instrucciones de un mensaje. */
export interface Protocolo {
  id: string; titulo: string; instruccion: string; fuente: string; referencia: string
  fechaDocumento: string; cita: string; activo: boolean; versiones: number
}
export interface DestinoVista { comunidadId: string; comunidad: string; contactoId: string; contacto: string; canal: TipoCanal; direccion: string }
export interface ListaDestinos {
  versionId: string; guardada: boolean; generadaEn: string | null; canales: TipoCanal[]; comunidades: number
  destinos: DestinoVista[]; pendientes: { comunidadId: string; comunidad: string; motivo: string }[]
}
export interface ResultadoDifusion {
  versionId: string; esPrueba: boolean; capEmitidoId: string; sha256: string; publicadoEnWeb: boolean; urlWeb: string | null
  trabajosSms: number; tareasManuales: number; incidencias: { canal: string; detalle: string }[]; trabajosDetenidos: number
}
// RF-08: seguimiento de la difusión.
export type EstadoTrabajo = 'Pendiente' | 'Enviado' | 'Fallido' | 'Bloqueado' | 'Confirmada' | 'NoRealizada' | 'Cancelado'
export type CanalDifusion = 'Sms' | 'Radio' | 'Altavoz' | 'RedesSociales'
export interface ConteoEstados { total: number; pendientes: number; enviados: number; fallidos: number; bloqueados: number; confirmadas: number; noRealizadas: number; cancelados: number; terminados: number }
export interface SeguimientoResumen {
  versionId: string; avisoId: string; numero: number; tipo: string; titular: string; esPrueba: boolean; difundidaEn: string
  sms: ConteoEstados; tareas: ConteoEstados; avance: number; completo: boolean
}
export interface TareaVista {
  id: string; canal: CanalDifusion; comunidadId: string | null; comunidad: string | null; estado: EstadoTrabajo; textoLegible: string
  observacion: string | null; confirmadoPor: string | null; creadaEn: string; resultadoEn: string | null; resultado: string | null
}
export interface EnvioSmsVista {
  id: string; comunidad: string | null; destino: string; estado: EstadoTrabajo; intentos: number; codigoProveedor: string | null
  ultimoError: string | null; proximoIntento: string | null; resultadoEn: string | null; resultado: string | null
}
export interface SeguimientoDetalle {
  resumen: SeguimientoResumen
  cap: { id: string; identificador: string; tipo: string; estado: string; enviado: string; sha256: string; valido: boolean; emitidoEn: string } | null
  publicadaWebEn: string | null
  comunidades: { comunidadId: string | null; comunidad: string; municipio: string | null; sms: ConteoEstados; altavoz: EstadoTrabajo | null }[]
  tareas: TareaVista[]; envios: EnvioSmsVista[]; smsTerminadosEn: string | null; dentroDeMeta: boolean | null; actualizadoEn: string
}
export interface AvisoPublico {
  id: string; numero: number; tipo: TipoMensaje; nivel: Nivel; evento: string; titular: string | null
  descripcion: string | null; instruccion: string | null; descripcionArea: string | null
  desde: string | null; hasta: string | null; aprobadoEn: string | null; vigente: boolean; reemplazadoPor: string | null
  area: GeoJSON.Geometry | null; textoLectura: string
}

export const NOMBRE_ESTADO_VERSION: Record<EstadoVersion, string> = {
  Borrador: 'Borrador', EnRevision: 'En revisión', Aprobada: 'Aprobada', Rechazada: 'Rechazada', Difundida: 'Difundida',
}
export const NOMBRE_TIPO: Record<TipoMensaje, string> = {
  Alert: 'Alerta', Update: 'Actualización', Cancel: 'Cancelación', Ack: 'Acuse', Error: 'Error',
}

export class ErrorApi extends Error {
  estado: number
  constructor(estado: number, mensaje: string) { super(mensaje); this.estado = estado }
}

export function leerSesion(): Sesion | null {
  try {
    const s = JSON.parse(localStorage.getItem(CLAVE_SESION) ?? 'null') as Sesion | null
    // Una sesión guardada antes de los roles múltiples no trae "roles": se pide entrar de nuevo.
    return s && Array.isArray(s.usuario?.roles) && new Date(s.expira) > new Date() ? s : null
  } catch { return null }
}
const CLAVE_VENCIDA = 'sat-pirai-sesion-vencida'
const haySesionGuardada = () => { try { return localStorage.getItem(CLAVE_SESION) !== null } catch { return false } }
/** Deja dicho que la sesión terminó sola, para avisarlo en el login en vez de devolver a la página pública sin explicación. */
export function marcarSesionVencida() { try { sessionStorage.setItem(CLAVE_VENCIDA, '1') } catch { /* sin almacenamiento */ } }
export function sesionVencidaPendiente(): boolean {
  try { const v = sessionStorage.getItem(CLAVE_VENCIDA) === '1'; sessionStorage.removeItem(CLAVE_VENCIDA); return v } catch { return false }
}
export function guardarSesion(s: Sesion | null) {
  try {
    if (s) localStorage.setItem(CLAVE_SESION, JSON.stringify(s))
    else localStorage.removeItem(CLAVE_SESION)
  } catch { /* almacenamiento no disponible */ }
}

export async function api<T>(ruta: string, opciones: RequestInit & { json?: unknown } = {}): Promise<T> {
  const { json, headers, ...resto } = opciones
  const h = new Headers(headers)
  const sesion = leerSesion()
  if (sesion) h.set('Authorization', `Bearer ${sesion.token}`)
  let body = resto.body
  if (json !== undefined) { h.set('Content-Type', 'application/json'); body = JSON.stringify(json) }

  let r: Response
  try { r = await fetch(`${BASE}${ruta}`, { ...resto, headers: h, body }) }
  catch { throw new ErrorApi(0, 'No se pudo conectar con el servidor. Revise su conexión e intente de nuevo.') }
  // Un 401 fuera del login es una sesión vencida o inválida. Se mira lo guardado y no solo `sesion`: con el panel abierto más de
  // 8 horas, `leerSesion()` ya devuelve null (caducó) y la petición sale sin credenciales; antes eso dejaba "Error 401" en todo.
  if (r.status === 401 && ruta !== '/api/auth/login' && (sesion || haySesionGuardada())) { marcarSesionVencida(); guardarSesion(null); window.location.reload() }
  if (!r.ok) {
    let mensaje = `Error ${r.status}`
    try { const p = await r.json(); mensaje = p.detail ?? p.title ?? mensaje } catch { /* sin cuerpo */ }
    if (r.status === 403) mensaje = 'Tus roles no tienen permiso para esta acción.'
    throw new ErrorApi(r.status, mensaje)
  }
  if (r.status === 204) return undefined as T
  const tipo = r.headers.get('Content-Type') ?? ''
  return (tipo.includes('json') ? r.json() : r.text()) as Promise<T>
}

export const puedeEditar = (roles: Rol[]) => roles.includes('Administrador') || roles.includes('Operador')
export const puedeAprobar = (roles: Rol[]) => roles.includes('Administrador') || roles.includes('Aprobador')
export const esAdministrador = (roles: Rol[]) => roles.includes('Administrador')

export const fecha = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString('es-BO', { dateStyle: 'short', timeStyle: 'short' }) : '—'

export const soloFecha = (iso: string | null) =>
  iso ? new Date(`${iso}T00:00:00`).toLocaleDateString('es-BO', { dateStyle: 'medium' }) : '—'

/** Descarga un texto como archivo. */
export function descargarTexto(texto: string, tipo: string, nombre: string) {
  const url = URL.createObjectURL(new Blob([texto], { type: tipo }))
  Object.assign(document.createElement('a'), { href: url, download: nombre }).click()
  URL.revokeObjectURL(url)
}
