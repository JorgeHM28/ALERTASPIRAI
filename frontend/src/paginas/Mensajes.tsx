import { useEffect, useState } from 'react'
import { CheckCircle2, ClipboardCheck, FilePen, MessageSquareText, RefreshCw, Send, ShieldAlert, Undo2, Users, XCircle } from 'lucide-react'
import {
  api, descargarTexto, fecha, NOMBRE_ESTADO_VERSION, NOMBRE_TIPO, puedeAprobar, puedeEditar,
  type EstadoVersion, type ListaDestinos, type ResultadoDifusion, type Rol, type TipoCanal, type VersionDetalle, type VersionResumen,
} from '../api'
import Mapa from './Mapa'
import { Alert } from '../components/ui/Alert'
import { UnfoldAccordion } from '../components/ui/UnfoldAccordion'
import { NivelAlerta } from '../components/NivelAlerta'

type Filtro = 'todas' | 'Borrador' | 'EnRevision' | 'aprobadas' | 'Rechazada'
const FILTROS: [Filtro, string][] = [['todas', 'Todas'], ['Borrador', 'Borradores'], ['EnRevision', 'En revisión'], ['aprobadas', 'Aprobadas'], ['Rechazada', 'Rechazadas']]
const coincide = (f: Filtro, e: EstadoVersion) =>
  f === 'todas' || (f === 'aprobadas' ? e === 'Aprobada' || e === 'Difundida' : e === f)

