import { NIVEL_POR_SEVERIDAD, NOMBRE_SEVERIDAD } from '../cap'

const SIGNIFICADO: Record<string, string> = {
  Extreme: 'Peligro inminente: evacuación inmediata.',
  Severe: 'Peligro probable: prepararse para salir.',
  Moderate: 'Situación a vigilar: informarse por canales oficiales.',
  Minor: 'Sin peligro previsto: conocer la ruta de evacuación.',
}

/** Ayuda breve para quien llega al panel sin conocer el estándar CAP ni el recorrido de un aviso. Cerrada por defecto. */
export function AyudaPanel() {
  return (
    <details className="ayuda">
      <summary>¿Qué es CAP y cómo se relacionan aviso, versión y mensaje?</summary>
      <div className="ayuda-cuerpo">
        <p><b>CAP</b> (Common Alerting Protocol) es el formato estándar con el que las instituciones, como el SENAMHI, se envían alertas. El sistema guarda cada aviso tal como llegó y no lo modifica nunca.</p>
        <ol>
          <li><b>Aviso:</b> el documento oficial que llega o se registra a mano.</li>
          <li><b>Versión:</b> el mensaje para la población que se prepara a partir del aviso. Pasa de borrador a revisión y, si un aprobador la aprueba, queda como oficial e inmutable. Una corrección es una versión nueva: una actualización o una cancelación.</li>
          <li><b>Difusión:</b> el envío de una versión aprobada a la web pública, por SMS y por tareas manuales (radio, altavoz y redes).</li>
        </ol>
        <p><b>Nivel de alerta:</b> sale de la severidad oficial del aviso.</p>
        <ul className="ayuda-niveles">
          {['Extreme', 'Severe', 'Moderate', 'Minor'].map(s => (
            <li key={s}><span className="ayuda-punto" style={{ background: NIVEL_POR_SEVERIDAD[s].color }} aria-hidden="true" /><b>{NIVEL_POR_SEVERIDAD[s].texto}</b> · severidad {NOMBRE_SEVERIDAD[s].toLowerCase()}. {SIGNIFICADO[s]}</li>
          ))}
        </ul>
      </div>
    </details>
  )
}
