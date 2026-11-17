import { useState, type ReactNode } from 'react'
import { Dialogo } from './Dialogo'

interface Opciones { titulo: string; texto: ReactNode; etiqueta: string; peligro?: boolean }

/**
 * Reemplazo del confirm() del navegador: `confirmar({...})` devuelve una promesa con la decisión y `dialogo`
 * es el elemento que hay que dibujar una vez en la página. El foco inicial queda en «Cancelar».
 */
export function useConfirmar() {
  const [pedido, setPedido] = useState<(Opciones & { resolver: (acepta: boolean) => void }) | null>(null)

  const confirmar = (o: Opciones) => new Promise<boolean>(resolver => setPedido({ ...o, resolver }))
  const cerrar = (acepta: boolean) => { pedido?.resolver(acepta); setPedido(null) }

  const dialogo = (
    <Dialogo abierto={!!pedido} titulo={pedido?.titulo ?? ''} alCerrar={() => cerrar(false)}
      pie={<>
        <button autoFocus onClick={() => cerrar(false)}>Cancelar</button>
        <button className={pedido?.peligro ? 'peligro' : 'primario'} onClick={() => cerrar(true)}>{pedido?.etiqueta}</button>
      </>}>
      <p>{pedido?.texto}</p>
    </Dialogo>
  )
  return { confirmar, dialogo }
}
