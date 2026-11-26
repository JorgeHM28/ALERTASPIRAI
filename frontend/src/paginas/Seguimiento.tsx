import { useCallback, useEffect, useState } from 'react'
import { CheckCircle2, Download, RefreshCw, XCircle } from 'lucide-react'
import {
  api, descargarTexto, fecha, puedeEditar,
  type CanalDifusion, type ConteoEstados, type EstadoTrabajo, type Rol, type SeguimientoDetalle, type SeguimientoResumen, type TareaVista,
} from '../api'
import { Alert } from '../components/ui/Alert'

/** RF-08 pide que el panel se actualice en menos de 1 minuto: se vuelve a pedir cada 10 s. */
const REFRESCO_MS = 10_000

const NOMBRE_CANAL: Record<CanalDifusion, string> = { Sms: 'SMS', Radio: 'Radio', Altavoz: 'Altavoz', RedesSociales: 'Redes sociales' }
const NOMBRE_ESTADO: Record<EstadoTrabajo, string> = {
  Pendiente: 'Pendiente', Enviado: 'Enviado', Fallido: 'Fallido', Bloqueado: 'Bloqueado', Confirmada: 'Realizada', NoRealizada: 'No realizada',
}

function Estado({ estado }: { estado: EstadoTrabajo | null }) {
  if (!estado) return <span className="tenue">—</span>
  return <span className={`estado-trabajo estado-trabajo-${estado}`}>{NOMBRE_ESTADO[estado]}</span>
}

/** Resumen de un conteo en palabras, sin depender del color. */
function TextoConteo({ c, tareas }: { c: ConteoEstados; tareas?: boolean }) {
  if (c.total === 0) return <span className="tenue">ninguno</span>
  const partes = tareas
    ? [`${c.confirmadas} realizadas`, c.noRealizadas ? `${c.noRealizadas} no realizadas` : '', c.pendientes ? `${c.pendientes} pendientes` : '']
    : [`${c.enviados} enviados`, c.fallidos ? `${c.fallidos} fallidos` : '', c.bloqueados ? `${c.bloqueados} bloqueados` : '', c.pendientes ? `${c.pendientes} pendientes` : '']
  return <span>{partes.filter(Boolean).join(' · ')} <span className="tenue">de {c.total}</span></span>
}

function Barra({ avance }: { avance: number }) {
  return <div className="avance" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={avance} aria-label={`Avance ${avance} %`}>
    <span style={{ width: `${avance}%` }} /><b>{avance} %</b>
  </div>
}

