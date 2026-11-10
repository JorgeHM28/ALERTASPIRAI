const BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'
const CLAVE_SESION = 'sat-pirai-sesion'

export type Rol = 'Administrador' | 'Operador' | 'Aprobador' | 'Consulta'
export type NivelRiesgo = 'Bajo' | 'Medio' | 'Alto' | 'MuyAlto'
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
}
export interface AvisoDetalle {
  resumen: AvisoResumen; descripcion: string | null; instruccion: string | null; vigente: string | null
  area: GeoJSON.Geometry | null; xmlCap: string
  contenidoOriginal: string | null; tipoContenidoOriginal: string | null; hashOriginal: string | null
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

  const r = await fetch(`${BASE}${ruta}`, { ...resto, headers: h, body })
  if (r.status === 401 && sesion) { guardarSesion(null); window.location.reload() }
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
