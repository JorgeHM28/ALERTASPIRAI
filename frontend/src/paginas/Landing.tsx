import { useCallback, useState } from 'react'
import { ArrowRight, MapPin, RefreshCw } from 'lucide-react'
import { type AvisoPublico } from '../api'
import { GlassNav } from '../components/GlassNav'
import { UnfoldAccordion } from '../components/ui/UnfoldAccordion'
import { NivelAlerta } from '../components/NivelAlerta'
import { usePublicResource } from './usePublicResource'
import './landing.css'

const NIVELES = [
  {
    codigo: 'roja',
    texto: 'Alerta roja',
    color: '#C62828',
    orden: 4,
    que: 'Peligro inminente. Siga de inmediato las instrucciones de evacuación.',
  },
  {
    codigo: 'naranja',
    texto: 'Alerta naranja',
    color: '#EF6C00',
    orden: 3,
    que: 'Peligro probable. Prepare su mochila de emergencia y esté listo para salir.',
  },
  {
    codigo: 'amarilla',
    texto: 'Alerta amarilla',
    color: '#F9A825',
    orden: 2,
    que: 'Situación a vigilar. Manténgase informado por los canales oficiales.',
  },
  {
    codigo: 'verde',
    texto: 'Alerta verde',
    color: '#2E7D32',
    orden: 1,
    que: 'Sin peligro previsto. Conozca su ruta de evacuación.',
  },
]
const funciones = [
  [
    'Administrar usuarios y permisos',
    'El administrador gestiona las cuentas y los permisos del personal que opera el sistema.',
  ],
  [
    'Administrar configuración territorial y de contactos',
    'El personal autorizado mantiene las zonas de riesgo, comunidades y contactos de coordinación. La población puede consultar las zonas publicadas.',
  ],
  [
    'Gestionar ciclo de vida del mensaje CAP',
    'El operador prepara el mensaje de alerta en el formato común CAP. El aprobador revisa el aviso, sus actualizaciones y su cancelación.',
  ],
  [
    'Gestionar y registrar difusión de alertas',
    'El equipo autorizado coordina la difusión del aviso aprobado y registra las acciones e incidencias por canal.',
  ],
  [
    'Consultar y reportar',
    'La población consulta los avisos aprobados y las zonas afectadas. Coordinación utiliza la información del sistema para el seguimiento de alertas.',
  ],
]
const clave = 'pirai-ultima-consulta'
function ultimaConsulta() {
  try {
    return sessionStorage.getItem(clave)
  } catch {
    return null
  }
}
function fechaLocal(value: string | null) {
  if (!value || !Number.isFinite(Date.parse(value))) return ''
  return new Date(value).toLocaleString('es-BO', {
    timeZone: 'America/La_Paz',
    dateStyle: 'short',
    timeStyle: 'short',
  })
}
const enlace = (id: string) => '/?publico=1&aviso=' + encodeURIComponent(id)
const zona = (a: AvisoPublico) => a.descripcionArea?.trim() || 'Zona no indicada en el aviso'
function contenido(a: AvisoPublico) {
  if (a.tipo === 'Cancel') return 'Este aviso fue cancelado'
  if (a.reemplazadoPor) return 'Este aviso fue reemplazado'
  if (!a.vigente) return 'Este aviso venció'
  return a.nivel.texto
}
function Detalle({ aviso: a }: { aviso: AvisoPublico }) {
  return (
    <>
      <h2>{a.titular || a.evento}</h2>
      <p className="pirai-zone">
        <MapPin size={18} aria-hidden="true" />
        {zona(a)}
      </p>
      {a.descripcion && <p>{a.descripcion}</p>}
      <strong>{a.vigente ? 'Qué hacer' : 'Instrucción del aviso original'}</strong>
      <p>{a.instruccion?.trim() || 'La fuente no indicó una instrucción.'}</p>
      <small>
        {a.tipo === 'Cancel'
          ? 'Cancelación'
          : a.reemplazadoPor
            ? 'Aprobación de esta versión'
            : 'Fin de vigencia'}{' '}
        ·{' '}
        {fechaLocal(a.tipo === 'Cancel' || a.reemplazadoPor ? a.aprobadoEn : a.hasta) || 'Fecha no indicada'}
      </small>
      {a.reemplazadoPor && (
        <p>
          <a href={enlace(a.reemplazadoPor)}>Consultar la versión posterior</a>
        </p>
      )}
    </>
  )
}
export default function Landing({ alIngresar }: { alIngresar: () => void }) {
  const id = new URLSearchParams(location.search).get('aviso')
  const [ultima, setUltima] = useState(ultimaConsulta)
  const guardarHora = useCallback(() => {
    const now = new Date().toISOString()
    setUltima(now)
    try {
      sessionStorage.setItem(clave, now)
    } catch {
      /* disponible en memoria */
    }
  }, [])
  const lista = usePublicResource<AvisoPublico[]>('/api/publico/avisos?cancelados=true', guardarHora)
  const detalle = usePublicResource<AvisoPublico>(id ? '/api/publico/avisos/' + encodeURIComponent(id) : null)
  const [Mapa, setMapa] = useState<typeof import('./Mapa').default>()
  const [mapState, setMapState] = useState<'idle' | 'loading' | 'error'>('idle')
  async function cargarMapa() {
    setMapState('loading')
    try {
      const modulo = await import('./Mapa')
      setMapa(() => modulo.default)
      setMapState('idle')
    } catch {
      setMapState('error')
    }
  }
  const activos = (lista.data ?? [])
    .filter((a) => a.vigente && a.tipo !== 'Cancel')
    .sort(
      (a, b) =>
        b.nivel.orden - a.nivel.orden ||
        (Date.parse(b.aprobadoEn ?? '') || 0) - (Date.parse(a.aprobadoEn ?? '') || 0) ||
        a.id.localeCompare(b.id),
    )
  const cancelados = lista.data?.filter((a) => a.tipo === 'Cancel') ?? []
  const principal = id ? detalle.data : activos[0]
  const estadoLista = lista.loading
    ? 'Consultando avisos…'
    : lista.error !== null
      ? 'No se pudo comprobar el estado actual'
      : activos.length
        ? contenido(activos[0])
        : 'Sin avisos vigentes'
  const titulo = !id
    ? estadoLista
    : detalle.loading
      ? 'Consultando el aviso…'
      : detalle.error === 404
        ? 'El aviso de este enlace no está disponible'
        : detalle.error !== null
          ? 'No se pudo consultar este aviso'
          : principal
            ? contenido(principal)
            : 'Consultando el aviso…'
  return (
    <div className="pirai">
      <a className="saltar" href="#avisos">
        Saltar a los avisos
      </a>
      <div className="pirai-curves" aria-hidden="true" />
      <GlassNav alIngresar={alIngresar} />
      <main>
        <section id="avisos" tabIndex={-1} className="pirai-hero">
          <div className="pirai-copy">
            <span className="pirai-location">
              <MapPin size={15} aria-hidden="true" /> Cuenca del río Piraí · Santa Cruz
            </span>
            <h1
              style={
                principal?.vigente && titulo === principal.nivel.texto
                  ? { borderInlineStart: '6px solid ' + principal.nivel.color, paddingInlineStart: '0.22em' }
                  : undefined
              }
            >
              {titulo}
            </h1>
            {principal && (!id || detalle.error === null) && (
              <>
                {!id && (lista.loading || lista.error !== null) && (
                  <p>Datos de la última consulta exitosa.</p>
                )}
                {titulo !== principal.nivel.texto && <NivelAlerta nivel={principal.nivel} />}
                <Detalle aviso={principal} />
              </>
            )}
            {!id && !principal && !lista.loading && lista.error === null && (
              <p>Consulte las zonas y conozca el significado de cada nivel de alerta.</p>
            )}
            {id && (
              <p>
                <a href="#mapa">Ver avisos vigentes</a>
              </p>
            )}
            {id && detalle.error !== null && detalle.error !== 404 && (
              <button onClick={detalle.retry}>Reintentar consulta del aviso</button>
            )}
            <p className="pirai-time">
              {ultima && fechaLocal(ultima) && (
                <>Última consulta exitosa del listado · {fechaLocal(ultima)} (Bolivia)</>
              )}
            </p>
            <button
              onClick={() => {
                if (!lista.loading) lista.retry()
              }}
              aria-disabled={lista.loading}
            >
              <RefreshCw size={16} aria-hidden="true" />{' '}
              {lista.error !== null ? 'Reintentar' : 'Actualizar avisos'}
            </button>
          </div>
          <div className="pirai-river" aria-hidden="true">
            <div className="pirai-river-lines" />
            <span>Río Piraí</span>
          </div>
        </section>
        <p className="solo-lector" role="status" aria-live="polite">
          {titulo}. {id && estadoLista} {ultima && fechaLocal(ultima)}
        </p>
        <section id="mapa" className="pirai-section">
          <h2>
            {lista.error !== null && lista.data ? 'Zonas en la última consulta exitosa' : 'Avisos y zonas'}
          </h2>
          {(lista.loading || lista.error !== null || !activos.length) && <p>{estadoLista}</p>}
          {activos.length > 0 && (
            <ul>
              {activos.map((a) => (
                <li key={a.id}>
                  <NivelAlerta nivel={a.nivel} />{' '}
                  <a href={enlace(a.id)}>
                    {zona(a)} · {a.titular || a.evento}
                  </a>
                </li>
              ))}
            </ul>
          )}
          {cancelados.length > 0 && (
            <>
              <h3>Cancelaciones de las últimas 24 horas consultadas</h3>
              <ul>
                {cancelados.map((a) => (
                  <li key={a.id}>
                    <a href={enlace(a.id)}>
                      {zona(a)} · {fechaLocal(a.aprobadoEn)}
                    </a>
                  </li>
                ))}
              </ul>
            </>
          )}
          {Mapa ? (
            <Mapa
              alto={340}
              zoomMaximo={11}
              capas={activos.flatMap((a) =>
                a.area ? [{ clave: a.id, geometria: a.area, color: a.nivel.color, etiqueta: zona(a) }] : [],
              )}
            />
          ) : (
            <>
              <button disabled={mapState === 'loading'} onClick={cargarMapa}>
                Ver mapa <ArrowRight size={16} aria-hidden="true" />
              </button>
              <span role="status">
                {mapState === 'loading'
                  ? 'Cargando mapa…'
                  : mapState === 'error'
                    ? 'No se pudo cargar el mapa. Puede volver a intentarlo.'
                    : ''}
              </span>
            </>
          )}
        </section>
        <section className="pirai-section">
          <h2>Guía de niveles de alerta</h2>
          <div className="pirai-levels">
            {NIVELES.map((n) => (
              <article key={n.codigo}>
                <i style={{ background: n.color }} />
                <h3>{n.texto}</h3>
                <p>{n.que}</p>
              </article>
            ))}
          </div>
        </section>
        <section className="pirai-section" id="funciones">
          <h2>Funciones del sistema</h2>
          {funciones.map(([title, body], i) => (
            <UnfoldAccordion key={title} title={title}>
              <p>{body}</p>
              {i === 4 && (
                <p>
                  <a href="#avisos">Consultar avisos</a> · <a href="#mapa">Ver zonas y mapa</a>
                </p>
              )}
            </UnfoldAccordion>
          ))}
        </section>
      </main>
      <footer>
        SAT Río Piraí <span>Santa Cruz · Bolivia</span>
      </footer>
    </div>
  )
}
