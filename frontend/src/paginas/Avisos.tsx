import { useEffect, useState } from 'react'
import { api, fecha, puedeEditar, type AvisoDetalle, type AvisoResumen, type Rol, type Zona } from '../api'
import Mapa from './Mapa'

const URGENCIAS = ['Immediate', 'Expected', 'Future', 'Past', 'Unknown']
const SEVERIDADES = ['Extreme', 'Severe', 'Moderate', 'Minor', 'Unknown']
const CERTEZAS = ['Observed', 'Likely', 'Possible', 'Unlikely', 'Unknown']
const COLOR_SEVERIDAD: Record<string, string> = { Extreme: '#c62828', Severe: '#ef6c00', Moderate: '#f9a825', Minor: '#2e7d32', Unknown: '#757575' }

type Modo = 'lista' | 'importar' | 'manual'

/** RF-01: registro de avisos e importación CAP. */
export default function Avisos({ rol }: { rol: Rol }) {
  const [avisos, setAvisos] = useState<AvisoResumen[]>([])
  const [detalle, setDetalle] = useState<AvisoDetalle | null>(null)
  const [modo, setModo] = useState<Modo>('lista')
  const [aviso, setAviso] = useState<{ ok: boolean; texto: string } | null>(null)
  const editar = puedeEditar(rol)

  const cargar = () => api<AvisoResumen[]>('/api/avisos').then(setAvisos)
  useEffect(() => { cargar() }, [])

  const registrado = (a: AvisoResumen) => {
    setAviso({ ok: true, texto: `Aviso "${a.evento}" registrado (${a.identificadorCap}).` })
    setModo('lista'); cargar(); api<AvisoDetalle>(`/api/avisos/${a.id}`).then(setDetalle)
  }

  async function descartar(id: string) {
    if (!confirm('¿Descartar este aviso?')) return
    await api(`/api/avisos/${id}/descartar`, { method: 'POST' }); cargar(); setDetalle(null)
  }

  return (
    <section>
      <div className="barra">
        <h2>Avisos</h2>
        {editar && (
          <div className="acciones">
            <button className={modo === 'importar' ? 'primario' : ''} onClick={() => setModo('importar')}>Importar CAP</button>
            <button className={modo === 'manual' ? 'primario' : ''} onClick={() => setModo('manual')}>Registrar a mano</button>
          </div>
        )}
      </div>
      {aviso && <p className={aviso.ok ? 'exito' : 'error'}>{aviso.texto}</p>}

      {modo === 'importar' && <ImportarCap alRegistrar={registrado} alCancelar={() => setModo('lista')} />}
      {modo === 'manual' && <RegistroManual alRegistrar={registrado} alCancelar={() => setModo('lista')} />}

      <div className="dos-columnas">
        <table>
          <thead><tr><th>Enviado</th><th>Evento</th><th>Severidad</th><th>Origen</th><th>Estado</th></tr></thead>
          <tbody>
            {avisos.map((a) => (
              <tr key={a.id} className={`clicable ${a.estado === 'Descartado' ? 'tachado' : ''}`}
                onClick={() => api<AvisoDetalle>(`/api/avisos/${a.id}`).then(setDetalle)}>
                <td>{fecha(a.enviado)}</td>
                <td>{a.titular ?? a.evento}<div className="tenue">{a.remitente}</div></td>
                <td><span className="etiqueta" style={{ background: COLOR_SEVERIDAD[a.severidad] }}>{a.severidad}</span></td>
                <td>{a.formato}</td><td>{a.estado}</td>
              </tr>
            ))}
            {avisos.length === 0 && <tr><td colSpan={5} className="tenue">Todavía no hay avisos.</td></tr>}
          </tbody>
        </table>

        {detalle ? (
          <div className="tarjeta">
            <h3>{detalle.resumen.titular ?? detalle.resumen.evento}</h3>
            <p className="tenue">{detalle.resumen.identificadorCap} · {detalle.resumen.remitente}</p>
            <dl>
              <dt>Urgencia / severidad / certeza</dt><dd>{detalle.resumen.urgencia} · {detalle.resumen.severidad} · {detalle.resumen.certeza}</dd>
              <dt>Vigencia</dt><dd>{fecha(detalle.vigente)} → {fecha(detalle.resumen.expira)}</dd>
              <dt>Área</dt><dd>{detalle.resumen.descripcionArea ?? '—'}</dd>
              {detalle.descripcion && <><dt>Descripción</dt><dd>{detalle.descripcion}</dd></>}
              {detalle.instruccion && <><dt>Instrucción</dt><dd>{detalle.instruccion}</dd></>}
            </dl>
            {detalle.area && <Mapa alto={220} capas={[{ clave: detalle.resumen.id, geometria: detalle.area, color: COLOR_SEVERIDAD[detalle.resumen.severidad], etiqueta: detalle.resumen.descripcionArea ?? '' }]} />}
            <details><summary>Mensaje CAP 1.2 (XML)</summary><pre>{detalle.xmlCap}</pre></details>
            <div className="acciones">
              <button onClick={() => {
                const url = URL.createObjectURL(new Blob([detalle.xmlCap], { type: 'application/xml' }))
                Object.assign(document.createElement('a'), { href: url, download: `${detalle.resumen.identificadorCap}.xml` }).click()
                URL.revokeObjectURL(url)
              }}>Descargar CAP</button>
              {editar && detalle.resumen.estado !== 'Descartado' && <button className="peligro" onClick={() => descartar(detalle.resumen.id)}>Descartar</button>}
            </div>
          </div>
        ) : <p className="tenue">Elige un aviso para ver el detalle y su mensaje CAP.</p>}
      </div>
    </section>
  )
}

