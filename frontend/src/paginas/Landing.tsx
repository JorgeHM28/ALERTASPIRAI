import { useEffect, useState } from 'react'
import { ArrowRight, BellRing, Clock3, MapPin, RefreshCw, Waves } from 'lucide-react'
import { api, fecha, NOMBRE_TIPO, type AvisoPublico } from '../api'
import { aclararSobre } from '../color'
import Mapa from './Mapa'
import { NivelAlerta } from '../components/NivelAlerta'

const NIVELES = [
  { codigo: 'roja', texto: 'Alerta roja', color: '#C62828', orden: 4, que: 'Peligro inminente. Siga de inmediato las instrucciones de evacuación.' },
  { codigo: 'naranja', texto: 'Alerta naranja', color: '#EF6C00', orden: 3, que: 'Peligro probable. Prepare su mochila de emergencia y esté listo para salir.' },
  { codigo: 'amarilla', texto: 'Alerta amarilla', color: '#F9A825', orden: 2, que: 'Situación a vigilar. Manténgase informado por los canales oficiales.' },
  { codigo: 'verde', texto: 'Alerta verde', color: '#2E7D32', orden: 1, que: 'Sin peligro previsto. Conozca su ruta de evacuación.' },
]
const COLOR_CANCELADO = '#687783'
const COLOR_SIN_AVISOS = '#2E7D32'

/** El titular oficial suele repetir el nivel ("Alerta naranja por…"): si ya lo dice, se usa el nombre del evento. */
const tituloDe = (a: AvisoPublico) => {
  const t = a.titular ?? a.evento
  return t.toLowerCase().startsWith(a.nivel.texto.toLowerCase()) ? a.evento : t
}
/** "hace 20 min" / "hace 3 h": las cancelaciones solo se muestran 24 horas, así que no hace falta más escala. */
const hace = (iso: string | null) => {
  if (!iso) return ''
  const min = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000))
  if (min < 1) return 'hace un momento'
  return min < 60 ? `hace ${min} min` : `hace ${Math.floor(min / 60)} h`
}
const hora = (d: Date) => d.toLocaleTimeString('es-BO', { hour: '2-digit', minute: '2-digit' })

/**
 * Página de inicio pública, sin sesión. Muestra solo mensajes aprobados: texto, nivel (con palabras), instrucciones
 * y zona en el mapa (RF-06). Nunca contactos ni datos internos.
 * Lo primero es el estado: una banda con el nivel en texto grande, qué hacer y la zona; el mapa y la guía van después.
 */
