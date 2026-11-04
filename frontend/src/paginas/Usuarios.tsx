import { useEffect, useState } from 'react'
import { api, fecha, type Rol, type Usuario } from '../api'

const ROLES: Rol[] = ['Administrador', 'Operador', 'Aprobador', 'Consulta']
const DESCRIPCION: Record<Rol, string> = {
  Administrador: 'Gestiona usuarios y toda la configuración.',
  Operador: 'Registra avisos y mantiene zonas, comunidades y contactos.',
  Aprobador: 'Revisa y aprueba mensajes antes de difundirlos (Sprint 2).',
  Consulta: 'Solo puede ver la información.',
}
const vacio = { nombreUsuario: '', nombreCompleto: '', email: '', rol: 'Operador' as Rol, contrasena: '' }

/** RF-13: usuarios y permisos. Solo para Administrador. */
export default function Usuarios({ usuarioActualId }: { usuarioActualId: string }) {
  const [usuarios, setUsuarios] = useState<Usuario[]>([])
  const [nuevo, setNuevo] = useState<typeof vacio | null>(null)
  const [mensaje, setMensaje] = useState<{ ok: boolean; texto: string } | null>(null)

  const cargar = () => api<Usuario[]>('/api/usuarios').then(setUsuarios)
  useEffect(() => { cargar() }, [])
  const fallo = (e: unknown) => setMensaje({ ok: false, texto: (e as Error).message })

  async function crear(e: React.FormEvent) {
    e.preventDefault()
    try { await api('/api/usuarios', { method: 'POST', json: nuevo }); setNuevo(null); setMensaje({ ok: true, texto: 'Usuario creado.' }); cargar() }
    catch (err) { fallo(err) }
  }

  async function actualizar(u: Usuario, cambios: Partial<Usuario>) {
    const v = { ...u, ...cambios }
    try { await api(`/api/usuarios/${u.id}`, { method: 'PUT', json: { nombreCompleto: v.nombreCompleto, email: v.email, rol: v.rol, activo: v.activo } }); setMensaje(null); cargar() }
    catch (err) { fallo(err) }
  }

  async function restablecer(u: Usuario) {
    const nueva = prompt(`Nueva contraseña para ${u.nombreUsuario} (mínimo 8 caracteres, con letras y números):`)
    if (!nueva) return
    try { await api(`/api/usuarios/${u.id}/contrasena`, { method: 'PUT', json: { contrasenaNueva: nueva } }); setMensaje({ ok: true, texto: 'Contraseña actualizada.' }) }
    catch (err) { fallo(err) }
  }

  return (
    <section>
      <div className="barra">
        <h2>Usuarios y permisos</h2>
        <button className="primario" onClick={() => setNuevo({ ...vacio })}>Nuevo usuario</button>
      </div>
      {mensaje && <p className={mensaje.ok ? 'exito' : 'error'}>{mensaje.texto}</p>}

      {nuevo && (
        <form className="tarjeta" onSubmit={crear}>
          <h3>Nuevo usuario</h3>
          <div className="fila">
            <label>Usuario<input required value={nuevo.nombreUsuario} onChange={(e) => setNuevo({ ...nuevo, nombreUsuario: e.target.value })} /></label>
            <label>Nombre completo<input required value={nuevo.nombreCompleto} onChange={(e) => setNuevo({ ...nuevo, nombreCompleto: e.target.value })} /></label>
          </div>
          <div className="fila">
            <label>Email<input type="email" required value={nuevo.email} onChange={(e) => setNuevo({ ...nuevo, email: e.target.value })} /></label>
            <label>Rol<select value={nuevo.rol} onChange={(e) => setNuevo({ ...nuevo, rol: e.target.value as Rol })}>{ROLES.map((r) => <option key={r}>{r}</option>)}</select></label>
            <label>Contraseña<input type="password" required minLength={8} value={nuevo.contrasena} onChange={(e) => setNuevo({ ...nuevo, contrasena: e.target.value })} /></label>
          </div>
          <p className="tenue">{DESCRIPCION[nuevo.rol]}</p>
          <div className="acciones"><button className="primario">Crear</button><button type="button" onClick={() => setNuevo(null)}>Cancelar</button></div>
        </form>
      )}

      <table>
        <thead><tr><th>Usuario</th><th>Nombre</th><th>Rol</th><th>Activo</th><th>Último acceso</th><th></th></tr></thead>
        <tbody>
          {usuarios.map((u) => (
            <tr key={u.id}>
              <td>{u.nombreUsuario}{u.id === usuarioActualId && <span className="tenue"> (tú)</span>}</td>
              <td>{u.nombreCompleto}<div className="tenue">{u.email}</div></td>
              <td><select value={u.rol} title={DESCRIPCION[u.rol]} onChange={(e) => actualizar(u, { rol: e.target.value as Rol })}>{ROLES.map((r) => <option key={r}>{r}</option>)}</select></td>
              <td><input type="checkbox" checked={u.activo} onChange={(e) => actualizar(u, { activo: e.target.checked })} /></td>
              <td>{fecha(u.ultimoAcceso)}</td>
              <td><button onClick={() => restablecer(u)}>Cambiar contraseña</button></td>
            </tr>
          ))}
        </tbody>
      </table>

      <h3>Matriz de permisos</h3>
      <table>
        <thead><tr><th>Rol</th><th>Ver datos</th><th>Editar zonas, comunidades y avisos</th><th>Gestionar usuarios</th></tr></thead>
        <tbody>
          {ROLES.map((r) => (
            <tr key={r}><td>{r}</td><td>✔</td><td>{r === 'Administrador' || r === 'Operador' ? '✔' : '—'}</td><td>{r === 'Administrador' ? '✔' : '—'}</td></tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
