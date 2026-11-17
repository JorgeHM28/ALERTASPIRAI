import { useEffect, useState } from 'react'
import { Eye, EyeOff } from 'lucide-react'
import { api, fecha, type Permiso, type RegistroBitacora, type Rol, type Usuario } from '../api'
import { Alert } from '../components/ui/Alert'
import { Dialogo } from '../components/ui/Dialogo'
import { useConfirmar } from '../components/ui/useConfirmar'
import { useEnfocarAlAbrir } from '../components/ui/useEnfocarAlAbrir'
import { ReglasContrasena } from '../components/ReglasContrasena'
import { reglasContrasena } from '../contrasena'

const ROLES: Rol[] = ['Administrador', 'Operador', 'Aprobador', 'Consulta']
const DESCRIPCION: Record<Rol, string> = {
  Administrador: 'Gestiona usuarios y toda la configuración.',
  Operador: 'Registra avisos y mantiene zonas, comunidades y contactos.',
  Aprobador: 'Revisa y aprueba mensajes antes de difundirlos.',
  Consulta: 'Solo puede ver la información.',
}
const PERMISOS: { id: Permiso; nombre: string }[] = [
  { id: 'Lectura', nombre: 'Ver datos' },
  { id: 'Edicion', nombre: 'Editar zonas, comunidades y avisos' },
  { id: 'Aprobacion', nombre: 'Aprobar mensajes' },
  { id: 'Administracion', nombre: 'Gestionar usuarios' },
]
const ACCION: Record<string, string> = {
  Alta: 'Alta del usuario', RolAsignado: 'Rol asignado', RolRevocado: 'Rol revocado', Activado: 'Activado',
  Desactivado: 'Desactivado', DatosActualizados: 'Datos actualizados', ContrasenaRestablecida: 'Contraseña restablecida',
}
const vacio = { nombreUsuario: '', nombreCompleto: '', email: '', roles: ['Operador'] as Rol[], contrasena: '' }

/** Casillas para elegir uno o varios roles. */
function SelectorRoles({ valor, alCambiar, deshabilitado }: { valor: Rol[]; alCambiar: (r: Rol[]) => void; deshabilitado?: boolean }) {
  return (
    <div className="chips" role="group" aria-label="Roles">
      {ROLES.map((r) => (
        <label key={r} className="check" title={DESCRIPCION[r]}>
          <input type="checkbox" disabled={deshabilitado} checked={valor.includes(r)}
            onChange={(e) => alCambiar(e.target.checked ? [...valor, r] : valor.filter((x) => x !== r))} /> {r}
        </label>
      ))}
    </div>
  )
}