/** RF-02 (preparar y corregir), RF-11 (revisar, aprobar o rechazar), RF-07 (destinos) y RF-06 (texto y mapa). */
export default function Mensajes({ roles, versionInicial }: { roles: Rol[]; versionInicial?: string | null }) {
  const [versiones, setVersiones] = useState<VersionResumen[]>([])
  const [detalle, setDetalle] = useState<VersionDetalle | null>(null)
  const [filtro, setFiltro] = useState<Filtro>('todas')
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [aviso, setAviso] = useState<{ ok: boolean; texto: string } | null>(null)

  const cargar = () => {
    setCargando(true); setError('')
    return api<VersionResumen[]>('/api/versiones').then(setVersiones)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : 'No se pudieron cargar las versiones.'))
      .finally(() => setCargando(false))
  }
  const abrir = (id: string) => api<VersionDetalle>(`/api/versiones/${id}`).then(setDetalle)
    .catch((e: unknown) => setError(e instanceof Error ? e.message : 'No se pudo abrir la versión.'))
  useEffect(() => { cargar(); if (versionInicial) abrir(versionInicial) }, [versionInicial])

  /** Ejecuta una acción sobre la versión y muestra la versión que resulta (puede ser otra, por ejemplo al corregir). */
  async function accion(texto: string, llamada: () => Promise<VersionDetalle>) {
    setAviso(null)
    try {
      const v = await llamada()
      setDetalle(v); setAviso({ ok: true, texto }); cargar()
    } catch (e) { setAviso({ ok: false, texto: e instanceof Error ? e.message : 'No se pudo completar la acción.' }) }
  }

  const filtradas = versiones.filter(v => coincide(filtro, v.estado))
  const cuenta = (f: Filtro) => versiones.filter(v => coincide(f, v.estado)).length

  return (
    <section>
      <div className="barra">
        <div><h2>Mensajes a la población</h2><p className="tenue">Versiones Alert, Update y Cancel preparadas desde los avisos oficiales, con su revisión y aprobación.</p></div>
      </div>
      <div className="stats-grid" aria-label="Resumen de versiones">
        {[{ nombre: 'En revisión', valor: cuenta('EnRevision'), Icon: ClipboardCheck, clase: 'amber', nota: 'Esperan al aprobador' },
          { nombre: 'Borradores', valor: cuenta('Borrador'), Icon: FilePen, clase: 'blue', nota: 'En preparación' },
          { nombre: 'Aprobadas', valor: cuenta('aprobadas'), Icon: CheckCircle2, clase: 'teal', nota: 'Listas o ya difundidas' },
          { nombre: 'Rechazadas', valor: cuenta('Rechazada'), Icon: XCircle, clase: 'slate', nota: 'Volvieron con su motivo' }].map(({ nombre, valor, Icon, clase, nota }) =>
          <div className="stat-card" key={nombre}><div className="stat-heading"><span>{nombre}</span><span className={`stat-icon ${clase}`}><Icon size={19} aria-hidden="true" /></span></div><strong>{cargando || error ? '—' : valor}</strong><small>{nota}</small></div>)}
      </div>
      {aviso && <Alert key={aviso.texto} tone={aviso.ok ? 'success' : 'danger'} dismissible>{aviso.texto}</Alert>}
      {error && <Alert key={error} tone="danger">{error} <button type="button" onClick={cargar}>Reintentar</button></Alert>}

      <div className="avisos-layout">
        <div className="list-panel">
          <div className="list-heading"><h3>Versiones <span className="count-chip">{cargando || error ? '—' : versiones.length}</span></h3><button className="icon-button" onClick={cargar} disabled={cargando} aria-label="Actualizar versiones" title="Actualizar versiones"><RefreshCw size={17} /></button></div>
          <div className="list-filters"><div className="filter-tabs" role="group" aria-label="Estado de la versión">{FILTROS.map(([id, nombre]) =>
            <button key={id} aria-pressed={filtro === id} className={filtro === id ? 'selected' : ''} onClick={() => setFiltro(id)}>{nombre}</button>)}</div></div>
          <div className="table-scroll" aria-busy={cargando}><table>
            <thead><tr><th>Creada</th><th>Mensaje</th><th>Nivel</th><th>Estado</th></tr></thead>
            <tbody>
              {!cargando && !error && filtradas.map(v => (
                <tr key={v.id} className={`clicable ${detalle?.detalle.resumen.id === v.id ? 'selected-row' : ''} ${v.reemplazadaPorId ? 'tachado' : ''}`} onClick={() => abrir(v.id)}>
                  <td>{fecha(v.creadoEn)}</td>
                  <td><button className="row-link" onClick={e => { e.stopPropagation(); abrir(v.id) }}>{v.titular ?? v.evento}</button>
                    <div className="tenue">Versión {v.numero} · {NOMBRE_TIPO[v.tipo]}{v.esPrueba ? ' · prueba' : ''}</div></td>
                  <td><NivelAlerta nivel={v.nivel} /></td>
                  <td><EstadoChip estado={v.estado} />{v.reemplazadaPorId && <div className="tenue">Reemplazada</div>}</td>
                </tr>
              ))}
              {cargando && <tr><td colSpan={4}><div className="table-empty" role="status"><RefreshCw size={26} /><strong>Cargando versiones…</strong></div></td></tr>}
              {!cargando && !error && filtradas.length === 0 && <tr><td colSpan={4}><div className="table-empty"><MessageSquareText size={28} /><strong>{versiones.length ? 'Sin versiones en este estado' : 'Todavía no hay mensajes'}</strong><span>Desde un aviso oficial, usa «Preparar mensaje» para crear la primera versión.</span></div></td></tr>}
            </tbody>
          </table></div>
        </div>

        {detalle
          ? <DetalleVersion key={detalle.detalle.resumen.id} v={detalle} roles={roles} accion={accion} cerrar={() => setDetalle(null)} />
          : <aside className="detail-empty"><span className="detail-empty-icon"><MessageSquareText size={28} aria-hidden="true" /></span><span className="eyebrow">REVISAR ANTES DE DIFUNDIR</span><h3>Cada versión, con su fuente.</h3><p>Selecciona una versión para ver su texto, su zona en el mapa, sus referencias y los destinos.</p><small>El detalle aparecerá en este panel.</small></aside>}
      </div>
    </section>
  )
}

function EstadoChip({ estado }: { estado: EstadoVersion }) {
  return <span className={`estado-version estado-${estado}`}>{NOMBRE_ESTADO_VERSION[estado]}</span>
}

