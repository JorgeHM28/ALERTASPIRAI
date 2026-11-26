import { useEffect, useState } from 'react'
import { api, descargarTexto, fecha, NOMBRE_TIPO, puedeEditar, type AvisoDetalle, type AvisoResumen, type Rol, type VersionDetalle, type Zona } from '../api'
import Mapa from './Mapa'
import { Bell, FileText, Plus, RefreshCw, Search, ShieldAlert, Upload } from 'lucide-react'
import { Alert } from '../components/ui/Alert'
import { Pestanas } from '../components/ui/Pestanas'
import { useConfirmar } from '../components/ui/useConfirmar'
import { useEnfocarAlAbrir } from '../components/ui/useEnfocarAlAbrir'
import { CERTEZAS, en, NIVEL_POR_SEVERIDAD, NOMBRE_CERTEZA, NOMBRE_FORMATO, NOMBRE_SEVERIDAD, NOMBRE_URGENCIA, SEVERIDADES, URGENCIAS } from '../cap'
import { BandaNivel } from '../components/BandaNivel'
import { AyudaPanel } from '../components/AyudaPanel'

const COLOR_SEVERIDAD: Record<string, string> = { Extreme: '#c62828', Severe: '#ef6c00', Moderate: '#f9a825', Minor: '#2e7d32', Unknown: '#757575' }

type Modo = 'lista' | 'importar' | 'manual'

