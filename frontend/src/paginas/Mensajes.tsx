import { useEffect, useState } from 'react'
import { CheckCircle2, MessageSquareText, RefreshCw, Send, ShieldAlert, Undo2, Users, XCircle } from 'lucide-react'
import {
  api, descargarTexto, fecha, NOMBRE_ESTADO_VERSION, NOMBRE_TIPO, puedeAprobar, puedeEditar,
  type EstadoVersion, type ListaDestinos, type ResultadoDifusion, type Rol, type TipoCanal, type VersionDetalle, type VersionResumen,
} from '../api'
import Mapa from './Mapa'
import { Alert } from '../components/ui/Alert'
import { Pestanas } from '../components/ui/Pestanas'
import { useEnfocarAlAbrir } from '../components/ui/useEnfocarAlAbrir'
import { BandaNivel } from '../components/BandaNivel'
import { AyudaPanel } from '../components/AyudaPanel'
import { Dialogo } from '../components/ui/Dialogo'
import { NivelAlerta } from '../components/NivelAlerta'
import { en, NOMBRE_CERTEZA, NOMBRE_SEVERIDAD, NOMBRE_URGENCIA } from '../cap'

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
        <div><h2>Mensajes a la población</h2><p className="tenue">Alertas, actualizaciones y cancelaciones preparadas desde los avisos oficiales, con su revisión y aprobación.</p></div>
      </div>
      {puedeAprobar(roles) && !cargando && !error && cuenta('EnRevision') > 0 && filtro !== 'EnRevision' && (
        <p className="atencion" role="status"><ShieldAlert size={18} aria-hidden="true" /><span><b>{cuenta('EnRevision')} {cuenta('EnRevision') === 1 ? 'versión espera' : 'versiones esperan'} tu aprobación.</b></span><button className="primario" onClick={() => setFiltro('EnRevision')}>Revisar ahora</button></p>
      )}
      <AyudaPanel />
      {aviso && <Alert key={aviso.texto} tone={aviso.ok ? 'success' : 'danger'} dismissible>{aviso.texto}</Alert>}
      {error && <Alert key={error} tone="danger">{error} <button type="button" onClick={cargar}>Reintentar</button></Alert>}

      <div className="avisos-layout">
        <div className="list-panel">
          <div className="list-heading"><h3>Versiones <span className="count-chip">{cargando || error ? '—' : versiones.length}</span></h3><button className="icon-button" onClick={cargar} disabled={cargando} aria-label="Actualizar versiones" title="Actualizar versiones"><RefreshCw size={17} /></button></div>
          <div className="list-filters"><div className="filter-tabs" role="group" aria-label="Estado de la versión">{FILTROS.map(([id, nombre]) =>
            <button key={id} aria-pressed={filtro === id} className={filtro === id ? 'selected' : ''} onClick={() => setFiltro(id)}>{nombre}{!cargando && !error && <span className="cuenta">{cuenta(id)}</span>}</button>)}</div></div>
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
          ? <DetalleVersion key={detalle.detalle.resumen.id} v={detalle} roles={roles} accion={accion} cerrar={() => setDetalle(null)} numeroReferencia={versiones.find(x => x.id === detalle.detalle.referenciaVersionId)?.numero} />
          : <aside className="detail-empty"><span className="detail-empty-icon"><MessageSquareText size={28} aria-hidden="true" /></span><h3>Selecciona una versión</h3><p>Aquí verás su texto, su zona en el mapa, sus referencias y los destinos.</p></aside>}
      </div>
    </section>
  )
}

function EstadoChip({ estado }: { estado: EstadoVersion }) {
  return <span className={`estado-version estado-${estado}`}>{NOMBRE_ESTADO_VERSION[estado]}</span>
}

