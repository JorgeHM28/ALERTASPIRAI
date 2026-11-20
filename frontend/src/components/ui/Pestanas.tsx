import { useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'

/** `mantener`: el contenido sigue montado (oculto) al cambiar de pestaña, para no perder lo escrito o marcado en él. */
export interface Pestana { id: string; nombre: string; contenido: ReactNode; mantener?: boolean }

/**
 * Pestañas accesibles (patrón WAI-ARIA): flechas, Inicio y Fin cambian de pestaña y solo la activa entra en el orden de tabulación.
 * Solo se dibuja el panel activo, así que un mapa o una consulta pesada no se carga hasta que se abre su pestaña.
 */
export function Pestanas({ pestanas, etiqueta }: { pestanas: Pestana[]; etiqueta: string }) {
  const [activa, setActiva] = useState(pestanas[0].id)
  const base = useId()
  const refs = useRef<Record<string, HTMLButtonElement | null>>({})
  const actual = pestanas.find(p => p.id === activa) ?? pestanas[0]

  function mover(e: KeyboardEvent, i: number) {
    const n = pestanas.length
    const j = e.key === 'ArrowRight' ? (i + 1) % n : e.key === 'ArrowLeft' ? (i - 1 + n) % n : e.key === 'Home' ? 0 : e.key === 'End' ? n - 1 : -1
    if (j < 0) return
    e.preventDefault()
    setActiva(pestanas[j].id)
    refs.current[pestanas[j].id]?.focus()
  }

  return (
    <div className="pestanas">
      <div role="tablist" aria-label={etiqueta} className="pestanas-lista">
        {pestanas.map((p, i) => (
          <button key={p.id} ref={el => { refs.current[p.id] = el }} role="tab" id={`${base}-${p.id}`}
            aria-selected={p.id === actual.id} aria-controls={p.id === actual.id ? `${base}-panel-${p.id}` : undefined} tabIndex={p.id === actual.id ? 0 : -1}
            onClick={() => setActiva(p.id)} onKeyDown={e => mover(e, i)}>{p.nombre}</button>
        ))}
      </div>
      {pestanas.map(p => (p.id === actual.id || p.mantener) && (
        <div key={p.id} role="tabpanel" id={`${base}-panel-${p.id}`} aria-labelledby={`${base}-${p.id}`} className="pestanas-panel"
          hidden={p.id !== actual.id} tabIndex={p.id === actual.id ? 0 : undefined}>{p.contenido}</div>
      ))}
    </div>
  )
}