function DetalleVersion({ v, roles, accion, cerrar }: {
  v: VersionDetalle; roles: Rol[]; cerrar: () => void
  accion: (texto: string, llamada: () => Promise<VersionDetalle>) => Promise<void>
}) {
  const d = v.detalle
  const r = d.resumen
  const editar = puedeEditar(roles)
  const aprobar = puedeAprobar(roles)
  const [redaccion, setRedaccion] = useState({ titular: r.titular ?? '', descripcion: d.descripcion ?? '', instruccion: d.instruccion ?? '' })
  const [motivo, setMotivo] = useState('')
  const [difusion, setDifusion] = useState<ResultadoDifusion | null>(null)
  const [errorDifusion, setErrorDifusion] = useState('')
  const vigente = (r.estado === 'Aprobada' || r.estado === 'Difundida') && !r.reemplazadaPorId
  const ruta = `/api/versiones/${r.id}`

  const guardar = () => api<VersionDetalle>(ruta, { method: 'PUT', json: redaccion })

  async function difundir() {
    if (!confirm('¿Difundir esta versión por web, SMS y canales manuales?')) return
    setErrorDifusion('')
    try { setDifusion(await api<ResultadoDifusion>(`/api/difusion/${r.id}`, { method: 'POST' })) }
    catch (e) { setErrorDifusion(e instanceof Error ? e.message : 'No se pudo difundir.') }
  }

  return (
    <div className="tarjeta detail-panel">
      <div className="detail-eyebrow"><MessageSquareText size={16} aria-hidden="true" />VERSIÓN {r.numero} · {NOMBRE_TIPO[r.tipo].toUpperCase()}<button className="row-link" onClick={cerrar}>Cerrar</button></div>
      <h3>{r.titular ?? r.evento}</h3>
      <div className="fila-chips"><NivelAlerta nivel={r.nivel} grande /><EstadoChip estado={r.estado} /></div>
      {r.esPrueba && <Alert tone="warning">Aviso de prueba o simulacro: no se publica en la web pública y el SMS real se bloquea.</Alert>}
      {r.reemplazadaPorId && <Alert tone="info">Esta versión fue reemplazada por un Update o Cancel aprobado.</Alert>}
      {r.estado === 'Rechazada' && <Alert tone="danger"><b>Rechazada</b> el {fecha(r.revisadoEn)}: {r.motivoRechazo}</Alert>}
      {d.motivoDevolucion && r.estado === 'Borrador' && <Alert tone="warning"><b>Volvió con este motivo:</b> {d.motivoDevolucion}</Alert>}
      {r.estado === 'Borrador' && r.faltantes.length > 0 && <Alert tone="warning"><b>Queda como borrador hasta completar:</b><ul>{r.faltantes.map(f => <li key={f}>{f}</li>)}</ul></Alert>}

      {r.estado === 'Borrador' && editar && (
        <form className="tarjeta interior" onSubmit={e => { e.preventDefault(); accion('Borrador guardado.', guardar) }}>
          <b>Redacción para la población</b>
          <label>Titular<input value={redaccion.titular} maxLength={300} onChange={e => setRedaccion({ ...redaccion, titular: e.target.value })} /></label>
          <label>Descripción<textarea value={redaccion.descripcion} onChange={e => setRedaccion({ ...redaccion, descripcion: e.target.value })} /></label>
          <label>Instrucciones oficiales<textarea value={redaccion.instruccion} onChange={e => setRedaccion({ ...redaccion, instruccion: e.target.value })} /></label>
          <p className="tenue">Los hechos, el nivel y el ámbito son los del aviso oficial y no se editan.</p>
          <div className="acciones">
            <button>Guardar borrador</button>
            <button type="button" className="primario" onClick={() => accion('Enviada a revisión: el XML CAP 1.2 se generó y es válido.', async () => {
              await guardar(); return api<VersionDetalle>(`${ruta}/enviar-revision`, { method: 'POST' })
            })}><Send size={16} aria-hidden="true" />Enviar a revisión</button>
          </div>
        </form>
      )}

      {r.estado === 'EnRevision' && aprobar && (
        <div className="tarjeta interior revision">
          <b>Revisión</b>
          <p className="tenue">Revisa la fuente, las instrucciones, el contenido CAP, el tipo, las referencias y el alcance antes de decidir.</p>
          <div className="acciones"><button className="primario" onClick={() => accion('Versión aprobada. Ya no se puede editar.', () => api(`${ruta}/aprobar`, { method: 'POST' }))}><CheckCircle2 size={16} aria-hidden="true" />Aprobar</button></div>
          <label>Motivo del rechazo<textarea value={motivo} onChange={e => setMotivo(e.target.value)} placeholder="Obligatorio para rechazar" /></label>
          <div className="acciones"><button className="peligro" disabled={motivo.trim().length < 5} onClick={() => accion('Versión rechazada: vuelve al operador con el motivo.', () => api(`${ruta}/rechazar`, { method: 'POST', json: { motivo } }))}><XCircle size={16} aria-hidden="true" />Rechazar</button></div>
        </div>
      )}

      <div className="texto-canales">
        <div><span className="eyebrow">SMS Y ALTAVOZ</span><p>{v.textoSms}</p></div>
        <div><span className="eyebrow">RADIO Y WEB (LECTURA)</span><p className="pre">{v.textoLectura}</p></div>
      </div>
      {v.area && <Mapa alto={220} capas={[{ clave: r.id, geometria: v.area, color: r.nivel.color, etiqueta: `${r.nivel.texto} · ${r.descripcionArea ?? ''}` }]} />}

      <dl>
        <dt>Fuente oficial</dt><dd>{d.fuenteRemitente} · {d.fuenteIdentificador} ({NOMBRE_TIPO[d.fuenteTipo]}, {fecha(d.fuenteEnviado)})</dd>
        <dt>Hechos</dt><dd>{r.evento} · urgencia {d.urgencia} · severidad {d.severidad} · certeza {d.certeza}</dd>
        <dt>Vigencia</dt><dd>{fecha(d.vigente)} → {fecha(r.expira)}</dd>
        <dt>Zona</dt><dd>{r.descripcionArea ?? '—'}</dd>
        <dt>Mensaje CAP</dt><dd>{d.identificadorCap} · {d.remitente}{d.enviadoCap ? ` · enviado ${fecha(d.enviadoCap)}` : ''}</dd>
        {d.referencias && <><dt>Referencias</dt><dd className="codigo-linea">{d.referencias}</dd></>}
        {r.revisadoEn && <><dt>Revisión</dt><dd>{NOMBRE_ESTADO_VERSION[r.estado]} el {fecha(r.revisadoEn)}</dd></>}
      </dl>

      <Destinos versionId={r.id} puedeGuardar={editar && vigente} />

      {d.xmlCap && <UnfoldAccordion title="Mensaje CAP 1.2" subtitle="XML generado y validado al enviar a revisión"><pre>{d.xmlCap}</pre></UnfoldAccordion>}

      {difusion && <Alert tone={difusion.incidencias.length ? 'warning' : 'success'}>Difundida{difusion.esPrueba ? ' (prueba)' : ''}: web {difusion.publicadoEnWeb ? 'sí' : 'no'}, {difusion.trabajosSms} SMS, {difusion.tareasManuales.length} tareas manuales.{difusion.incidencias.map(i => <div key={i.canal + i.detalle}>{i.canal}: {i.detalle}</div>)}</Alert>}
      {errorDifusion && <Alert tone="danger">{errorDifusion}</Alert>}

      <div className="acciones">
        {d.xmlCap && <button onClick={() => descargarTexto(d.xmlCap!, 'application/xml', `${d.identificadorCap}.xml`)}>Descargar CAP</button>}
        {r.estado === 'Rechazada' && editar && <button className="primario" onClick={() => accion('Se creó una versión nueva en borrador para corregir.', () => api(`${ruta}/corregir`, { method: 'POST' }))}><Undo2 size={16} aria-hidden="true" />Corregir (nueva versión)</button>}
        {vigente && editar && r.tipo !== 'Cancel' && <>
          <button onClick={() => accion('Update preparado como borrador.', () => api(`${ruta}/update`, { method: 'POST', json: {} }))}>Preparar Update</button>
          <button className="peligro" onClick={() => confirm('¿Preparar la cancelación de este aviso?') && accion('Cancel preparado como borrador.', () => api(`${ruta}/cancel`, { method: 'POST', json: { instruccion: 'El aviso fue cancelado. Puede retomar sus actividades con precaución.' } }))}>Preparar Cancel</button>
        </>}
        {vigente && aprobar && <button className="primario" onClick={difundir}><Send size={16} aria-hidden="true" />Difundir</button>}
      </div>
    </div>
  )
}