/** RF-01: registro de avisos e importación CAP. */
export default function Avisos({ roles, alPreparar }: { roles: Rol[]; alPreparar: (versionId: string) => void }) {
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
  const { confirmar, dialogo } = useConfirmar()
  const { panel, titulo } = useEnfocarAlAbrir<HTMLDivElement>(detalle?.resumen.id)

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
    if (!await confirmar({ titulo: '¿Descartar este aviso?', texto: 'Deja de poder usarse para preparar mensajes. El registro y el documento original se conservan.', etiqueta: 'Descartar aviso', peligro: true })) return
    try { await api(`/api/avisos/${id}/descartar`, { method: 'POST' }); setAviso({ ok: true, texto: 'Aviso descartado.' }); cargar(); setDetalle(null) }
    catch (e) { setError(e instanceof Error ? e.message : 'No se pudo descartar el aviso.') }
  }

  /** RF-02: prepara la versión del mensaje (Alert, Update o Cancel según el aviso) y la abre en Mensajes. */
  async function preparar(id: string) {
    try { const v = await api<VersionDetalle>(`/api/avisos/${id}/versiones`, { method: 'POST', json: {} }); alPreparar(v.detalle.resumen.id) }
    catch (e) { setError(e instanceof Error ? e.message : 'No se pudo preparar el mensaje.') }
  }

  return (
    <section>
      <div className="barra">
        <div><h2>Avisos tempranos</h2><p className="tenue">Información oportuna para tomar mejores decisiones.</p></div>
        {editar && (
          <div className="acciones">
            <button className={modo === 'importar' ? 'primario' : ''} onClick={() => setModo('importar')} title="Importar un mensaje en formato CAP 1.2 (XML o JSON)"><Upload size={16} aria-hidden="true" />Importar CAP</button>
            <button className="primario" onClick={() => setModo('manual')}><Plus size={17} aria-hidden="true" />Registrar a mano</button>
          </div>
        )}
      </div>
      <AyudaPanel />
      {aviso && <Alert key={aviso.texto} tone={aviso.ok ? 'success' : 'danger'} dismissible>{aviso.texto}</Alert>}
      {error && <Alert key={error} tone="danger">{error} <button type="button" onClick={cargar}>Reintentar</button></Alert>}

      {modo === 'importar' && <ImportarCap alRegistrar={registrado} alCancelar={() => setModo('lista')} />}
      {modo === 'manual' && <RegistroManual alRegistrar={registrado} alCancelar={() => setModo('lista')} />}

      <div className="avisos-layout">
        <div className="list-panel">
        <div className="list-heading"><h3>Registro de avisos <span className="count-chip">{cargando || error ? '—' : avisos.length}</span></h3><button className="icon-button" onClick={cargar} disabled={cargando} aria-label="Actualizar avisos" title="Actualizar avisos"><RefreshCw size={17} /></button></div>
        <div className="list-filters"><div className="filter-tabs" role="group" aria-label="Estado del aviso">{[['todos', 'Todos', avisos.length], ['pendientes', 'Sin descartar', pendientes.length], ['noaptos', 'No aptos', avisos.filter(a => !a.aptoParaDifusion).length], ['descartados', 'Descartados', avisos.length - pendientes.length]].map(([id, nombre, n]) => <button key={id} aria-pressed={filtro === id} className={filtro === id ? 'selected' : ''} onClick={() => { setFiltro(id as string); setPagina(1) }}>{nombre}{!cargando && !error && <span className="cuenta">{n}</span>}</button>)}</div>
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
                <td>{en(NOMBRE_FORMATO, a.formato)}</td><td>{a.estado}</td>
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
          <div className="tarjeta detail-panel" ref={panel}>
            <BandaNivel texto={(NIVEL_POR_SEVERIDAD[detalle.resumen.severidad] ?? NIVEL_POR_SEVERIDAD.Unknown).texto}
              color={(NIVEL_POR_SEVERIDAD[detalle.resumen.severidad] ?? NIVEL_POR_SEVERIDAD.Unknown).color}>Aviso oficial · {detalle.resumen.estado}</BandaNivel>
            <div className="detail-eyebrow"><FileText size={16} aria-hidden="true" />Detalle del aviso<button className="row-link" onClick={() => setDetalle(null)}>Cerrar</button></div>
            <h3 ref={titulo} tabIndex={-1}>{detalle.resumen.titular ?? detalle.resumen.evento}</h3>
            <p className="tenue">{detalle.resumen.identificadorCap} · {detalle.resumen.remitente}</p>
            {detalle.resumen.aptoParaDifusion
              ? <Alert tone="success">Apto para difusión.</Alert>
              : <Alert tone="danger"><b>No apto para difusión</b> (registrado el {fecha(detalle.resumen.aptitudEvaluadaEn)}):<ul>{detalle.resumen.motivosNoApto.map(m => <li key={m}>{m}</li>)}</ul></Alert>}
            {detalle.resumen.tipoMensaje !== 'Alert' && <Alert tone="info">Mensaje oficial de tipo <b>{en(NOMBRE_TIPO, detalle.resumen.tipoMensaje).toLowerCase()}</b>{detalle.resumen.referencias ? <> que referencia a <code>{detalle.resumen.referencias}</code></> : null}.</Alert>}
            {!detalle.resumen.preparable && detalle.resumen.estado !== 'Descartado' && <Alert tone="warning"><b>No se puede preparar un mensaje:</b><ul>{detalle.resumen.motivosNoPreparable.map(m => <li key={m}>{m}</li>)}</ul></Alert>}
            <Pestanas key={detalle.resumen.id} etiqueta="Detalle del aviso" pestanas={[
              { id: 'contenido', nombre: 'Contenido', contenido: <>
                <div className="texto-canales">
                  {detalle.descripcion && <div><h4 className="subtitulo">Descripción</h4><p>{detalle.descripcion}</p></div>}
                  {detalle.instruccion && <div><h4 className="subtitulo">Instrucción a la población</h4><p>{detalle.instruccion}</p></div>}
                  {!detalle.descripcion && !detalle.instruccion && <p className="tenue">Este aviso no trae descripción ni instrucciones.</p>}
                </div>
                {detalle.area && <Mapa alto={220} capas={[{ clave: detalle.resumen.id, geometria: detalle.area, color: COLOR_SEVERIDAD[detalle.resumen.severidad], etiqueta: detalle.resumen.descripcionArea ?? '' }]} />}
              </> },
              { id: 'fuente', nombre: 'Fuente', contenido: (
                <dl>
                  <dt>Origen</dt><dd>{detalle.resumen.remitente} · {en(NOMBRE_FORMATO, detalle.resumen.formato)}</dd>
                  <dt>Urgencia · severidad · certeza</dt><dd>{en(NOMBRE_URGENCIA, detalle.resumen.urgencia)} · {en(NOMBRE_SEVERIDAD, detalle.resumen.severidad)} · {en(NOMBRE_CERTEZA, detalle.resumen.certeza)}</dd>
                  <dt>Vigencia</dt><dd>{fecha(detalle.vigente)} → {fecha(detalle.resumen.expira)}</dd>
                  <dt>Área</dt><dd>{detalle.resumen.descripcionArea ?? '—'}</dd>
                  <dt>Enviado</dt><dd>{fecha(detalle.resumen.enviado)}</dd>
                  <dt>Registrado</dt><dd>{fecha(detalle.resumen.registradoEn)}</dd>
                  {detalle.resumen.referencias && <><dt>Referencias</dt><dd className="codigo-linea">{detalle.resumen.referencias}</dd></>}
                </dl>) },
              ...(detalle.contenidoOriginal ? [{ id: 'original', nombre: 'Original', contenido: <>
                <p className="tenue">{detalle.tipoContenidoOriginal === TIPO_EXCEL ? 'Excel (.xlsx)' : detalle.tipoContenidoOriginal === 'application/json' ? 'JSON' : 'XML'} exacto, tal como llegó · SHA-256 {detalle.hashOriginal?.slice(0, 16)}…</p>
                <div className="acciones"><button onClick={() => descargarOriginal(detalle.contenidoOriginal!, detalle.tipoContenidoOriginal, detalle.resumen.identificadorCap)}>Descargar original</button></div>
                {detalle.tipoContenidoOriginal === TIPO_EXCEL ? <p className="tenue">El original es una planilla: descárgala para verla.</p> : <pre>{detalle.contenidoOriginal}</pre>}
              </> }] : []),
              { id: 'tecnico', nombre: 'Técnico', contenido: <>
                <p className="tenue">Mensaje en formato CAP 1.2 · {detalle.contenidoOriginal ? 'XML normalizado por el sistema' : 'XML generado por el sistema'}</p>
                <div className="acciones"><button onClick={() => descargarTexto(detalle.xmlCap, 'application/xml', `${detalle.resumen.identificadorCap}.xml`)}>Descargar XML</button></div>
                <pre>{detalle.xmlCap}</pre>
              </> },
            ]} />
            <div className="acciones">
              <button className="solo-escritorio" onClick={() => descargarTexto(detalle.xmlCap, 'application/xml', `${detalle.resumen.identificadorCap}.xml`)}>Descargar XML</button>
              {detalle.contenidoOriginal && <button className="solo-escritorio" onClick={() => descargarOriginal(detalle.contenidoOriginal!, detalle.tipoContenidoOriginal, detalle.resumen.identificadorCap)}>Descargar original</button>}
              {editar && detalle.resumen.preparable && <button className="primario" onClick={() => preparar(detalle.resumen.id)}>Preparar mensaje</button>}
              {editar && detalle.resumen.estado !== 'Descartado' && <button className="peligro" onClick={() => descartar(detalle.resumen.id)}>Descartar</button>}
            </div>
          </div>
        ) : <aside className="detail-empty"><span className="detail-empty-icon"><FileText size={28} aria-hidden="true" /></span><h3>Selecciona un aviso</h3><p>Aquí verás su alcance, su vigencia y el mensaje en formato CAP.</p></aside>}
      </div>
      {dialogo}
    </section>
  )
}