/** RF-13: usuarios, sus roles (uno o varios) y la bitácora de cambios. Solo para Administrador. */
export default function Usuarios({ usuarioActualId }: { usuarioActualId: string }) {
  const [usuarios, setUsuarios] = useState<Usuario[]>([])
  const [nuevo, setNuevo] = useState<typeof vacio | null>(null)
  const [editando, setEditando] = useState<{ id: string; roles: Rol[] } | null>(null)
  const [bitacora, setBitacora] = useState<{ usuario: Usuario; registros: RegistroBitacora[] } | null>(null)
  const [mensaje, setMensaje] = useState<{ ok: boolean; texto: string } | null>(null)
  const { confirmar, dialogo } = useConfirmar()
  const { panel: panelBitacora, titulo: tituloBitacora } = useEnfocarAlAbrir<HTMLDivElement>(bitacora?.usuario.id)
  const [reiniciando, setReiniciando] = useState<Usuario | null>(null)
  const [nuevaContrasena, setNuevaContrasena] = useState('')
  const [verContrasena, setVerContrasena] = useState(false)
  const contrasenaValida = reglasContrasena(nuevaContrasena).every(r => r.ok)

  const cargar = () => api<Usuario[]>('/api/usuarios').then(setUsuarios)
  useEffect(() => { cargar() }, [])
  const fallo = (e: unknown) => setMensaje({ ok: false, texto: (e as Error).message })

  async function crear(e: React.FormEvent) {
    e.preventDefault()
    if (!nuevo?.roles.length) { setMensaje({ ok: false, texto: 'Elige al menos un rol.' }); return }
    try { await api('/api/usuarios', { method: 'POST', json: nuevo }); setNuevo(null); setMensaje({ ok: true, texto: 'Usuario creado.' }); cargar() }
    catch (err) { fallo(err) }
  }

  async function actualizar(u: Usuario, cambios: Partial<Usuario>) {
    const v = { ...u, ...cambios }
    try {
      await api(`/api/usuarios/${u.id}`, { method: 'PUT', json: { nombreCompleto: v.nombreCompleto, email: v.email, roles: v.roles, activo: v.activo } })
      setMensaje({ ok: true, texto: 'Cambio guardado y registrado en la bitácora.' }); setEditando(null); cargar()
      if (bitacora?.usuario.id === u.id) verBitacora(v)
    } catch (err) { fallo(err) }
  }

  function pedirContrasena(u: Usuario) { setNuevaContrasena(''); setVerContrasena(false); setReiniciando(u) }

  async function restablecer() {
    if (!reiniciando) return
    const u = reiniciando
    try {
      await api(`/api/usuarios/${u.id}/contrasena`, { method: 'PUT', json: { contrasenaNueva: nuevaContrasena } })
      setReiniciando(null); setNuevaContrasena(''); setMensaje({ ok: true, texto: `Contraseña de ${u.nombreUsuario} actualizada.` })
    } catch (err) { setReiniciando(null); fallo(err) }
  }

  /** Quitar el acceso cambia quién puede decidir: se dice qué pierde esa persona antes de aplicar el cambio. */
  async function cambiarActivo(u: Usuario, activo: boolean) {
    if (!activo) {
      const pierde = PERMISOS.filter(p => p.id !== 'Lectura' && u.roles.some(r => TIENE[r].includes(p.id))).map(p => p.nombre.toLowerCase())
      const ok = await confirmar({
        titulo: `¿Desactivar a ${u.nombreUsuario}?`,
        texto: `No podrá iniciar sesión${pierde.length ? `, ni ${pierde.join(', ni ')}` : ''}. Sus roles y su historial se conservan y puedes activarlo de nuevo cuando quieras.`,
        etiqueta: 'Desactivar usuario', peligro: true,
      })
      if (!ok) return
    }
    actualizar(u, { activo })
  }

  async function verBitacora(u: Usuario) {
    try { setBitacora({ usuario: u, registros: await api<RegistroBitacora[]>(`/api/usuarios/${u.id}/bitacora`) }) }
    catch (err) { fallo(err) }
  }

  return (
    <section>
      <div className="barra">
        <h2>Usuarios y permisos</h2>
        <button className="primario" onClick={() => setNuevo({ ...vacio })}>Nuevo usuario</button>
      </div>
      {mensaje && <Alert key={mensaje.texto} tone={mensaje.ok ? 'success' : 'danger'} dismissible>{mensaje.texto}</Alert>}

      {nuevo && (
        <form className="tarjeta" onSubmit={crear}>
          <h3>Nuevo usuario</h3>
          <div className="fila">
            <label>Usuario<input required value={nuevo.nombreUsuario} onChange={(e) => setNuevo({ ...nuevo, nombreUsuario: e.target.value })} /></label>
            <label>Nombre completo<input required value={nuevo.nombreCompleto} onChange={(e) => setNuevo({ ...nuevo, nombreCompleto: e.target.value })} /></label>
          </div>
          <div className="fila">
            <label>Email<input type="email" required value={nuevo.email} onChange={(e) => setNuevo({ ...nuevo, email: e.target.value })} /></label>
            <div><label>Contraseña<input type="password" required autoComplete="new-password" aria-describedby="reglas-nueva" value={nuevo.contrasena} onChange={(e) => setNuevo({ ...nuevo, contrasena: e.target.value })} /></label>
              <ReglasContrasena id="reglas-nueva" valor={nuevo.contrasena} /></div>
          </div>
          <b>Roles</b>
          <SelectorRoles valor={nuevo.roles} alCambiar={(roles) => setNuevo({ ...nuevo, roles })} />
          <p className="tenue">{nuevo.roles.map((r) => `${r}: ${DESCRIPCION[r]}`).join(' ') || 'Elige al menos un rol. Puedes marcar varios.'}</p>
          <div className="acciones"><button className="primario" disabled={!reglasContrasena(nuevo.contrasena).every(r => r.ok)}>Crear</button><button type="button" onClick={() => setNuevo(null)}>Cancelar</button></div>
        </form>
      )}

      <div className="table-scroll"><table>
        <thead><tr><th>Usuario</th><th>Nombre</th><th>Roles</th><th>Activo</th><th>Último acceso</th><th></th></tr></thead>
        <tbody>
          {usuarios.map((u) => (
            <tr key={u.id}>
              <td>{u.nombreUsuario}{u.id === usuarioActualId && <span className="tenue"> (tú)</span>}</td>
              <td>{u.nombreCompleto}<div className="tenue">{u.email}</div></td>
              <td>
                {editando?.id === u.id ? (
                  <>
                    <SelectorRoles valor={editando.roles} alCambiar={(roles) => setEditando({ id: u.id, roles })} />
                    <div className="acciones">
                      <button className="primario" disabled={!editando.roles.length} onClick={() => actualizar(u, { roles: editando.roles })}>Guardar roles</button>
                      <button onClick={() => setEditando(null)}>Cancelar</button>
                    </div>
                  </>
                ) : (
                  <>
                    {u.roles.map((r) => <span key={r} className="etiqueta gris" style={{ marginRight: 4 }} title={DESCRIPCION[r]}>{r}</span>)}
                    <button className="row-link" style={{ display: 'block' }} onClick={() => setEditando({ id: u.id, roles: u.roles })}>Cambiar</button>
                  </>
                )}
              </td>
              <td><input type="checkbox" aria-label={`${u.nombreUsuario} activo`} checked={u.activo} onChange={(e) => cambiarActivo(u, e.target.checked)} /></td>
              <td>{fecha(u.ultimoAcceso)}</td>
              <td><div className="acciones"><button onClick={() => verBitacora(u)}>Bitácora</button><button onClick={() => pedirContrasena(u)}>Cambiar contraseña</button></div></td>
            </tr>
          ))}
        </tbody>
      </table></div>

      {bitacora && (
        <div className="tarjeta" ref={panelBitacora}>
          <div className="barra"><h3 ref={tituloBitacora} tabIndex={-1}>Bitácora de {bitacora.usuario.nombreUsuario}</h3><button onClick={() => setBitacora(null)}>Cerrar</button></div>
          <p className="tenue">Cada asignación o cambio, con el administrador responsable y la fecha. No se puede editar.</p>
          <div className="table-scroll"><table>
            <thead><tr><th>Fecha</th><th>Acción</th><th>Rol</th><th>Administrador</th><th>Detalle</th></tr></thead>
            <tbody>
              {bitacora.registros.map((r) => (
                <tr key={r.id}><td>{fecha(r.fecha)}</td><td>{ACCION[r.accion] ?? r.accion}</td><td>{r.rol ?? '—'}</td>
                  <td>{r.administrador ?? 'sistema'}</td><td className="tenue">{r.detalle ?? ''}</td></tr>
              ))}
              {bitacora.registros.length === 0 && <tr><td colSpan={5} className="tenue">Sin registros.</td></tr>}
            </tbody>
          </table></div>
        </div>
      )}

      {/* Referencia que se consulta de vez en cuando (al asignar roles): plegada, como la ayuda de Avisos y Mensajes. */}
      <details className="ayuda">
        <summary>¿Qué puede hacer cada rol?</summary>
        <div className="ayuda-cuerpo">
          <p>Un usuario con varios roles tiene la suma de sus permisos.</p>
          <div className="table-scroll"><table>
            <thead><tr><th>Rol</th>{PERMISOS.map((p) => <th key={p.id}>{p.nombre}</th>)}</tr></thead>
            <tbody>
              {ROLES.map((r) => (
                <tr key={r}><td>{r}</td>{PERMISOS.map((p) => <td key={p.id}>{TIENE[r].includes(p.id)
                  ? <><span aria-hidden="true">✔</span><span className="solo-lector">Sí</span></>
                  : <><span aria-hidden="true">—</span><span className="solo-lector">No</span></>}</td>)}</tr>
              ))}
            </tbody>
          </table></div>
        </div>
      </details>

      <Dialogo abierto={!!reiniciando} titulo={`Cambiar la contraseña de ${reiniciando?.nombreUsuario ?? ''}`} alCerrar={() => setReiniciando(null)}
        pie={<>
          <button onClick={() => setReiniciando(null)}>Cancelar</button>
          <button className="primario" disabled={!contrasenaValida} onClick={restablecer}>Cambiar contraseña</button>
        </>}>
        <p>La persona tendrá que usar la nueva contraseña en su próximo inicio de sesión.</p>
        <label>Contraseña nueva
          <span className="password-field">
            <input autoFocus type={verContrasena ? 'text' : 'password'} autoComplete="new-password" value={nuevaContrasena}
              onChange={e => setNuevaContrasena(e.target.value)} aria-describedby="reglas-contrasena" />
            <button type="button" className="icon-button" aria-label={verContrasena ? 'Ocultar contraseña' : 'Mostrar contraseña'} aria-pressed={verContrasena}
              onClick={() => setVerContrasena(!verContrasena)}>{verContrasena ? <EyeOff size={18} /> : <Eye size={18} />}</button>
          </span>
        </label>
        <ReglasContrasena id="reglas-contrasena" valor={nuevaContrasena} />
      </Dialogo>
      {dialogo}
    </section>
  )
}

const TIENE: Record<Rol, Permiso[]> = {
  Administrador: ['Lectura', 'Edicion', 'Aprobacion', 'Administracion'],
  Operador: ['Lectura', 'Edicion'],
  Aprobador: ['Lectura', 'Aprobacion'],
  Consulta: ['Lectura'],
}
