import { lazy, Suspense, useEffect, useState } from 'react'
import { Activity, ArrowLeft, ArrowRight, Bell, BookOpenCheck, Eye, EyeOff, LogOut, Map, MapPin, MessageSquareText, ShieldCheck, Users, Waves } from 'lucide-react'
import 'leaflet/dist/leaflet.css'
import './index.css'
import { api, esAdministrador, guardarSesion, leerSesion, marcarSesionVencida, sesionVencidaPendiente, type Sesion } from './api'
import { Alert } from './components/ui/Alert'
import { Esqueleto } from './components/ui/Esqueleto'
import { EstadoPublico } from './components/EstadoPublico'
const Zonas = lazy(() => import('./paginas/Zonas'))
const Comunidades = lazy(() => import('./paginas/Comunidades'))
const Avisos = lazy(() => import('./paginas/Avisos'))
const Usuarios = lazy(() => import('./paginas/Usuarios'))
const Mensajes = lazy(() => import('./paginas/Mensajes'))
const Protocolos = lazy(() => import('./paginas/Protocolos'))
const Seguimiento = lazy(() => import('./paginas/Seguimiento'))
const Landing = lazy(() => import('./paginas/Landing'))

type Pagina = 'avisos' | 'mensajes' | 'seguimiento' | 'protocolos' | 'zonas' | 'comunidades' | 'usuarios'
const paginas: { id: Pagina; nombre: string; Icon: typeof Bell }[] = [
  { id: 'avisos', nombre: 'Avisos', Icon: Bell },
  { id: 'mensajes', nombre: 'Mensajes', Icon: MessageSquareText },
  { id: 'seguimiento', nombre: 'Seguimiento', Icon: Activity },
  { id: 'protocolos', nombre: 'Protocolos', Icon: BookOpenCheck },
  { id: 'zonas', nombre: 'Zonas de riesgo', Icon: Map },
  { id: 'comunidades', nombre: 'Comunidades', Icon: MapPin },
  { id: 'usuarios', nombre: 'Usuarios', Icon: Users },
]

