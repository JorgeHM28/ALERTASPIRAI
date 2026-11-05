import { useState } from 'react'
import 'leaflet/dist/leaflet.css'
import './index.css'
import { api, guardarSesion, leerSesion, type Sesion } from './api'
import Zonas from './paginas/Zonas'
import Comunidades from './paginas/Comunidades'
import Avisos from './paginas/Avisos'
import Usuarios from './paginas/Usuarios'

type Pagina = 'avisos' | 'zonas' | 'comunidades' | 'usuarios'

export default function App() {
  const [sesion, setSesion] = useState<Sesion | null>(leerSesion)
  const [pagina, setPagina] = useState<Pagina>('avisos')

  if (!sesion) return <Login alEntrar={(s) => { guardarSesion(s); setSesion(s) }} />

  const { usuario } = sesion
  const paginas: [Pagina, string][] = [
    ['avisos', 'Avisos'], ['zonas', 'Zonas'], ['comunidades', 'Comunidades'],
    ...(usuario.rol === 'Administrador' ? [['usuarios', 'Usuarios'] as [Pagina, string]] : []),
  ]

  return (
    <div className="app">
      <a className="saltar" href="#contenido">Saltar al contenido</a>
      <header>
        <div className="marca">
          <span className="marca-simbolo" aria-hidden="true">≈</span>
          <div><h1>SAT Río Piraí</h1><span className="marca-descripcion">Sistema de alertas tempranas</span></div>
        </div>
        <nav aria-label="Navegación principal">
          {paginas.map(([id, nombre]) => (
            <button key={id} aria-current={pagina === id ? "page" : undefined} className={pagina === id ? 'activo' : ''} onClick={() => setPagina(id)}>{nombre}</button>
          ))}
        </nav>
        <div className="usuario">
          <span>{usuario.nombreCompleto} · <b>{usuario.rol}</b></span>
          <button onClick={() => { guardarSesion(null); setSesion(null) }}>Salir</button>
        </div>
      </header>
      <main id="contenido">
        <div className="contexto">CENTRO DE GESTIÓN <span>Río Piraí · Santa Cruz</span></div>
        {pagina === 'avisos' && <Avisos rol={usuario.rol} />}
        {pagina === 'zonas' && <Zonas rol={usuario.rol} />}
        {pagina === 'comunidades' && <Comunidades rol={usuario.rol} />}
        {pagina === 'usuarios' && <Usuarios usuarioActualId={usuario.id} />}
      </main>
    </div>
  )
}

function Login({ alEntrar }: { alEntrar: (s: Sesion) => void }) {
  const [nombreUsuario, setNombreUsuario] = useState('')
  const [contrasena, setContrasena] = useState('')
  const [error, setError] = useState('')
  const [cargando, setCargando] = useState(false)

  async function entrar(e: React.FormEvent) {
    e.preventDefault()
    setCargando(true); setError('')
    try {
      alEntrar(await api<Sesion>('/api/auth/login', { method: 'POST', json: { nombreUsuario, contrasena } }))
    } catch (err) {
      setError((err as Error).message === 'Failed to fetch' ? 'No se pudo conectar con la API.' : (err as Error).message)
    } finally { setCargando(false) }
  }

  return (
    <div className="login">
      <div className="login-presentacion">
        <span className="eyebrow">SAT / RÍO PIRAÍ</span>
        <h2>Información oportuna.<br />Comunidades preparadas.</h2>
        <p>Gestiona avisos, zonas de riesgo y contactos comunitarios desde un solo lugar.</p>
        <span className="login-region">Santa Cruz · Bolivia</span>
      </div>
      <form onSubmit={entrar} className="tarjeta">
        <span className="eyebrow">ACCESO AL SISTEMA</span>
        <h1>Bienvenido de nuevo</h1>
        <p className="tenue">Ingresa con tu cuenta para continuar.</p>
        <label>Usuario<input autoComplete="username" placeholder="Tu nombre de usuario" value={nombreUsuario} onChange={(e) => setNombreUsuario(e.target.value)} autoFocus required /></label>
        <label>Contraseña<input type="password" autoComplete="current-password" placeholder="Tu contraseña" value={contrasena} onChange={(e) => setContrasena(e.target.value)} required /></label>
        {error && <p className="error" role="alert">{error}</p>}
        <button className="primario" disabled={cargando}>{cargando ? 'Entrando…' : 'Entrar'}</button>
      </form>
    </div>
  )
}
