/** Reserva el espacio de una sección mientras carga, para que la página no salte cuando llega. */
export function Esqueleto({ etiqueta = 'Cargando' }: { etiqueta?: string }) {
  return (
    <div className="esqueleto" role="status" aria-label={etiqueta}>
      <span className="esqueleto-barra" style={{ width: '38%', height: '2rem' }} />
      <span className="esqueleto-barra" style={{ width: '62%' }} />
      <div className="esqueleto-bloque">
        <span className="esqueleto-barra" style={{ width: '100%', height: '2.5rem' }} />
        <span className="esqueleto-barra" style={{ width: '100%', height: '4rem' }} />
        <span className="esqueleto-barra" style={{ width: '100%', height: '4rem' }} />
      </div>
    </div>
  )
}
