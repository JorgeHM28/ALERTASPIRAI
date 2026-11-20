import { useEffect, useRef, type ReactNode } from 'react'

/**
 * Diálogo de confirmación sobre el elemento nativo <dialog>: el navegador atrapa el foco, cierra con Esc
 * y deja el resto de la página inerte. El primer botón del pie es el que recibe el foco (la opción segura).
 */
export function Dialogo({ abierto, titulo, alCerrar, children, pie }: {
  abierto: boolean; titulo: string; alCerrar: () => void; children: ReactNode; pie: ReactNode
}) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    if (abierto && !el.open) el.showModal()
    if (!abierto && el.open) el.close()
  }, [abierto])

  return (
    <dialog ref={ref} className="dialogo" aria-labelledby="dialogo-titulo" onClose={alCerrar}
      onClick={(e) => { if (e.target === e.currentTarget) alCerrar() }}>
      {abierto && (
        <div className="dialogo-cuerpo">
          <h3 id="dialogo-titulo">{titulo}</h3>
          {children}
          <div className="dialogo-pie">{pie}</div>
        </div>
      )}
    </dialog>
  )
}
