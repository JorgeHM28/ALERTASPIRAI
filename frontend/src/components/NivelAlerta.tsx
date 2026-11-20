import type { Nivel } from '../api'

/** RF-06: el nivel siempre se lee como texto; el color solo acompaña. */
export function NivelAlerta({ nivel, grande = false }: { nivel: Nivel; grande?: boolean }) {
  return <span className={`nivel-alerta ${grande ? 'grande' : ''}`} style={{ '--nivel': nivel.color } as React.CSSProperties}>
    <span className="nivel-marca" aria-hidden="true" />{nivel.texto}
  </span>
}