const TIPO_EXCEL = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

/** El original de un Excel está guardado en base64: se descarga como .xlsx. */
function descargarOriginal(contenido: string, tipo: string | null, identificador: string) {
  if (tipo === TIPO_EXCEL) {
    const bytes = Uint8Array.from(atob(contenido.trim()), c => c.charCodeAt(0))
    const url = URL.createObjectURL(new Blob([bytes], { type: TIPO_EXCEL }))
    Object.assign(document.createElement('a'), { href: url, download: `${identificador}.original.xlsx` }).click()
    URL.revokeObjectURL(url)
    return
  }
  descargarTexto(contenido, tipo ?? 'text/plain', `${identificador}.original.${tipo === 'application/json' ? 'json' : 'xml'}`)
}

function EtiquetaAptitud({ aviso }: { aviso: AvisoResumen }) {
  if (aviso.aptoParaDifusion) return <span className="etiqueta" style={{ background: '#2e7d32' }}>Apto</span>
  const texto = aviso.vencido ? 'Vencido' : aviso.incompleto ? 'Incompleto' : 'No apto'
  return <span className="etiqueta gris" title={aviso.motivosNoApto.join(' ')}>{texto}</span>
}

/** Un .xlsx se manda como texto en base64: la API lo reconoce por su firma (UEsDB, los bytes "PK" del ZIP). */
async function aBase64(f: File): Promise<string> {
  const bytes = new Uint8Array(await f.arrayBuffer())
  let binario = ''
  for (let i = 0; i < bytes.length; i += 0x8000) binario += String.fromCharCode(...bytes.subarray(i, i + 0x8000))
  return btoa(binario)
}