export default function App() {
  const [sesion, setSesion] = useState<Sesion | null>(leerSesion)
  const [pagina, setPagina] = useState<Pagina>('avisos')
  const [versionAbierta, setVersionAbierta] = useState<string | null>(null)
  // Si la sesión terminó sola (venció o el servidor la rechazó), se vuelve al login con un aviso, no a la landing sin explicación.
  const [vencida] = useState(sesionVencidaPendiente)
  const [verLogin, setVerLogin] = useState(vencida || new URLSearchParams(location.search).has('ingresar'))
  // Cierra la sesión justo cuando caduca el token, aunque el panel siga abierto, en vez de esperar a que una petición falle con 401.
  useEffect(() => {
    if (!sesion) return
    const ms = new Date(sesion.expira).getTime() - Date.now()
    const cerrar = () => { marcarSesionVencida(); guardarSesion(null); setSesion(null) }
    if (ms <= 0) { cerrar(); return }
    const t = window.setTimeout(cerrar, Math.min(ms, 2 ** 31 - 1))
    return () => window.clearTimeout(t)
  }, [sesion])
  // En el celular la navegación se desplaza: la sección activa siempre debe quedar a la vista.
  useEffect(() => {
    document.querySelector('.sidebar nav [aria-current="page"]')?.scrollIntoView({ inline: 'nearest', block: 'nearest' })
  }, [pagina, sesion])
  // La página pública también se puede ver con la sesión abierta (/?publico=1), para comprobar lo que ve la población.
  const consulta = new URLSearchParams(window.location.search)
  if (consulta.has('publico') || consulta.has('aviso')) {
    return <Suspense fallback={<div className="publico"><Esqueleto etiqueta="Cargando avisos" /></div>}><Landing alIngresar={() => { window.location.href = sesion ? '/' : '/?ingresar=1' }} /></Suspense>
  }
  // Sin sesión se ve la página de inicio pública; el panel privado pide iniciar sesión.
  if (!sesion) return verLogin
    ? <Login vencida={vencida} alEntrar={(s) => { guardarSesion(s); setSesion(s) }} alVolver={() => setVerLogin(false)} />
    : <Suspense fallback={<div className="publico"><Esqueleto etiqueta="Cargando avisos" /></div>}><Landing alIngresar={() => setVerLogin(true)} /></Suspense>
  const { usuario } = sesion
  return <div className="app dashboard">
    <a className="saltar" href="#contenido">Saltar al contenido</a>
    <aside className="sidebar">
      <div className="marca"><span className="marca-simbolo"><Waves size={25} aria-hidden="true" /></span>
        <div><h1>SAT Río Piraí</h1><span className="marca-descripcion">ALERTAS TEMPRANAS</span></div></div>
      <span className="nav-label">CENTRO DE GESTIÓN</span>
      <nav aria-label="Navegación principal">{paginas.filter(p => p.id !== 'usuarios' || esAdministrador(usuario.roles)).map(({ id, nombre, Icon }) =>
        <button key={id} aria-current={pagina === id ? 'page' : undefined} className={pagina === id ? 'activo' : ''} onClick={() => { setPagina(id); setVersionAbierta(null) }}>
          <Icon size={19} aria-hidden="true" />{nombre}{pagina === id && <span className="nav-dot" />}
        </button>)}</nav>
      <div className="sidebar-region"><MapPin size={18} aria-hidden="true" /><div>Cuenca del río Piraí<small>Santa Cruz · Bolivia</small></div></div>
      <div className="sidebar-person"><span className="avatar">{usuario.nombreCompleto.slice(0, 1).toUpperCase()}</span>
        <div><strong>{usuario.nombreCompleto}</strong><small>{usuario.roles.join(' · ')}</small></div>
        <button className="icon-button" aria-label="Cerrar sesión" title="Cerrar sesión" onClick={() => { guardarSesion(null); setSesion(null) }}><LogOut size={18} /></button>
      </div>
    </aside>
    <div className="workspace">
      <div className="cabecera-panel">
        <header className="topbar"><div><span className="tenue">Centro de gestión</span><span className="breadcrumb-slash">/</span><strong>{paginas.find(p => p.id === pagina)?.nombre}</strong></div>
          <span className="access-badge"><ShieldCheck size={15} aria-hidden="true" />Acceso {usuario.roles.join(' + ').toLowerCase()}</span></header>
        <EstadoPublico seccion={pagina} />
      </div>
      <main id="contenido" tabIndex={-1}>
        <Suspense fallback={<Esqueleto etiqueta="Cargando sección" />}>
        {pagina === 'avisos' && <Avisos roles={usuario.roles} alPreparar={(id) => { setVersionAbierta(id); setPagina('mensajes') }} />}
        {pagina === 'mensajes' && <Mensajes roles={usuario.roles} versionInicial={versionAbierta} />}
        {pagina === 'seguimiento' && <Seguimiento roles={usuario.roles} />}
        {pagina === 'protocolos' && <Protocolos roles={usuario.roles} />}
        {pagina === 'zonas' && <Zonas roles={usuario.roles} />}
        {pagina === 'comunidades' && <Comunidades roles={usuario.roles} />}
        {pagina === 'usuarios' && esAdministrador(usuario.roles) && <Usuarios usuarioActualId={usuario.id} />}
        </Suspense>
        <footer className="app-footer"><span>SAT Río Piraí · Gestión del riesgo</span><span>Componentes <a href="https://easyui.site/" target="_blank" rel="noreferrer">EasyUI</a> · MIT</span></footer>
      </main>
    </div>
  </div>
}