export default function Landing({ alIngresar }: { alIngresar: () => void }) {
  const [avisos, setAvisos] = useState<AvisoPublico[]>([])
  const [enlazado, setEnlazado] = useState<AvisoPublico | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [consultadoEn, setConsultadoEn] = useState<Date | null>(null)
  const idEnlace = new URLSearchParams(window.location.search).get('aviso')

  const cargar = () => {
    setCargando(true); setError('')
    return api<AvisoPublico[]>('/api/publico/avisos?cancelados=true').then(a => { setAvisos(a); setConsultadoEn(new Date()) })
      .catch(() => setError('No se pudo consultar los avisos. Intente de nuevo en unos minutos.'))
      .finally(() => setCargando(false))
  }
  useEffect(() => {
    cargar()
    if (idEnlace) api<AvisoPublico>(`/api/publico/avisos/${idEnlace}`).then(setEnlazado).catch(() => setEnlazado(null))
  }, [idEnlace])

  const activos = avisos.filter(a => a.vigente && a.tipo !== 'Cancel').sort((a, b) => b.nivel.orden - a.nivel.orden)
  const cancelados = avisos.filter(a => a.tipo === 'Cancel')
  // Si llegan por un enlace a un aviso concreto, ese es el que ocupa la banda.
  const principal = enlazado ?? activos[0] ?? null
  const otros = activos.filter(a => a.id !== principal?.id)
  const cancelado = principal?.tipo === 'Cancel'
  const color = principal ? (cancelado ? COLOR_CANCELADO : principal.nivel.color) : error || (cargando && !consultadoEn) ? null : COLOR_SIN_AVISOS
  const estilo = color ? { '--nivel': color, '--acento': aclararSobre(color, '#0b1f2a') } as React.CSSProperties : undefined

  return (
    <div className="publico">
      <a className="saltar skip-link" href="#avisos-publicos">Saltar a los avisos</a>
      <header className="publico-header">
        <div className="marca"><span className="marca-simbolo"><Waves size={24} aria-hidden="true" /></span>
          <div><strong>SAT Río Piraí</strong><span className="marca-descripcion">ALERTAS TEMPRANAS · SANTA CRUZ</span></div></div>
        <button className="primario" onClick={alIngresar}>Ingresar al sistema<ArrowRight size={17} aria-hidden="true" /></button>
      </header>

      <section id="avisos-publicos" tabIndex={-1} className="estado" style={estilo} aria-live="polite" aria-busy={cargando}>
        <svg className="estado-olas" viewBox="0 0 1280 760" preserveAspectRatio="xMidYMid slice" aria-hidden="true"><g fill="none" stroke="#8bd6dc" strokeOpacity=".16" strokeWidth="2">
          {[520, 560, 600, 640, 680, 720].map(y => <path key={y} d={`M-20 ${y}C160 ${y - 50} 260 ${y + 60} 440 ${y + 10}S700 ${y - 70} 860 ${y - 20}S1120 ${y + 40} 1300 ${y - 20}`} />)}
        </g></svg>
        <div className="estado-interior">
          {principal ? <>
            <Titular palabras={cancelado ? 'Aviso cancelado' : principal.nivel.texto} detalle={tituloDe(principal)} />
            <div className="estado-cuerpo">
              {principal.instruccion && <div className="estado-hacer"><b>Qué hacer</b><p>{principal.instruccion}</p></div>}
              <div className="estado-datos">
                <div><span className="etq">Zona</span><strong><MapPin size={16} aria-hidden="true" />{principal.descripcionArea ?? 'Zona del aviso'}</strong></div>
                <div><span className="etq">{cancelado ? 'Cancelado' : 'Vigente hasta'}</span><strong><Clock3 size={16} aria-hidden="true" />{cancelado ? fecha(principal.aprobadoEn) : fecha(principal.hasta)}</strong></div>
                {principal.descripcion && <div><span className="etq">Qué pasa</span><p>{principal.descripcion}</p></div>}
              </div>
            </div>
            {otros.length > 0 && <p className="estado-descripcion"><a href="#otros-avisos">Hay {otros.length} aviso{otros.length > 1 ? 's' : ''} más en otras zonas</a></p>}
          </> : error ? <>
            <Titular palabras="Sin conexión" />
            <div className="estado-cuerpo"><div className="estado-hacer"><p>{error}</p></div></div>
          </> : cargando && !consultadoEn ? <Titular palabras="Consultando avisos…" /> : <>
            <Titular palabras="Sin avisos vigentes" />
            <div className="estado-cuerpo"><div className="estado-hacer"><p>No hay avisos oficiales activos para la cuenca del río Piraí. Esto no reemplaza las indicaciones de las autoridades: siga informándose por los canales oficiales.</p></div></div>
          </>}
          <p className="estado-pie">
            <span>{consultadoEn ? `Consultado a las ${hora(consultadoEn)}` : 'Consultando…'}</span>
            <button onClick={cargar} disabled={cargando}><RefreshCw size={15} className={cargando ? 'girando' : undefined} aria-hidden="true" />{cargando ? 'Actualizando avisos…' : 'Actualizar avisos'}</button>
          </p>
        </div>
      </section>

      <main className="publico-main">
        {otros.length > 0 && <section className="publico-lista" id="otros-avisos">
          <h2>Otros avisos vigentes</h2>
          {otros.map(a => <TarjetaAviso key={a.id} a={a} />)}
        </section>}

        <section className="publico-zonas" aria-label="Zonas con aviso">
          <div>
            <h2>Zonas con aviso</h2>
            {activos.length > 0 ? <ul className="zonas-lista">
              {activos.map(a => <li key={a.id}><NivelAlerta nivel={a.nivel} /><span>{a.descripcionArea ?? 'Zona del aviso'}</span></li>)}
            </ul> : error ? <p className="tenue">No se pudo comprobar qué zonas tienen un aviso vigente.</p>
              : <p className="tenue">Ninguna zona tiene un aviso vigente en este momento.</p>}
            {/* Una cancelación confirma que un aviso que alguien pudo recibir ya no rige: va aquí, junto a las zonas, y breve. */}
            {cancelados.length > 0 && <>
              <p className="cancelados-intro">Cancelados en las últimas 24 horas</p>
              <ul className="zonas-lista zonas-canceladas" aria-label="Avisos cancelados en las últimas 24 horas">{cancelados.map(a => <li key={a.id}>
                <span className="nivel-alerta cancelado">Cancelado</span>
                <span>{a.descripcionArea ?? 'Zona del aviso'} · <time dateTime={a.aprobadoEn ?? undefined} title={fecha(a.aprobadoEn)}>{hace(a.aprobadoEn)}</time></span>
                {a.instruccion && <span className="cancelados-texto">{a.instruccion}</span>}
              </li>)}</ul>
            </>}
            <p className="tenue">Si su zona no aparece aquí, no tiene un aviso oficial activo. Ante cualquier duda, siga las indicaciones de las autoridades.</p>
          </div>
          <div>
            <Mapa zoomMaximo={11} capas={activos.filter(a => a.area).map(a => ({ clave: a.id, geometria: a.area!, color: a.nivel.color, etiqueta: `${a.nivel.texto}: ${a.titular ?? a.evento}` }))} />
            <ul className="leyenda" aria-label="Niveles de alerta">{NIVELES.map(n => <li key={n.codigo}><NivelAlerta nivel={n} /></li>)}</ul>
          </div>
        </section>

        <section className="publico-niveles"><h2>Niveles de alerta: qué significa cada uno</h2>
          <div className="niveles-grid">{NIVELES.map(n => <div key={n.codigo} className="nivel-col" style={{ '--c': n.color } as React.CSSProperties}>
            <div className="nivel-barra" aria-hidden="true" />
            <strong className="nivel-nombre">{n.texto}</strong>
            {activos.some(a => a.nivel.codigo === n.codigo) && <span className="nivel-actual">Nivel actual</span>}
            <p>{n.que}</p></div>)}</div>
        </section>
      </main>
      <footer className="app-footer publico-footer"><span>SAT Río Piraí · Gestión del riesgo</span><span>Santa Cruz · Bolivia</span></footer>
    </div>
  )
}