function ImportarCap({ alRegistrar, alCancelar }: { alRegistrar: (a: AvisoResumen) => void; alCancelar: () => void }) {
  const [contenido, setContenido] = useState('')
  const [excel, setExcel] = useState<string | null>(null)
  const [error, setError] = useState('')

  async function elegir(f: File | undefined) {
    setError('')
    if (!f) return
    if (f.name.toLowerCase().endsWith('.xlsx')) { setExcel(f.name); setContenido(await aBase64(f)) }
    else { setExcel(null); setContenido(await f.text()) }
  }

  async function enviar() {
    setError('')
    const tipo = excel ? 'text/plain' : contenido.trimStart().startsWith('{') ? 'application/json' : 'application/xml'
    try { alRegistrar(await api<AvisoResumen>('/api/avisos/importar', { method: 'POST', body: contenido, headers: { 'Content-Type': tipo } })) }
    catch (e) { setError((e as Error).message) }
  }

  return (
    <div className="tarjeta">
      <h3>Importar mensaje CAP 1.2</h3>
      <p className="tenue">Pega el XML o JSON, o elige un archivo XML, JSON o Excel (.xlsx con la plantilla de avisos). El sistema detecta el formato, valida el mensaje contra el estándar y guarda el documento original exacto. Si le falta información o ya venció, se registra igual pero queda marcado como no apto para difusión.</p>
      <input type="file" accept=".xml,.json,.cap,.xlsx" onChange={(e) => elegir(e.target.files?.[0])} />
      {excel
        ? <p>Excel elegido: <b>{excel}</b>. <button className="row-link" onClick={() => { setExcel(null); setContenido('') }}>Quitar</button></p>
        : <textarea rows={10} value={contenido} onChange={(e) => setContenido(e.target.value)} placeholder='<alert xmlns="urn:oasis:names:tc:emergency:cap:1.2">…' className="codigo" />}
      {error && <p className="error" role="status">{error}</p>}
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

  // Lo que todavía falta para que el aviso sea apto para difusión (el servidor aplica la misma regla al registrar).
  const faltan = [
    !(f.titular.trim() || f.descripcion.trim()) && 'titular o descripción',
    !f.instruccion.trim() && 'instrucción',
    zonaIds.length === 0 && 'zonas afectadas',
    !f.expira && 'fecha de expiración',
  ].filter(Boolean) as string[]

  const sel = (campo: 'urgencia' | 'severidad' | 'certeza', opciones: string[], nombre: string, nombres: Record<string, string>) => (
    <label>{nombre}<select value={f[campo]} onChange={(e) => setF({ ...f, [campo]: e.target.value })}>{opciones.map((o) => <option key={o} value={o}>{en(nombres, o)}</option>)}</select></label>
  )

  return (
    <form className="tarjeta" onSubmit={enviar}>
      <h3>Registrar aviso</h3>
      <p className="tenue">Completa los datos y selecciona las zonas afectadas para generar el mensaje CAP 1.2. Los campos marcados «para difundir» son los que hacen falta para que el aviso quede apto.</p>
      <label>Evento<input required value={f.evento} onChange={(e) => setF({ ...f, evento: e.target.value })} /></label>
      <div className="fila">{sel('urgencia', URGENCIAS, 'Urgencia', NOMBRE_URGENCIA)}{sel('severidad', SEVERIDADES, 'Severidad', NOMBRE_SEVERIDAD)}{sel('certeza', CERTEZAS, 'Certeza', NOMBRE_CERTEZA)}</div>
      <label>Titular <span className="requisito">titular o descripción, para difundir</span><input value={f.titular} onChange={(e) => setF({ ...f, titular: e.target.value })} /></label>
      <label>Descripción <span className="requisito">titular o descripción, para difundir</span><textarea value={f.descripcion} onChange={(e) => setF({ ...f, descripcion: e.target.value })} /></label>
      <label>Instrucción a la población <span className="requisito">para difundir</span><textarea value={f.instruccion} onChange={(e) => setF({ ...f, instruccion: e.target.value })} /></label>
      <div className="fila">
        <label>Vigente desde<input type="datetime-local" value={f.vigente} onChange={(e) => setF({ ...f, vigente: e.target.value })} /></label>
        <label>Expira <span className="requisito">para difundir</span><input type="datetime-local" value={f.expira} onChange={(e) => setF({ ...f, expira: e.target.value })} /></label>
      </div>
      <b>Zonas afectadas <span className="requisito">para difundir</span></b>
      <div className="chips">
        {zonas.map((z) => (
          <label key={z.id} className="check">
            <input type="checkbox" checked={zonaIds.includes(z.id)}
              onChange={(e) => setZonaIds(e.target.checked ? [...zonaIds, z.id] : zonaIds.filter((x) => x !== z.id))} /> {z.nombre}
          </label>
        ))}
      </div>
      {error && <p className="error" role="status">{error}</p>}
      {faltan.length > 0
        ? <Alert tone="warning">Se puede registrar así, pero quedará como <b>no apto para difundir</b>. Falta: {faltan.join(', ')}.</Alert>
        : <Alert tone="success">Con estos datos el aviso queda completo para difundirse (mientras no esté vencido).</Alert>}
      <div className="acciones"><button className="primario">Registrar</button><button type="button" onClick={alCancelar}>Cancelar</button></div>
    </form>
  )
}
