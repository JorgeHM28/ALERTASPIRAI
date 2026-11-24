import { useEffect, useState } from 'react'
import { BookOpenCheck, Plus, RefreshCw } from 'lucide-react'
import { api, esAdministrador, soloFecha, type Protocolo, type Rol } from '../api'
import { Alert } from '../components/ui/Alert'

const VACIO = { titulo: '', instruccion: '', fuente: '', referencia: '', fechaDocumento: '' }

/** RF-05: catálogo de protocolos oficiales. Las instrucciones de cada mensaje salen de uno de ellos, con su fuente y fecha. */
export default function Protocolos({ roles }: { roles: Rol[] }) {
  const admin = esAdministrador(roles)
  const [lista, setLista] = useState<Protocolo[]>([])
  const [cargando, setCargando] = useState(true)
  const [aviso, setAviso] = useState<{ ok: boolean; texto: string } | null>(null)
  const [editando, setEditando] = useState<string | 'nuevo' | null>(null)
  const [form, setForm] = useState(VACIO)

  const cargar = () => {
    setCargando(true)
    return api<Protocolo[]>('/api/protocolos').then(setLista)
      .catch((e: unknown) => setAviso({ ok: false, texto: e instanceof Error ? e.message : 'No se pudieron cargar los protocolos.' }))
      .finally(() => setCargando(false))
  }
  useEffect(() => { cargar() }, [])

  function abrir(p?: Protocolo) {
    setAviso(null)
    setEditando(p ? p.id : 'nuevo')
    setForm(p ? { titulo: p.titulo, instruccion: p.instruccion, fuente: p.fuente, referencia: p.referencia, fechaDocumento: p.fechaDocumento } : VACIO)
  }

  async function guardar(e: { preventDefault: () => void }) {
    e.preventDefault()
    try {
      if (editando === 'nuevo') await api('/api/protocolos', { method: 'POST', json: form })
      else await api(`/api/protocolos/${editando}`, { method: 'PUT', json: form })
      setAviso({ ok: true, texto: editando === 'nuevo' ? 'Protocolo agregado al catálogo.' : 'Protocolo actualizado. Las versiones que ya lo citan conservan el texto que copiaron.' })
      setEditando(null); cargar()
    } catch (err) { setAviso({ ok: false, texto: err instanceof Error ? err.message : 'No se pudo guardar el protocolo.' }) }
  }

  async function cambiarEstado(p: Protocolo) {
    try {
      await api(`/api/protocolos/${p.id}/${p.activo ? 'desactivar' : 'activar'}`, { method: 'POST' })
      setAviso({ ok: true, texto: p.activo ? 'Protocolo desactivado: ya no se puede elegir para mensajes nuevos.' : 'Protocolo activado.' }); cargar()
    } catch (err) { setAviso({ ok: false, texto: err instanceof Error ? err.message : 'No se pudo cambiar el estado.' }) }
  }

  return (
    <section>
      <div className="barra">
        <div><h2>Protocolos oficiales</h2><p className="tenue">Instrucciones oficiales para la población, con la institución, el documento y la fecha de donde salen. Cada mensaje toma sus instrucciones de uno de ellos.</p></div>
        {admin && <button className="primario" onClick={() => abrir()}><Plus size={16} aria-hidden="true" />Nuevo protocolo</button>}
      </div>
      {aviso && <Alert key={aviso.texto} tone={aviso.ok ? 'success' : 'danger'} dismissible>{aviso.texto}</Alert>}

      {editando && admin && (
        <form className="tarjeta interior" onSubmit={guardar}>
          <b>{editando === 'nuevo' ? 'Nuevo protocolo' : 'Editar protocolo'}</b>
          <label>Título<input required maxLength={200} value={form.titulo} onChange={e => setForm({ ...form, titulo: e.target.value })} /></label>
          <label>Instrucciones oficiales<textarea required maxLength={4000} value={form.instruccion} onChange={e => setForm({ ...form, instruccion: e.target.value })} /></label>
          <label>Institución que lo emite<input required maxLength={150} value={form.fuente} onChange={e => setForm({ ...form, fuente: e.target.value })} placeholder="Ej.: COED Santa Cruz" /></label>
          <label>Documento de referencia<input required maxLength={200} value={form.referencia} onChange={e => setForm({ ...form, referencia: e.target.value })} placeholder="Ej.: Plan de contingencia 01/2026" /></label>
          <label>Fecha del documento<input required type="date" value={form.fechaDocumento} onChange={e => setForm({ ...form, fechaDocumento: e.target.value })} /></label>
          <div className="acciones">
            <button type="button" onClick={() => setEditando(null)}>Cancelar</button>
            <button className="primario">Guardar</button>
          </div>
        </form>
      )}

      <div className="list-panel">
        <div className="list-heading"><h3>Catálogo <span className="count-chip">{cargando ? '—' : lista.length}</span></h3><button className="icon-button" onClick={cargar} disabled={cargando} aria-label="Actualizar protocolos" title="Actualizar protocolos"><RefreshCw size={17} /></button></div>
        <div className="table-scroll" aria-busy={cargando}><table>
          <thead><tr><th>Protocolo</th><th>Fuente</th><th>Mensajes</th><th>Estado</th>{admin && <th>Acciones</th>}</tr></thead>
          <tbody>
            {!cargando && lista.map(p => (
              <tr key={p.id} className={p.activo ? '' : 'tachado'}>
                <td><b>{p.titulo}</b><div className="tenue">{p.instruccion}</div></td>
                <td>{p.fuente}<div className="tenue">{p.referencia} · {soloFecha(p.fechaDocumento)}</div></td>
                <td>{p.versiones}</td>
                <td>{p.activo ? 'Activo' : 'Desactivado'}</td>
                {admin && <td><div className="acciones"><button onClick={() => abrir(p)}>Editar</button><button onClick={() => cambiarEstado(p)}>{p.activo ? 'Desactivar' : 'Activar'}</button></div></td>}
              </tr>
            ))}
            {!cargando && lista.length === 0 && <tr><td colSpan={admin ? 5 : 4}><div className="table-empty"><BookOpenCheck size={28} /><strong>Todavía no hay protocolos</strong><span>{admin ? 'Agrega el primero con «Nuevo protocolo».' : 'Un administrador debe cargarlos.'}</span></div></td></tr>}
          </tbody>
        </table></div>
      </div>
    </section>
  )
}
