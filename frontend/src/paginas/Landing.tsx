import { useEffect, useState } from 'react'
import { ArrowRight, BellRing, Clock3, MapPin, RefreshCw, ShieldCheck, Waves } from 'lucide-react'
import { api, fecha, NOMBRE_TIPO, type AvisoPublico } from '../api'
import Mapa from './Mapa'
import { NivelAlerta } from '../components/NivelAlerta'

const NIVELES = [
  { codigo: 'roja', texto: 'Alerta roja', color: '#C62828', orden: 4, que: 'Peligro inminente. Siga de inmediato las instrucciones de evacuación.' },
  { codigo: 'naranja', texto: 'Alerta naranja', color: '#EF6C00', orden: 3, que: 'Peligro probable. Prepare su mochila de emergencia y esté listo para salir.' },
  { codigo: 'amarilla', texto: 'Alerta amarilla', color: '#F9A825', orden: 2, que: 'Situación a vigilar. Manténgase informado por los canales oficiales.' },
  { codigo: 'verde', texto: 'Alerta verde', color: '#2E7D32', orden: 1, que: 'Sin peligro previsto. Conozca su ruta de evacuación.' },
]

/**
 * Página de inicio pública, sin sesión. Muestra solo mensajes aprobados: texto, nivel (con palabras), instrucciones
 * y zona en el mapa (RF-06). Nunca contactos ni datos internos.
 */
export default function Landing({ alIngresar }: { alIngresar: () => void }) {
  const [avisos, setAvisos] = useState<AvisoPublico[]>([])
  const [enlazado, setEnlazado] = useState<AvisoPublico | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const idEnlace = new URLSearchParams(window.location.search).get('aviso')

  const cargar = () => {
    setCargando(true); setError('')
    return api<AvisoPublico[]>('/api/publico/avisos?cancelados=true').then(setAvisos)
      .catch(() => setError('No se pudo consultar los avisos. Intente de nuevo en unos minutos.'))
      .finally(() => setCargando(false))
  }
  useEffect(() => {
    cargar()
    if (idEnlace) api<AvisoPublico>(`/api/publico/avisos/${idEnlace}`).then(setEnlazado).catch(() => setEnlazado(null))
  }, [idEnlace])

  const vigentes = avisos.filter(a => a.vigente)
  const cancelados = avisos.filter(a => a.tipo === 'Cancel')
  const maximo = vigentes[0]?.nivel

  return (
    <div className="publico">
      <header className="publico-header">
        <div className="marca"><span className="marca-simbolo"><Waves size={24} aria-hidden="true" /></span>
          <div><strong>SAT Río Piraí</strong><span className="marca-descripcion">ALERTAS TEMPRANAS · SANTA CRUZ</span></div></div>
        <button className="primario" onClick={alIngresar}>Ingresar al sistema<ArrowRight size={17} aria-hidden="true" /></button>
      </header>

      <main className="publico-main">
        <section className="publico-hero">
          <span className="eyebrow">CUENCA DEL RÍO PIRAÍ</span>
          <h1>{cargando ? 'Consultando avisos…' : vigentes.length ? `${vigentes.length} aviso${vigentes.length > 1 ? 's' : ''} vigente${vigentes.length > 1 ? 's' : ''}` : 'Sin avisos vigentes'}</h1>
          <p>Información oficial aprobada por el sistema de alertas tempranas. Siga siempre las instrucciones de las autoridades.</p>
          {maximo && <p className="publico-maximo">Nivel más alto ahora: <NivelAlerta nivel={maximo} grande /></p>}
          {error && <p className="error">{error} <button onClick={cargar}><RefreshCw size={14} aria-hidden="true" />Reintentar</button></p>}
        </section>

        {enlazado && <TarjetaAviso a={enlazado} destacado />}

        <section className="publico-mapa" aria-label="Mapa de zonas con aviso">
          <Mapa alto={380} capas={vigentes.filter(a => a.area).map(a => ({ clave: a.id, geometria: a.area!, color: a.nivel.color, etiqueta: `${a.nivel.texto}: ${a.titular ?? a.evento}` }))} />
          <ul className="leyenda" aria-label="Niveles de alerta">{NIVELES.map(n => <li key={n.codigo}><NivelAlerta nivel={n} /></li>)}</ul>
        </section>

        <section className="publico-lista" aria-busy={cargando}>
          {vigentes.filter(a => a.id !== enlazado?.id).map(a => <TarjetaAviso key={a.id} a={a} />)}
          {!cargando && !error && vigentes.length === 0 && <div className="tarjeta publico-vacio"><ShieldCheck size={28} aria-hidden="true" /><strong>No hay avisos vigentes en este momento.</strong><span>Esta página se actualiza cuando un aviso es aprobado.</span></div>}
        </section>

        {cancelados.length > 0 && <section className="publico-lista"><h2>Cancelados en las últimas 24 horas</h2>{cancelados.map(a => <TarjetaAviso key={a.id} a={a} />)}</section>}

        <section className="publico-niveles"><h2>¿Qué significa cada nivel?</h2>
          <div className="niveles-grid">{NIVELES.map(n => <div key={n.codigo} className="tarjeta"><NivelAlerta nivel={n} grande /><p>{n.que}</p></div>)}</div>
        </section>
      </main>
      <footer className="app-footer publico-footer"><span>SAT Río Piraí · Gestión del riesgo</span><span>Santa Cruz · Bolivia</span></footer>
    </div>
  )
}

function TarjetaAviso({ a, destacado = false }: { a: AvisoPublico; destacado?: boolean }) {
  const cancelado = a.tipo === 'Cancel'
  return (
    <article className={`tarjeta aviso-publico ${destacado ? 'destacado' : ''}`} style={{ '--nivel': cancelado ? '#687783' : a.nivel.color } as React.CSSProperties}>
      <div className="aviso-publico-cabecera">
        {cancelado ? <span className="nivel-alerta grande cancelado">Aviso cancelado</span> : <NivelAlerta nivel={a.nivel} grande />}
        {a.tipo === 'Update' && <span className="estado-version estado-EnRevision">{NOMBRE_TIPO.Update}</span>}
        {!a.vigente && !cancelado && <span className="estado-version estado-Rechazada">Ya no está vigente</span>}
      </div>
      <h3><BellRing size={18} aria-hidden="true" /> {a.titular ?? a.evento}</h3>
      {a.descripcion && <p>{a.descripcion}</p>}
      {a.instruccion && <div className="aviso-instruccion"><b>Qué hacer</b><p>{a.instruccion}</p></div>}
      <p className="tenue aviso-meta"><MapPin size={14} aria-hidden="true" />{a.descripcionArea ?? 'Zona del aviso'} · <Clock3 size={14} aria-hidden="true" />{cancelado ? `Cancelado ${fecha(a.aprobadoEn)}` : `Vigente hasta ${fecha(a.hasta)}`}</p>
      {destacado && a.area && <Mapa alto={240} capas={[{ clave: a.id, geometria: a.area, color: a.nivel.color, etiqueta: a.nivel.texto }]} />}
    </article>
  )
}