/** RF-07: alcance por comunidad. La vista previa no se guarda; la lista se guarda solo para una versión aprobada. */
function Destinos({ versionId, puedeGuardar }: { versionId: string; puedeGuardar: boolean }) {
  const [aprobados, setAprobados] = useState<TipoCanal[]>([])
  const [canales, setCanales] = useState<TipoCanal[]>([])
  const [lista, setLista] = useState<ListaDestinos | null>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    api<TipoCanal[]>('/api/destinos/canales').then(c => { setAprobados(c); setCanales(c) }).catch(() => undefined)
    api<ListaDestinos>(`/api/versiones/${versionId}/destinos`).then(setLista).catch(() => undefined)
  }, [versionId])

  const consulta = () => canales.map(c => `canales=${c}`).join('&')
  async function ver(guardar: boolean) {
    setError('')
    try {
      setLista(guardar
        ? await api<ListaDestinos>(`/api/versiones/${versionId}/destinos`, { method: 'POST', json: { canales } })
        : await api<ListaDestinos>(`/api/versiones/${versionId}/destinos/previa?${consulta()}`))
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo calcular los destinos.') }
  }

  return (
    <UnfoldAccordion title="Destinos por comunidad" subtitle={lista ? `${lista.destinos.length} destinos · ${lista.comunidades} comunidades · ${lista.pendientes.length} pendientes${lista.guardada ? ' · guardada' : ' · vista previa'}` : 'Comunidades del área y canales aprobados'}>
      <div className="chips">{aprobados.map(c => <label key={c} className="check"><input type="checkbox" checked={canales.includes(c)}
        onChange={e => setCanales(e.target.checked ? [...canales, c] : canales.filter(x => x !== c))} /> {c}</label>)}</div>
      <div className="acciones">
        <button onClick={() => ver(false)} disabled={!canales.length}><Users size={16} aria-hidden="true" />Ver alcance</button>
        {puedeGuardar && <button className="primario" onClick={() => ver(true)} disabled={!canales.length}>Guardar lista de destinos</button>}
      </div>
      {error && <p className="error">{error}</p>}
      {lista && <>
        {lista.guardada && <p className="tenue">Guardada el {fecha(lista.generadaEn)} con {lista.canales.join(', ')}.</p>}
        {lista.destinos.length > 0 && <div className="table-scroll"><table>
          <thead><tr><th>Comunidad</th><th>Contacto</th><th>Canal</th><th>Destino</th></tr></thead>
          <tbody>{lista.destinos.map(x => <tr key={x.canal + x.direccion}><td>{x.comunidad}</td><td>{x.contacto}</td><td>{x.canal}</td><td>{x.direccion}</td></tr>)}</tbody>
        </table></div>}
        {lista.pendientes.length > 0 && <Alert tone="warning"><b>Comunidades pendientes (sin canal):</b><ul>{lista.pendientes.map(p => <li key={p.comunidadId}>{p.comunidad}: {p.motivo}</li>)}</ul></Alert>}
        {lista.destinos.length === 0 && lista.pendientes.length === 0 && <p className="tenue"><ShieldAlert size={14} aria-hidden="true" /> Ninguna comunidad registrada dentro del área.</p>}
      </>}
    </UnfoldAccordion>
  )
}
