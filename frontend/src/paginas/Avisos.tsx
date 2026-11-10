import { useEffect, useState } from 'react'
import { api, descargarTexto, fecha, puedeEditar, type AvisoDetalle, type AvisoResumen, type Rol, type Zona } from '../api'
import Mapa from './Mapa'
import { Ban, Bell, Clock3, FileText, Plus, RefreshCw, Search, ShieldAlert, Upload } from 'lucide-react'
import { Alert } from '../components/ui/Alert'
import { UnfoldAccordion } from '../components/ui/UnfoldAccordion'

const URGENCIAS = ['Immediate', 'Expected', 'Future', 'Past', 'Unknown']
const SEVERIDADES = ['Extreme', 'Severe', 'Moderate', 'Minor', 'Unknown']
const CERTEZAS = ['Observed', 'Likely', 'Possible', 'Unlikely', 'Unknown']
const COLOR_SEVERIDAD: Record<string, string> = { Extreme: '#c62828', Severe: '#ef6c00', Moderate: '#f9a825', Minor: '#2e7d32', Unknown: '#757575' }
const NOMBRE_SEVERIDAD: Record<string, string> = { Extreme: 'Extrema', Severe: 'Severa', Moderate: 'Moderada', Minor: 'Menor', Unknown: 'Desconocida' }

type Modo = 'lista' | 'importar' | 'manual'