function ImportarCap({ alRegistrar, alCancelar }: { alRegistrar: (a: AvisoResumen) => void; alCancelar: () => void }) {
  const [contenido, setContenido] = useState('')
  const [error, setError] = useState('')

  async function enviar() {
    setError('')
    const tipo = contenido.trimStart().startsWith('{') ? 'application/json' : 'application/xml'
    try { alRegistrar(await api<AvisoResumen>('/api/avisos/importar', { method: 'POST', body: contenido, headers: { 'Content-Type': tipo } })) }
    catch (e) { setError((e as Error).message) }
  }

  return (
    <div className="tarjeta">
      <h3>Importar mensaje CAP 1.2</h3>
      <p className="tenue">Pega el XML o JSON, o elige un archivo. El sistema detecta el formato y valida el mensaje contra el estándar.</p>
      <input type="file" accept=".xml,.json,.cap" onChange={async (e) => { const f = e.target.files?.[0]; if (f) setContenido(await f.text()) }} />
      <textarea rows={10} value={contenido} onChange={(e) => setContenido(e.target.value)} placeholder='<alert xmlns="urn:oasis:names:tc:emergency:cap:1.2">…' className="codigo" />
      {error && <p className="error">{error}</p>}
      <div className="acciones">
        <button className="primario" disabled={!contenido.trim()} onClick={enviar}>Importar</button>
        <button onClick={alCancelar}>Cancelar</button>
      </div>
    </div>
  )
}

function RegistroManual({ alRegistrar, alCancelar }: { alRegistrar: (a: AvisoResumen) => void; alCancelar: () => void }) {
  const [zonas, setZonas] = useState<Zona[]>([])
  const [f, setF] = useState({ evento: 'Crecida del río Piraí', urgencia: 'Expected', severidad: 'Severe', certeza: 'Likely', titular: '', descripcion: '', instruccion: '', vigente: '', expira: '' })
  const [zonaIds, setZonaIds] = useState<string[]>([])
  const [error, setError] = useState('')
  useEffect(() => { api<Zona[]>('/api/zonas').then(setZonas) }, [])

  async function enviar(e: React.FormEvent) {
    e.preventDefault(); setError('')
    const iso = (v: string) => (v ? new Date(v).toISOString() : null)
    try {
      alRegistrar(await api<AvisoResumen>('/api/avisos', {
        method: 'POST',
        json: { ...f, titular: f.titular || null, descripcion: f.descripcion || null, instruccion: f.instruccion || null, vigente: iso(f.vigente), expira: iso(f.expira), zonaIds },
      }))
    } catch (err) { setError((err as Error).message) }
  }

  const sel = (campo: 'urgencia' | 'severidad' | 'certeza', opciones: string[], nombre: string) => (
    <label>{nombre}<select value={f[campo]} onChange={(e) => setF({ ...f, [campo]: e.target.value })}>{opciones.map((o) => <option key={o}>{o}</option>)}</select></label>
  )

  return (
    <form className="tarjeta" onSubmit={enviar}>
      <h3>Registrar aviso</h3>
      <p className="tenue">El sistema genera el mensaje CAP 1.2 con el patrón Builder, que valida los campos obligatorios.</p>
      <label>Evento<input required value={f.evento} onChange={(e) => setF({ ...f, evento: e.target.value })} /></label>
      <div className="fila">{sel('urgencia', URGENCIAS, 'Urgencia')}{sel('severidad', SEVERIDADES, 'Severidad')}{sel('certeza', CERTEZAS, 'Certeza')}</div>
      <label>Titular<input value={f.titular} onChange={(e) => setF({ ...f, titular: e.target.value })} /></label>
      <label>Descripción<textarea value={f.descripcion} onChange={(e) => setF({ ...f, descripcion: e.target.value })} /></label>
      <label>Instrucción a la población<textarea value={f.instruccion} onChange={(e) => setF({ ...f, instruccion: e.target.value })} /></label>
      <div className="fila">
        <label>Vigente desde<input type="datetime-local" value={f.vigente} onChange={(e) => setF({ ...f, vigente: e.target.value })} /></label>
        <label>Expira<input type="datetime-local" value={f.expira} onChange={(e) => setF({ ...f, expira: e.target.value })} /></label>
      </div>
      <b>Zonas afectadas</b>
      <div className="chips">
        {zonas.map((z) => (
          <label key={z.id} className="check">
            <input type="checkbox" checked={zonaIds.includes(z.id)}
              onChange={(e) => setZonaIds(e.target.checked ? [...zonaIds, z.id] : zonaIds.filter((x) => x !== z.id))} /> {z.nombre}
          </label>
        ))}
      </div>
      {error && <p className="error">{error}</p>}
      <div className="acciones"><button className="primario">Registrar</button><button type="button" onClick={alCancelar}>Cancelar</button></div>
    </form>
  )
}