/** El nivel en dos líneas de letra monumental: la primera palabra con el color del nivel y el resto en blanco. El detalle (nombre del evento) va debajo. */
function Titular({ palabras, detalle }: { palabras: string; detalle?: string }) {
  const [primera, ...resto] = palabras.split(' ')
  const segunda = resto.join(' ')
  const largo = Math.max(primera.length, segunda.length)
  return (
    <h1 className="estado-titular" data-tam={largo <= 8 ? 'g' : largo <= 12 ? 'm' : 'p'}>
      <span className="estado-palabra">{primera}</span>
      {segunda && <span className="estado-nivel">{segunda}</span>}
      {detalle && <span className="estado-titulo">{detalle}</span>}
    </h1>
  )
}

function TarjetaAviso({ a }: { a: AvisoPublico }) {
  const cancelado = a.tipo === 'Cancel'
  return (
    <article className="tarjeta aviso-publico" style={{ '--nivel': cancelado ? COLOR_CANCELADO : a.nivel.color } as React.CSSProperties}>
      <div className="aviso-publico-cabecera">
        {cancelado ? <span className="nivel-alerta grande cancelado">Aviso cancelado</span> : <NivelAlerta nivel={a.nivel} grande />}
        {a.tipo === 'Update' && <span className="estado-version estado-EnRevision">{NOMBRE_TIPO.Update}</span>}
        {!a.vigente && !cancelado && <span className="estado-version estado-Rechazada">Ya no está vigente</span>}
      </div>
      <h3><BellRing size={18} aria-hidden="true" /> {a.titular ?? a.evento}</h3>
      {a.descripcion && <p>{a.descripcion}</p>}
      {a.instruccion && <div className="aviso-instruccion"><b>Qué hacer</b><p>{a.instruccion}</p></div>}
      <p className="tenue aviso-meta"><MapPin size={14} aria-hidden="true" />{a.descripcionArea ?? 'Zona del aviso'} · <Clock3 size={14} aria-hidden="true" />{cancelado ? `Cancelado ${fecha(a.aprobadoEn)}` : `Vigente hasta ${fecha(a.hasta)}`}</p>
    </article>
  )
}