/** RF-01: registro de avisos e importación CAP. */
export default function Avisos({ roles }: { roles: Rol[] }) {
  const [avisos, setAvisos] = useState<AvisoResumen[]>([])
  const [detalle, setDetalle] = useState<AvisoDetalle | null>(null)
  const [modo, setModo] = useState<Modo>('lista')
  const [aviso, setAviso] = useState<{ ok: boolean; texto: string } | null>(null)
  const editar = puedeEditar(roles)
  const [busqueda, setBusqueda] = useState('')
  const [severidad, setSeveridad] = useState('')
  const [filtro, setFiltro] = useState('todos')
  const [pagina, setPagina] = useState(1)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  const cargar = () => {
    setCargando(true); setError('')
    return api<AvisoResumen[]>('/api/avisos').then(setAvisos)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : 'No se pudieron cargar los avisos.'))
      .finally(() => setCargando(false))
  }
  useEffect(() => { cargar() }, [])

  const pendientes = avisos.filter(a => a.estado !== 'Descartado')
  const filtrados = avisos.filter(a =>
    (filtro === 'todos' || (filtro === 'descartados' ? a.estado === 'Descartado' : filtro === 'noaptos' ? !a.aptoParaDifusion : a.estado !== 'Descartado')) &&
    (!severidad || a.severidad === severidad) &&
    `${a.titular ?? ''} ${a.evento} ${a.remitente} ${a.identificadorCap}`.toLocaleLowerCase().includes(busqueda.toLocaleLowerCase()))
  const totalPaginas = Math.max(1, Math.ceil(filtrados.length / 8))
  const paginaActual = Math.min(pagina, totalPaginas)
  const visibles = filtrados.slice((paginaActual - 1) * 8, paginaActual * 8)
  const abrir = (a: AvisoResumen) => api<AvisoDetalle>(`/api/avisos/${a.id}`).then(setDetalle)
    .catch((e: unknown) => setError(e instanceof Error ? e.message : 'No se pudo abrir el aviso.'))

  const registrado = (a: AvisoResumen) => {
    setAviso({ ok: true, texto: `Aviso "${a.evento}" registrado (${a.identificadorCap}).` })
    setModo('lista'); cargar(); abrir(a)
  }

  async function descartar(id: string) {
    if (!confirm('¿Descartar este aviso?')) return
    try { await api(`/api/avisos/${id}/descartar`, { method: 'POST' }); cargar(); setDetalle(null) }
    catch (e) { setError(e instanceof Error ? e.message : 'No se pudo descartar el aviso.') }
  }

  return (
    <section>
      <div className="barra">
        <div><h2>Avisos tempranos</h2><p className="tenue">Información oportuna para tomar mejores decisiones.</p></div>
        {editar && (
          <div className="acciones">
            <button className={modo === 'importar' ? 'primario' : ''} onClick={() => setModo('importar')}><Upload size={16} aria-hidden="true" />Importar CAP</button>
            <button className="primario" onClick={() => setModo('manual')}><Plus size={17} aria-hidden="true" />Registrar a mano</button>
          </div>
        )}
      </div>
      <div className="stats-grid" aria-label="Resumen de avisos">
        {[{ nombre: 'Avisos registrados', valor: avisos.length, Icon: Bell, clase: 'teal', nota: 'Todos los registros' },
          { nombre: 'Sin descartar', valor: pendientes.length, Icon: Clock3, clase: 'blue', nota: 'Pendientes de seguimiento' },
          { nombre: 'Severidad alta', valor: pendientes.filter(a => ['Severe', 'Extreme'].includes(a.severidad)).length, Icon: ShieldAlert, clase: 'amber', nota: 'Severos o extremos, sin descartar' },
          { nombre: 'No aptos para difusión', valor: avisos.filter(a => !a.aptoParaDifusion).length, Icon: Ban, clase: 'slate', nota: 'Incompletos, vencidos o descartados' }].map(({ nombre, valor, Icon, clase, nota }) =>
          <div className="stat-card" key={nombre}><div className="stat-heading"><span>{nombre}</span><span className={`stat-icon ${clase}`}><Icon size={19} aria-hidden="true" /></span></div><strong>{cargando || error ? '—' : valor}</strong><small>{nota}</small></div>)}
      </div>
      {aviso && <Alert key={aviso.texto} tone={aviso.ok ? 'success' : 'danger'} dismissible>{aviso.texto}</Alert>}
      {error && <Alert key={error} tone="danger">{error} <button type="button" onClick={cargar}>Reintentar</button></Alert>}

      {modo === 'importar' && <ImportarCap alRegistrar={registrado} alCancelar={() => setModo('lista')} />}
      {modo === 'manual' && <RegistroManual alRegistrar={registrado} alCancelar={() => setModo('lista')} />}

      <div className="avisos-layout">
        <div className="list-panel">
        <div className="list-heading"><h3>Registro de avisos <span className="count-chip">{cargando || error ? '—' : avisos.length}</span></h3><button className="icon-button" onClick={cargar} disabled={cargando} aria-label="Actualizar avisos" title="Actualizar avisos"><RefreshCw size={17} /></button></div>
        <div className="list-filters"><div className="filter-tabs" role="group" aria-label="Estado del aviso">{[['todos', 'Todos'], ['pendientes', 'Sin descartar'], ['noaptos', 'No aptos'], ['descartados', 'Descartados']].map(([id, nombre]) => <button key={id} aria-pressed={filtro === id} className={filtro === id ? 'selected' : ''} onClick={() => { setFiltro(id); setPagina(1) }}>{nombre}</button>)}</div>
          <div className="filter-fields"><label className="search-field"><Search size={17} aria-hidden="true" /><input aria-label="Buscar avisos" placeholder="Buscar evento, origen o identificador…" value={busqueda} onChange={e => { setBusqueda(e.target.value); setPagina(1) }} /></label>
          <select aria-label="Filtrar por severidad" value={severidad} onChange={e => { setSeveridad(e.target.value); setPagina(1) }}><option value="">Toda severidad</option>{SEVERIDADES.map(s => <option key={s} value={s}>{NOMBRE_SEVERIDAD[s]}</option>)}</select></div>
        </div>
        <div className="table-scroll" aria-busy={cargando}><table>
          <thead><tr><th>Enviado</th><th>Evento</th><th>Severidad</th><th>Origen</th><th>Estado</th><th>Difusión</th></tr></thead>
          <tbody>
            {!cargando && !error && visibles.map((a) => (
              <tr key={a.id} className={`clicable ${detalle?.resumen.id === a.id ? 'selected-row' : ''} ${a.estado === 'Descartado' ? 'tachado' : ''}`}
                onClick={() => abrir(a)}>
                <td>{fecha(a.enviado)}</td>
                <td><button className="row-link" onClick={e => { e.stopPropagation(); abrir(a) }}>{a.titular ?? a.evento}</button><div className="tenue">{a.remitente}</div></td>
                <td><span className={`severity severity-${a.severidad}`}><span aria-hidden="true" />{NOMBRE_SEVERIDAD[a.severidad] ?? a.severidad}</span></td>
                <td>{a.formato}</td><td>{a.estado}</td>
                <td><EtiquetaAptitud aviso={a} /></td>
              </tr>
            ))}
            {cargando && <tr><td colSpan={6}><div className="table-empty" role="status"><RefreshCw size={26} /><strong>Cargando avisos…</strong><span>Consultando los registros del sistema.</span></div></td></tr>}
            {!cargando && error && <tr><td colSpan={6}><div className="table-empty"><ShieldAlert size={26} /><strong>Registros no disponibles</strong><span>Reintenta la conexión para consultar los avisos.</span></div></td></tr>}
            {!cargando && !error && filtrados.length === 0 && <tr><td colSpan={6}><div className="table-empty"><Bell size={28} /><strong>{avisos.length ? 'Sin coincidencias' : 'Todavía no hay avisos'}</strong><span>{avisos.length ? 'Prueba con otro texto o cambia los filtros.' : 'Los avisos registrados aparecerán aquí.'}</span>{(busqueda || severidad || filtro !== 'todos') && <button onClick={() => { setBusqueda(''); setSeveridad(''); setFiltro('todos'); setPagina(1) }}>Limpiar filtros</button>}</div></td></tr>}
          </tbody>
        </table></div>
        <div className="pagination"><span role="status">{cargando ? 'Consultando registros…' : error ? 'Sin conexión con los registros' : `${filtrados.length} aviso(s) · Página ${paginaActual} de ${totalPaginas}`}</span><div><button disabled={cargando || paginaActual <= 1} onClick={() => setPagina(paginaActual - 1)}>Anterior</button><button disabled={cargando || paginaActual >= totalPaginas} onClick={() => setPagina(paginaActual + 1)}>Siguiente</button></div></div>
        </div>

        {detalle ? (
          <div className="tarjeta detail-panel">
            <div className="detail-eyebrow"><FileText size={16} aria-hidden="true" />DETALLE DEL AVISO<button className="row-link" onClick={() => setDetalle(null)}>Cerrar</button></div>
            <h3>{detalle.resumen.titular ?? detalle.resumen.evento}</h3>
            <p className="tenue">{detalle.resumen.identificadorCap} · {detalle.resumen.remitente}</p>
            {detalle.resumen.aptoParaDifusion
              ? <Alert tone="success">Apto para difusión.</Alert>
              : <Alert tone="danger"><b>No apto para difusión</b> (registrado el {fecha(detalle.resumen.aptitudEvaluadaEn)}):<ul>{detalle.resumen.motivosNoApto.map(m => <li key={m}>{m}</li>)}</ul></Alert>}
            <dl>
              <dt>Urgencia / severidad / certeza</dt><dd>{detalle.resumen.urgencia} · {detalle.resumen.severidad} · {detalle.resumen.certeza}</dd>
              <dt>Vigencia</dt><dd>{fecha(detalle.vigente)} → {fecha(detalle.resumen.expira)}</dd>
              <dt>Área</dt><dd>{detalle.resumen.descripcionArea ?? '—'}</dd>
              {detalle.descripcion && <><dt>Descripción</dt><dd>{detalle.descripcion}</dd></>}
              {detalle.instruccion && <><dt>Instrucción</dt><dd>{detalle.instruccion}</dd></>}
            </dl>
            {detalle.area && <Mapa alto={220} capas={[{ clave: detalle.resumen.id, geometria: detalle.area, color: COLOR_SEVERIDAD[detalle.resumen.severidad], etiqueta: detalle.resumen.descripcionArea ?? '' }]} />}
            {detalle.contenidoOriginal && (
              <UnfoldAccordion key={`${detalle.resumen.id}-original`} title="Documento original importado"
                subtitle={`${detalle.tipoContenidoOriginal === 'application/json' ? 'JSON' : 'XML'} exacto, tal como llegó · SHA-256 ${detalle.hashOriginal?.slice(0, 16)}…`}>
                <pre>{detalle.contenidoOriginal}</pre>
              </UnfoldAccordion>
            )}
            <UnfoldAccordion key={detalle.resumen.id} title="Mensaje CAP 1.2" subtitle={detalle.contenidoOriginal ? 'XML normalizado por el sistema' : 'Generado por el sistema'}><pre>{detalle.xmlCap}</pre></UnfoldAccordion>
            <div className="acciones">
              <button onClick={() => descargarTexto(detalle.xmlCap, 'application/xml', `${detalle.resumen.identificadorCap}.xml`)}>Descargar CAP</button>
              {detalle.contenidoOriginal && <button onClick={() => descargarTexto(detalle.contenidoOriginal!, detalle.tipoContenidoOriginal ?? 'text/plain',
                `${detalle.resumen.identificadorCap}.original.${detalle.tipoContenidoOriginal === 'application/json' ? 'json' : 'xml'}`)}>Descargar original</button>}
              {editar && detalle.resumen.estado !== 'Descartado' && <button className="peligro" onClick={() => descartar(detalle.resumen.id)}>Descartar</button>}
            </div>
          </div>
        ) : <aside className="detail-empty"><span className="detail-empty-icon"><FileText size={28} aria-hidden="true" /></span><span className="eyebrow">CADA DETALLE CUENTA</span><h3>La información, en contexto.</h3><p>Selecciona un aviso para consultar su alcance, vigencia y mensaje CAP.</p><div><MapPinPlaceholder /></div><small>El detalle aparecerá en este panel.</small></aside>}
      </div>
    </section>
  )
}

function EtiquetaAptitud({ aviso }: { aviso: AvisoResumen }) {
  if (aviso.aptoParaDifusion) return <span className="etiqueta" style={{ background: '#2e7d32' }}>Apto</span>
  const texto = aviso.vencido ? 'Vencido' : aviso.incompleto ? 'Incompleto' : 'No apto'
  return <span className="etiqueta gris" title={aviso.motivosNoApto.join(' ')}>{texto}</span>
}

function MapPinPlaceholder() {
  return <div className="detail-lines" aria-hidden="true"><span /><span /><span /></div>
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
      <p className="tenue">Pega el XML o JSON, o elige un archivo. El sistema detecta el formato, valida el mensaje contra el estándar y guarda el documento original exacto. Si le falta información o ya venció, se registra igual pero queda marcado como no apto para difusión.</p>
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
      <p className="tenue">Completa los datos y selecciona las zonas afectadas para generar el mensaje CAP 1.2. Sin titular o descripción, instrucción, zonas o fecha de expiración el aviso queda como no apto para difusión.</p>
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