/** RF-08: seguimiento de la difusión por comunidad y canal. Se actualiza solo. */
export default function Seguimiento({ roles }: { roles: Rol[] }) {
  const [lista, setLista] = useState<SeguimientoResumen[]>([])
  const [elegida, setElegida] = useState<string | null>(null)
  const [detalle, setDetalle] = useState<SeguimientoDetalle | null>(null)
  const [error, setError] = useState('')
  const [cargando, setCargando] = useState(true)

  const cargar = useCallback(async () => {
    try {
      const l = await api<SeguimientoResumen[]>('/api/difusion/seguimiento')
      setLista(l)
      const id = elegida ?? l[0]?.versionId ?? null
      if (id !== elegida) setElegida(id)
      setDetalle(id ? await api<SeguimientoDetalle>(`/api/difusion/seguimiento/${id}`) : null)
      setError('')
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo cargar el seguimiento.') }
    finally { setCargando(false) }
  }, [elegida])

  useEffect(() => {
    cargar()
    const t = window.setInterval(cargar, REFRESCO_MS)
    return () => window.clearInterval(t)
  }, [cargar])

  return <section>
    <div className="barra">
      <div><h2>Seguimiento de la difusión</h2>
        <p className="tenue">Qué salió, a qué comunidades y por qué canal. Se actualiza solo cada 10 segundos.</p></div>
      <button className="icon-button" onClick={cargar} aria-label="Actualizar ahora" title="Actualizar ahora"><RefreshCw size={17} /></button>
    </div>
    {error && <Alert tone="danger">{error}</Alert>}
    {!cargando && lista.length === 0 && !error && <Alert tone="info">Todavía no se difundió ningún mensaje. Cuando un aprobador difunda una versión, su avance aparece aquí.</Alert>}

    {lista.length > 0 && <div className="table-scroll"><table>
      <thead><tr><th>Mensaje</th><th>Difundido</th><th>SMS</th><th>Tareas</th><th>Avance</th></tr></thead>
      <tbody>{lista.map(r => <tr key={r.versionId} aria-selected={r.versionId === elegida}>
        <td><button className="row-link" onClick={() => setElegida(r.versionId)}>
          {r.tipo} v{r.numero}: {r.titular}{r.esPrueba ? ' (prueba)' : ''}</button></td>
        <td>{fecha(r.difundidaEn)}</td>
        <td><TextoConteo c={r.sms} /></td>
        <td><TextoConteo c={r.tareas} tareas /></td>
        <td><Barra avance={r.avance} />{r.completo && <span className="tenue"> completo</span>}</td>
      </tr>)}</tbody>
    </table></div>}

    {detalle && <Detalle d={detalle} editar={puedeEditar(roles)} alCambiar={cargar} />}
  </section>
}

function Detalle({ d, editar, alCambiar }: { d: SeguimientoDetalle; editar: boolean; alCambiar: () => void }) {
  const r = d.resumen
  async function descargarCap() {
    const xml = await api<string>(`/api/difusion/${r.versionId}/cap`)
    descargarTexto(xml, 'application/xml', `${d.cap?.identificador ?? 'cap'}.xml`)
  }
  return <div className="tarjeta interior seguimiento">
    <div className="barra"><div><h3>{r.tipo} v{r.numero}: {r.titular}</h3>
      <p className="tenue">Difundido {fecha(r.difundidaEn)} · actualizado {fecha(d.actualizadoEn)}</p></div>
      <Barra avance={r.avance} /></div>

    <dl className="datos-seguimiento">
      <div><dt>CAP emitido</dt><dd>{d.cap
        ? <>{d.cap.identificador} ({d.cap.valido ? 'válido CAP 1.2' : 'no válido'}) <button className="row-link" onClick={descargarCap}><Download size={14} aria-hidden="true" />XML</button>
          <br /><small className="tenue">SHA-256 {d.cap.sha256.slice(0, 16)}…</small></>
        : '—'}</dd></div>
      <div><dt>Web pública</dt><dd>{d.publicadaWebEn ? `Publicado ${fecha(d.publicadaWebEn)}` : 'No publicado'}</dd></div>
      <div><dt>SMS</dt><dd><TextoConteo c={r.sms} /></dd></div>
      <div><dt>Meta de 5 minutos (RNF-02)</dt><dd>{d.dentroDeMeta === null
        ? (r.sms.total === 0 ? 'Sin SMS' : 'En curso')
        : d.dentroDeMeta ? `Cumplida: terminó ${fecha(d.smsTerminadosEn)}` : `No cumplida: terminó ${fecha(d.smsTerminadosEn)}`}</dd></div>
    </dl>

    <h4>Por comunidad</h4>
    {d.comunidades.length === 0 ? <p className="tenue">Ninguna comunidad recibió SMS ni tareas de altavoz.</p>
      : <div className="table-scroll"><table>
        <thead><tr><th>Comunidad</th><th>Municipio</th><th>SMS</th><th>Altavoz</th></tr></thead>
        <tbody>{d.comunidades.map(c => <tr key={c.comunidadId ?? c.comunidad}>
          <td>{c.comunidad}</td><td>{c.municipio ?? '—'}</td><td><TextoConteo c={c.sms} /></td><td><Estado estado={c.altavoz} /></td>
        </tr>)}</tbody></table></div>}

    <h4>Tareas manuales</h4>
    <Tareas tareas={d.tareas} editar={editar} alCambiar={alCambiar} />

    {d.envios.length > 0 && <>
      <h4>Envíos SMS</h4>
      <div className="table-scroll"><table>
        <thead><tr><th>Comunidad</th><th>Teléfono</th><th>Estado</th><th>Intentos</th><th>Detalle</th></tr></thead>
        <tbody>{d.envios.map(e => <tr key={e.id}>
          <td>{e.comunidad ?? '—'}</td><td>{e.destino}</td><td><Estado estado={e.estado} /></td><td>{e.intentos}</td>
          <td>{e.estado === 'Pendiente' && e.proximoIntento ? `Próximo intento ${fecha(e.proximoIntento)}` : e.ultimoError ?? e.codigoProveedor ?? ''}</td>
        </tr>)}</tbody></table></div>
    </>}
  </div>
}

function Tareas({ tareas, editar, alCambiar }: { tareas: TareaVista[]; editar: boolean; alCambiar: () => void }) {
  const [abierta, setAbierta] = useState<{ id: string; modo: 'confirmar' | 'no-realizada' } | null>(null)
  const [texto, setTexto] = useState('')
  const [error, setError] = useState('')
  async function cerrar(e: { preventDefault: () => void }) {
    e.preventDefault()
    if (!abierta) return
    try {
      await api(`/api/difusion/tareas/${abierta.id}/${abierta.modo}`, { method: 'POST', json: { observacion: texto || null } })
      setAbierta(null); setTexto(''); setError(''); alCambiar()
    } catch (err) { setError(err instanceof Error ? err.message : 'No se pudo guardar.') }
  }
  if (tareas.length === 0) return <p className="tenue">No hay tareas manuales.</p>
  return <ul className="lista-tareas">{tareas.map(t => <li key={t.id}>
    <div className="barra">
      <div><b>{NOMBRE_CANAL[t.canal]}{t.comunidad ? ` · ${t.comunidad}` : ''}</b> <Estado estado={t.estado} />
        {t.confirmadoPor && <small className="tenue"> por {t.confirmadoPor}, {fecha(t.resultadoEn)}</small>}</div>
      {editar && t.estado === 'Pendiente' && <div className="acciones">
        <button className="primario" onClick={() => { setAbierta({ id: t.id, modo: 'confirmar' }); setTexto('') }}><CheckCircle2 size={16} aria-hidden="true" />Realizada</button>
        <button onClick={() => { setAbierta({ id: t.id, modo: 'no-realizada' }); setTexto('') }}><XCircle size={16} aria-hidden="true" />No se pudo</button>
      </div>}
    </div>
    <p className="texto-tarea">{t.textoLegible}</p>
    {t.observacion && <p className="tenue">Observación: {t.observacion}</p>}
    {abierta?.id === t.id && <form onSubmit={cerrar} className="tarjeta interior">
      <label>{abierta.modo === 'confirmar' ? 'Observación (opcional)' : 'Motivo (obligatorio)'}
        <input value={texto} onChange={e => setTexto(e.target.value)} required={abierta.modo === 'no-realizada'} maxLength={500} autoFocus /></label>
      {error && <Alert tone="danger">{error}</Alert>}
      <div className="acciones"><button className="primario">Guardar</button><button type="button" onClick={() => setAbierta(null)}>Cancelar</button></div>
    </form>}
  </li>)}</ul>
}
