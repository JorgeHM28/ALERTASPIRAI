/** Valores del estándar CAP 1.2 tal como los guarda el sistema, y su nombre en español para mostrarlos. */
export const URGENCIAS = ['Immediate', 'Expected', 'Future', 'Past', 'Unknown']
export const SEVERIDADES = ['Extreme', 'Severe', 'Moderate', 'Minor', 'Unknown']
export const CERTEZAS = ['Observed', 'Likely', 'Possible', 'Unlikely', 'Unknown']

export const NOMBRE_URGENCIA: Record<string, string> = { Immediate: 'Inmediata', Expected: 'Esperada', Future: 'Futura', Past: 'Pasada', Unknown: 'Desconocida' }
export const NOMBRE_SEVERIDAD: Record<string, string> = { Extreme: 'Extrema', Severe: 'Severa', Moderate: 'Moderada', Minor: 'Menor', Unknown: 'Desconocida' }
export const NOMBRE_CERTEZA: Record<string, string> = { Observed: 'Observada', Likely: 'Probable', Possible: 'Posible', Unlikely: 'Improbable', Unknown: 'Desconocida' }
export const NOMBRE_FORMATO: Record<string, string> = { Manual: 'Registro manual', CapXml: 'XML (CAP)', CapJson: 'JSON (CAP)' }

/** Nivel de alerta que corresponde a cada severidad oficial (RF-06), con su color. */
export const NIVEL_POR_SEVERIDAD: Record<string, { texto: string; color: string }> = {
  Extreme: { texto: 'Alerta roja', color: '#C62828' },
  Severe: { texto: 'Alerta naranja', color: '#EF6C00' },
  Moderate: { texto: 'Alerta amarilla', color: '#F9A825' },
  Minor: { texto: 'Alerta verde', color: '#2E7D32' },
  Unknown: { texto: 'Sin nivel', color: '#687783' },
}

/** Nombre en español de un valor CAP; si no se conoce, el valor tal cual. */
export const en = (nombres: Record<string, string>, valor: string) => nombres[valor] ?? valor
