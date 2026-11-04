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
      <header>
        <h1>SAT Río Piraí</h1>
        <nav>
          {paginas.map(([id, nombre]) => (
            <button key={id} className={pagina === id ? 'activo' : ''} onClick={() => setPagina(id)}>{nombre}</button>
          ))}
        </nav>
        <div className="usuario">
          <span>{usuario.nombreCompleto} · <b>{usuario.rol}</b></span>
          <button onClick={() => { guardarSesion(null); setSesion(null) }}>Salir</button>
        </div>
      </header>
      <main>
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
      <form onSubmit={entrar} className="tarjeta">
        <h1>Sistema de Alertas Tempranas</h1>
        <p className="tenue">Río Piraí · Santa Cruz</p>
        <label>Usuario<input value={nombreUsuario} onChange={(e) => setNombreUsuario(e.target.value)} autoFocus required /></label>
        <label>Contraseña<input type="password" value={contrasena} onChange={(e) => setContrasena(e.target.value)} required /></label>
        {error && <p className="error">{error}</p>}
        <button className="primario" disabled={cargando}>{cargando ? 'Entrando…' : 'Entrar'}</button>
      </form>
    </div>
  )
}