function DetalleVersion({ v, roles, accion, cerrar, numeroReferencia }: {
  v: VersionDetalle; roles: Rol[]; cerrar: () => void; numeroReferencia?: number
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

  const { panel, titulo } = useEnfocarAlAbrir<HTMLDivElement>(r.id)
  const [decidiendo, setDecidiendo] = useState<'aprobar' | 'rechazar' | null>(null)
  const [cambios, setCambios] = useState<{ campo: string; antes: string; ahora: string }[] | null>(null)

  /** Antes de aprobar se compara el texto con la versión que esta reemplaza, para que se vea qué cambia. */
  const [alcance, setAlcance] = useState<ListaDestinos | 'sin' | null>(null)

  function pedirAprobacion() {
    setCambios(null); setAlcance(null); setDecidiendo('aprobar')
    // A cuántas personas llegaría si luego se difunde (vista previa de los destinos, sin guardar nada).
    api<TipoCanal[]>('/api/destinos/canales')
      .then(cs => api<ListaDestinos>(`/api/versiones/${r.id}/destinos/previa?${cs.map(c => `canales=${c}`).join('&')}`))
      .then(setAlcance).catch(() => setAlcance('sin'))
    if (!d.referenciaVersionId) return
    api<VersionDetalle>(`/api/versiones/${d.referenciaVersionId}`).then(ant => {
      const par = (campo: string, antes: string | null, ahora: string | null) => ({ campo, antes: antes ?? '', ahora: ahora ?? '' })
      const lista = [par('Titular', ant.detalle.resumen.titular, r.titular), par('Descripción', ant.detalle.descripcion, d.descripcion), par('Instrucciones', ant.detalle.instruccion, d.instruccion)]
      setCambios(lista.filter(c => c.antes !== c.ahora))
    }).catch(() => setCambios(null))
  }

  const [confirmando, setConfirmando] = useState<'difundir' | 'cancel' | null>(null)
  const [vista, setVista] = useState<ListaDestinos | null>(null)
  const [vistaFallo, setVistaFallo] = useState(false)
  const [enviando, setEnviando] = useState(false)

  /** Antes de difundir se muestra qué saldrá y a quién: nivel, zona y destinos guardados. */
  function pedirDifusion() {
    setVista(null); setVistaFallo(false); setConfirmando('difundir')
    api<ListaDestinos>(`/api/versiones/${r.id}/destinos`).then(setVista).catch(() => setVistaFallo(true))
  }

  async function difundir() {
    setErrorDifusion(''); setEnviando(true)
    try { setDifusion(await api<ResultadoDifusion>(`/api/difusion/${r.id}`, { method: 'POST' })) }
    catch (e) { setErrorDifusion(e instanceof Error ? e.message : 'No se pudo difundir.') }
    finally { setEnviando(false); setConfirmando(null) }
  }

  const sms = vista?.destinos.filter(x => x.canal === 'Sms') ?? []
  const comunidadesSms = new Set(sms.map(x => x.comunidadId)).size

  return (
    <div className="tarjeta detail-panel" ref={panel}>
      <BandaNivel texto={r.nivel.texto} color={r.nivel.color}>Versión {r.numero} · {NOMBRE_TIPO[r.tipo]}</BandaNivel>
      <div className="detail-eyebrow"><MessageSquareText size={16} aria-hidden="true" />Detalle de la versión<button className="row-link" onClick={cerrar}>Cerrar</button></div>
      <h3 ref={titulo} tabIndex={-1}>{r.titular ?? r.evento}</h3>
      <div className="fila-chips"><EstadoChip estado={r.estado} /></div>
      {r.esPrueba && <Alert tone="warning">Aviso de prueba o simulacro: no se publica en la web pública y el SMS real se bloquea.</Alert>}
      {r.reemplazadaPorId && <Alert tone="info">Esta versión fue reemplazada por una actualización o cancelación aprobada.</Alert>}
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
          <p className="tenue">Lee el texto de cada canal, la zona, la fuente oficial y los destinos antes de decidir. Aprobar y Rechazar están en la barra del pie, siempre a la vista.</p>
        </div>
      )}

      <Pestanas etiqueta="Detalle de la versión" pestanas={[
        { id: 'contenido', nombre: 'Contenido', contenido: <>
          <div className="texto-canales">
            <div><h4 className="subtitulo">SMS y altavoz</h4><p>{v.textoSms}</p></div>
            <div><h4 className="subtitulo">Radio y web (lectura)</h4><p className="pre">{v.textoLectura}</p></div>
          </div>
          {v.area && <Mapa alto={220} capas={[{ clave: r.id, geometria: v.area, color: r.nivel.color, etiqueta: `${r.nivel.texto} · ${r.descripcionArea ?? ''}` }]} />}
        </> },
        { id: 'fuente', nombre: 'Fuente', contenido: (
          <dl>
            <dt>Fuente oficial</dt><dd>{d.fuenteRemitente} · {d.fuenteIdentificador} ({NOMBRE_TIPO[d.fuenteTipo]}, {fecha(d.fuenteEnviado)})</dd>
            <dt>Hechos</dt><dd>{r.evento} · urgencia {en(NOMBRE_URGENCIA, d.urgencia).toLowerCase()} · severidad {en(NOMBRE_SEVERIDAD, d.severidad).toLowerCase()} · certeza {en(NOMBRE_CERTEZA, d.certeza).toLowerCase()}</dd>
            <dt>Vigencia</dt><dd>{fecha(d.vigente)} → {fecha(r.expira)}</dd>
            <dt>Zona</dt><dd>{r.descripcionArea ?? '—'}</dd>
            {d.referencias && <><dt>Referencias</dt><dd className="codigo-linea">{d.referencias}</dd></>}
            {r.revisadoEn && <><dt>Revisión</dt><dd>{NOMBRE_ESTADO_VERSION[r.estado]} el {fecha(r.revisadoEn)}</dd></>}
          </dl>) },
        { id: 'destinos', nombre: 'Destinos', mantener: true, contenido: <Destinos versionId={r.id} puedeGuardar={editar && vigente} /> },
        { id: 'tecnico', nombre: 'Técnico', contenido: <>
          <dl>
            <dt>Identificador CAP</dt><dd>{d.identificadorCap}</dd>
            <dt>Remitente</dt><dd>{d.remitente}</dd>
            {d.enviadoCap && <><dt>Generado</dt><dd>{fecha(d.enviadoCap)}</dd></>}
          </dl>
          {d.xmlCap ? <><p className="tenue">Mensaje en formato CAP 1.2, generado y validado al enviar la versión a revisión.</p><div className="acciones"><button onClick={() => descargarTexto(d.xmlCap!, 'application/xml', `${d.identificadorCap}.xml`)}>Descargar XML</button></div><pre>{d.xmlCap}</pre></>
            : <p className="tenue">El mensaje CAP se genera cuando la versión se envía a revisión.</p>}
        </> },
      ]} />

      {difusion && <Alert tone={difusion.incidencias.length ? 'warning' : 'success'}>Difundida{difusion.esPrueba ? ' (prueba)' : ''}: web {difusion.publicadoEnWeb ? 'sí' : 'no'}, {difusion.trabajosSms} SMS, {difusion.tareasManuales.length} tareas manuales.{difusion.incidencias.map(i => <div key={i.canal + i.detalle}>{i.canal}: {i.detalle}</div>)}</Alert>}
      {errorDifusion && <Alert tone="danger">{errorDifusion}</Alert>}

      <div className="acciones">
        {d.xmlCap && <button className="solo-escritorio" onClick={() => descargarTexto(d.xmlCap!, 'application/xml', `${d.identificadorCap}.xml`)}>Descargar XML</button>}
        {r.estado === 'Rechazada' && editar && <button className="primario" onClick={() => accion('Se creó una versión nueva en borrador para corregir.', () => api(`${ruta}/corregir`, { method: 'POST' }))}><Undo2 size={16} aria-hidden="true" />Corregir (nueva versión)</button>}
        {vigente && editar && r.tipo !== 'Cancel' && <>
          <button onClick={() => accion('Actualización preparada como borrador.', () => api(`${ruta}/update`, { method: 'POST', json: {} }))}>Preparar actualización</button>
          <button className="peligro" onClick={() => setConfirmando('cancel')}>Preparar cancelación</button>
        </>}
        {vigente && aprobar && <button className="primario ancho-movil" onClick={pedirDifusion}><Send size={16} aria-hidden="true" />Difundir</button>}
        {r.estado === 'EnRevision' && aprobar && <>
          <button className="peligro" onClick={() => { setMotivo(''); setDecidiendo('rechazar') }}><XCircle size={16} aria-hidden="true" />Rechazar</button>
          <button className="primario" onClick={pedirAprobacion}><CheckCircle2 size={16} aria-hidden="true" />Aprobar</button>
        </>}
      </div>

      <Dialogo abierto={decidiendo === 'aprobar'} titulo="¿Aprobar esta versión?" alCerrar={() => setDecidiendo(null)}
        pie={<>
          <button autoFocus onClick={() => setDecidiendo(null)}>Cancelar</button>
          <button className="primario" onClick={() => { setDecidiendo(null); accion('Versión aprobada. Ya no se puede editar.', () => api(`${ruta}/aprobar`, { method: 'POST' })) }}><CheckCircle2 size={16} aria-hidden="true" />Aprobar versión</button>
        </>}>
        <div className="fila-chips"><NivelAlerta nivel={r.nivel} grande /></div>
        <p><b>{r.titular ?? r.evento}</b></p>
        <Alert tone="warning">Al aprobarla pasa a ser el mensaje oficial: ya no se puede editar y la web pública la muestra de inmediato. Solo se corrige con una actualización o una cancelación.</Alert>
        {alcance && alcance !== 'sin' && (() => {
          const sms = alcance.destinos.filter(x => x.canal === 'Sms')
          const comunidades = new Set(sms.map(x => x.comunidadId)).size
          return <p>Si después se difunde: <b>{sms.length} SMS</b> a {comunidades} comunidad{comunidades === 1 ? '' : 'es'}{alcance.pendientes.length > 0 ? `; ${alcance.pendientes.length} comunidad(es) del área no tienen canal.` : '.'}</p>
        })()}
        {alcance === 'sin' && <p className="tenue">No se pudo calcular a cuántas personas llegaría.</p>}
        {d.referenciaVersionId
          ? <p>Reemplaza a la versión {numeroReferencia ?? 'anterior'} de este evento ({NOMBRE_TIPO[r.tipo].toLowerCase()}).</p>
          : <p className="tenue">Es la primera versión de este evento.</p>}
        {cambios && (cambios.length
          ? <div className="cambios"><b>Qué cambió frente a la versión anterior</b>
            {cambios.map(c => <div key={c.campo}><span className="tenue">{c.campo}</span><p><s>{c.antes || '—'}</s></p><p>{c.ahora || '—'}</p></div>)}</div>
          : <p className="tenue">El texto es igual al de la versión anterior.</p>)}
      </Dialogo>

      <Dialogo abierto={decidiendo === 'rechazar'} titulo="Rechazar esta versión" alCerrar={() => setDecidiendo(null)}
        pie={<>
          <button autoFocus onClick={() => setDecidiendo(null)}>Cancelar</button>
          <button className="peligro" disabled={motivo.trim().length < 5} onClick={() => { setDecidiendo(null); accion('Versión rechazada: vuelve al operador con el motivo.', () => api(`${ruta}/rechazar`, { method: 'POST', json: { motivo } })) }}><XCircle size={16} aria-hidden="true" />Rechazar versión</button>
        </>}>
        <p>Vuelve al operador con tu motivo y no se publica nada.</p>
        <label>Motivo del rechazo<textarea value={motivo} onChange={e => setMotivo(e.target.value)} placeholder="Obligatorio, mínimo 5 caracteres" /></label>
      </Dialogo>

      <Dialogo abierto={confirmando === 'difundir'} titulo="¿Difundir a la población?" alCerrar={() => !enviando && setConfirmando(null)}
        pie={<>
          <button autoFocus onClick={() => setConfirmando(null)} disabled={enviando}>Cancelar</button>
          <button className="primario" onClick={difundir} disabled={enviando}><Send size={16} aria-hidden="true" />{enviando ? 'Difundiendo…' : `Difundir ${r.nivel.texto}`}</button>
        </>}>
        <div className="fila-chips"><NivelAlerta nivel={r.nivel} grande /></div>
        <p><b>{r.titular ?? r.evento}</b></p>
        <dl>
          <dt>Zona</dt><dd>{r.descripcionArea ?? '—'}</dd>
          <dt>Vigencia</dt><dd>{fecha(d.vigente)} → {fecha(r.expira)}</dd>
          <dt>Web pública</dt><dd>{r.esPrueba ? 'No (es un simulacro)' : 'Sí, se publica de inmediato'}</dd>
          <dt>SMS</dt><dd>{vistaFallo ? 'No se pudo calcular' : !vista ? 'Calculando…' : r.esPrueba ? 'Bloqueado (simulacro)' : `${sms.length} contacto(s) en ${comunidadesSms} comunidad(es)`}</dd>
          <dt>Radio, altavoz y redes</dt><dd>Se crean las tareas para el equipo</dd>
        </dl>
        {vistaFallo && <Alert tone="warning">No se pudo consultar la lista de destinos. Puedes difundir igual, pero no verás cuántos SMS saldrán.</Alert>}
        {vista && !vista.guardada && !r.esPrueba && <Alert tone="warning">No hay una lista de destinos guardada: no saldrá ningún SMS. Cancela, guárdala en «Destinos por comunidad» y vuelve a difundir.</Alert>}
        {vista && vista.pendientes.length > 0 && <Alert tone="warning">{vista.pendientes.length} comunidad(es) del área no tienen canal y no recibirán SMS: {vista.pendientes.map(p => p.comunidad).join(', ')}.</Alert>}
        {r.estado === 'Difundida' && <Alert tone="info">Esta versión ya se difundió el {fecha(d.difundidaEn)}. Se volverá a enviar.</Alert>}
        <p className="tenue">Una difusión no se puede deshacer: solo se corrige con una actualización o una cancelación.</p>
      </Dialogo>

      <Dialogo abierto={confirmando === 'cancel'} titulo="¿Preparar la cancelación?" alCerrar={() => setConfirmando(null)}
        pie={<>
          <button autoFocus onClick={() => setConfirmando(null)}>Volver</button>
          <button className="peligro" onClick={() => { setConfirmando(null); accion('Cancelación preparada como borrador.', () => api(`${ruta}/cancel`, { method: 'POST', json: { instruccion: 'El aviso fue cancelado. Puede retomar sus actividades con precaución.' } })) }}>Preparar cancelación</button>
        </>}>
        <p>Se crea un borrador de cancelación de «{r.titular ?? r.evento}». Todavía no se envía nada: debe pasar por revisión y aprobación, y recién entonces reemplaza a esta versión.</p>
      </Dialogo>
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
    <div className="destinos">
      <p className="tenue">{lista ? `${lista.destinos.length} destinos · ${lista.comunidades} comunidades · ${lista.pendientes.length} pendientes${lista.guardada ? ' · lista guardada' : ' · vista previa'}` : 'Comunidades del área y canales aprobados'}</p>
      <div className="chips">{aprobados.map(c => <label key={c} className="check"><input type="checkbox" checked={canales.includes(c)}
        onChange={e => setCanales(e.target.checked ? [...canales, c] : canales.filter(x => x !== c))} /> {c}</label>)}</div>
      <div className="acciones">
        <button onClick={() => ver(false)} disabled={!canales.length}><Users size={16} aria-hidden="true" />Ver alcance</button>
        {puedeGuardar && <button className="primario" onClick={() => ver(true)} disabled={!canales.length}>Guardar lista de destinos</button>}
      </div>
      {error && <p className="error" role="status">{error}</p>}
      {lista && <>
        {lista.guardada && <p className="tenue">Guardada el {fecha(lista.generadaEn)} con {lista.canales.join(', ')}.</p>}
        {lista.destinos.length > 0 && <div className="table-scroll"><table>
          <thead><tr><th>Comunidad</th><th>Contacto</th><th>Canal</th><th>Destino</th></tr></thead>
          <tbody>{lista.destinos.map(x => <tr key={x.canal + x.direccion}><td>{x.comunidad}</td><td>{x.contacto}</td><td>{x.canal}</td><td>{x.direccion}</td></tr>)}</tbody>
        </table></div>}
        {lista.pendientes.length > 0 && <Alert tone="warning"><b>Comunidades pendientes (sin canal):</b><ul>{lista.pendientes.map(p => <li key={p.comunidadId}>{p.comunidad}: {p.motivo}</li>)}</ul></Alert>}
        {lista.destinos.length === 0 && lista.pendientes.length === 0 && <p className="tenue"><ShieldAlert size={14} aria-hidden="true" /> Ninguna comunidad registrada dentro del área.</p>}
      </>}
    </div>
  )
}
