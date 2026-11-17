import type { ReactNode } from 'react'
import { textoSobre } from '../color'

/**
 * La banda del nivel en el detalle de un aviso o de una versión: el mismo signo que la landing pública, en pequeño.
 * El nivel va en texto grande y el color enmarca; el texto se elige por contraste.
 */
export function BandaNivel({ texto, color, children }: { texto: string; color: string; children?: ReactNode }) {
  return (
    <div className="nivel-banda" style={{ '--nivel': color, '--sobre-nivel': textoSobre(color) } as React.CSSProperties}>
      <span className="nivel-banda-texto">{texto}</span>
      {children && <small>{children}</small>}
    </div>
  )
}