function Login({ alEntrar, alVolver, vencida }: { alEntrar: (s: Sesion) => void; alVolver: () => void; vencida?: boolean }) {
  const [nombreUsuario, setNombreUsuario] = useState('')
  const [contrasena, setContrasena] = useState('')
  const [mostrar, setMostrar] = useState(false)
  const [error, setError] = useState('')
  const [cargando, setCargando] = useState(false)
  // Registro propio: la cuenta nace con el rol Consulta (solo lectura). Los demás roles los da un Administrador.
  const [registro, setRegistro] = useState(false)
  const [nombreCompleto, setNombreCompleto] = useState('')
  const [email, setEmail] = useState('')
  async function entrar(e: React.FormEvent) {
    e.preventDefault(); setCargando(true); setError('')
    try {
      alEntrar(registro
        ? await api<Sesion>('/api/auth/registro', { method: 'POST', json: { nombreUsuario, nombreCompleto, email, contrasena } })
        : await api<Sesion>('/api/auth/login', { method: 'POST', json: { nombreUsuario, contrasena } }))
    }
    catch (err) { setError(err instanceof Error && err.message !== 'Failed to fetch' ? err.message : 'No se pudo conectar con la API. Intenta de nuevo.') }
    finally { setCargando(false) }
  }
  return <div className="login">
    <div className="login-presentacion">
      <div className="marca"><span className="marca-simbolo"><Waves size={26} aria-hidden="true" /></span><strong>SAT Río Piraí</strong></div>
      <div className="login-story">
        <h2>Anticiparnos<br />hace la <span>diferencia.</span></h2>
        <p>Información oportuna para gestionar el riesgo y cuidar a las comunidades de la cuenca del río Piraí.</p>
        <div className="river-art" aria-hidden="true"><svg viewBox="0 0 560 160" fill="none"><path d="M-20 130C110 150 90 30 210 65S320 160 390 80 480 15 580 40" /><path d="M-20 110C100 130 100 10 220 45S330 140 400 60 490 -5 580 20" /><path d="M-20 150C120 170 80 50 200 85S310 180 380 100 470 35 580 60" /><circle cx="212" cy="65" r="7" /><circle cx="390" cy="80" r="7" /></svg><span><MapPin size={14} /> Cuenca del río Piraí</span></div>
      </div>
      <span className="login-region">Santa Cruz · Bolivia <span>Prevención que nos une.</span></span>
    </div>
    <div className="login-access"><form onSubmit={entrar} className="tarjeta" aria-busy={cargando}>
      <span className="login-lock"><ShieldCheck size={24} aria-hidden="true" /></span>
      <div>{registro
        ? <><h1>Crear cuenta de consulta</h1><p className="tenue">Podrás ver avisos, mensajes y el seguimiento, sin cambiar nada.</p></>
        : <><h1>Bienvenido de nuevo</h1><p className="tenue">Ingresa tus datos para continuar.</p></>}</div>
      {registro && <>
        <label>Nombre completo<input autoComplete="name" value={nombreCompleto} onChange={e => setNombreCompleto(e.target.value)} required /></label>
        <label>Email<input type="email" autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} required /></label>
      </>}
      <label>Usuario<input autoComplete="username" placeholder="Tu nombre de usuario" value={nombreUsuario} onChange={e => setNombreUsuario(e.target.value)} autoFocus required /></label>
      <label>Contraseña<span className="password-field"><input type={mostrar ? 'text' : 'password'} autoComplete={registro ? 'new-password' : 'current-password'} placeholder={registro ? 'Mínimo 8 caracteres, con letras y números' : 'Tu contraseña'} value={contrasena} onChange={e => setContrasena(e.target.value)} required />
        <button type="button" className="icon-button" aria-label={mostrar ? 'Ocultar contraseña' : 'Mostrar contraseña'} aria-pressed={mostrar} onClick={() => setMostrar(!mostrar)}>{mostrar ? <EyeOff size={18} /> : <Eye size={18} />}</button></span></label>
      {vencida && !error && <Alert tone="warning">Tu sesión terminó. Vuelve a entrar para seguir.</Alert>}
      {error && <Alert key={error} tone="danger">{error}</Alert>}
      <button className="primario login-submit" disabled={cargando}>{cargando ? (registro ? 'Creando cuenta…' : 'Entrando…') : (registro ? 'Crear cuenta' : 'Entrar al sistema')}<ArrowRight size={18} aria-hidden="true" /></button>
      <p className="login-note"><ShieldCheck size={14} aria-hidden="true" />{registro ? 'Las cuentas nuevas son de solo lectura.' : 'Operar y aprobar está reservado a personal autorizado.'}</p>
      <button type="button" className="row-link" onClick={() => { setRegistro(!registro); setError('') }}>{registro ? '¿Ya tienes cuenta? Entrar' : '¿No tienes cuenta? Crear una de consulta'}</button>
    </form><p className="login-credit"><button type="button" className="row-link" onClick={alVolver}><ArrowLeft size={14} aria-hidden="true" />Volver a los avisos públicos</button></p></div>
  </div>
}
