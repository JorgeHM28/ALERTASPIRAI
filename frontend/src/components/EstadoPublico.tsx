import { useEffect, useState } from 'react'
import { ArrowRight } from 'lucide-react'
import { api, fecha, type AvisoPublico } from '../api'
import { aclararSobre } from '../color'

/** El titular oficial suele repetir el nivel ("Alerta naranja por…"): si ya lo dice, se usa el nombre del evento. */
const tituloDe = (a: AvisoPublico) => {
  const t = a.titular ?? a.evento
  return t.toLowerCase().startsWith(a.nivel.texto.toLowerCase()) ? a.evento : t
}

/**
 * Franja de la cabecera del panel: lo que ve hoy la población en la página pública. Quien gestiona avisos sabe de un vistazo
 * qué nivel está vigente y puede abrir esa página. Se vuelve a consultar al cambiar de sección, porque aprobar o difundir
 * cambia lo que se ve afuera.
 */
export function EstadoPublico({ seccion }: { seccion: string }) {
  const [avisos, setAvisos] = useState<AvisoPublico[] | null>(null)
  const [error, setError] = useState(false)

  useEffect(() => {
    let vigente = true
    api<AvisoPublico[]>('/api/publico/avisos')
      .then((a) => { if (vigente) { setAvisos(a); setError(false) } })
      .catch(() => { if (vigente) setError(true) })
    return () => { vigente = false }
  }, [seccion])

  const activos = (avisos ?? []).filter((a) => a.vigente && a.tipo !== 'Cancel').sort((a, b) => b.nivel.orden - a.nivel.orden)
  const principal = activos[0]
  const [primera, ...resto] = (principal ? principal.nivel.texto : error ? 'Sin datos' : avisos ? 'Sin avisos vigentes' : 'Consultando…').split(' ')
  const estilo = principal ? { '--nivel': principal.nivel.color, '--acento': aclararSobre(principal.nivel.color, '#0b1f2a') } as React.CSSProperties : undefined

  return (
    <section className="estado-panel" style={estilo} aria-label="Estado que ve la población">
      <svg className="ep-olas" viewBox="0 0 1200 200" preserveAspectRatio="xMidYMid slice" aria-hidden="true"><g fill="none" stroke="#8bd6dc" strokeOpacity=".14" strokeWidth="2">
        {[120, 150, 180].map((y) => <path key={y} d={`M-20 ${y}C160 ${y - 40} 260 ${y + 40} 440 ${y + 5}S700 ${y - 50} 860 ${y - 10}S1120 ${y + 30} 1300 ${y - 15}`} />)}
      </g></svg>
      <div className="ep-interior">
        <div>
          <p className="ep-etiqueta">Lo que ve hoy la población</p>
          <div className="ep-fila">
            <p className="ep-nivel"><span className="ep-acento">{primera}</span>{resto.length > 0 && ` ${resto.join(' ')}`}</p>
            <p className="ep-detalle">
              {principal
                ? <>{tituloDe(principal)} · {principal.descripcionArea ?? 'Zona del aviso'} · vigente hasta {fecha(principal.hasta)}{activos.length > 1 && ` · y ${activos.length - 1} aviso${activos.length > 2 ? 's' : ''} más`}</>
                : error ? 'No se pudo consultar la página pública.' : avisos ? 'No hay avisos oficiales activos para la cuenca del río Piraí.' : ''}
            </p>
          </div>
        </div>
        <a className="ep-enlace" href="/?publico=1" target="_blank" rel="noreferrer">Ver la página pública<ArrowRight size={16} aria-hidden="true" /><span className="solo-lector"> (se abre en otra pestaña)</span></a>
      </div>
    </section>
  )
}
