const TEXTO_OSCURO = '#14232c'
const TEXTO_CLARO = '#ffffff'

function luminancia(hex: string) {
  const n = parseInt(hex.replace('#', '').slice(0, 6), 16)
  const canal = (v: number) => { const c = v / 255; return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4 }
  return 0.2126 * canal((n >> 16) & 255) + 0.7152 * canal((n >> 8) & 255) + 0.0722 * canal(n & 255)
}

const contraste = (a: number, b: number) => (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)

const mezclar = (hex: string, t: number) => {
  const n = parseInt(hex.replace('#', '').slice(0, 6), 16)
  const c = [(n >> 16) & 255, (n >> 8) & 255, n & 255].map((v) => Math.round(v + (255 - v) * t))
  return '#' + c.map((v) => v.toString(16).padStart(2, '0')).join('')
}

/**
 * El color tal cual si se lee bien sobre el fondo oscuro; si no, lo aclara con blanco solo lo necesario. El mínimo es 3:1 porque
 * se usa en letra de tamaño muy grande (WCAG 1.4.3, texto grande); así el rojo sigue siendo rojo y no se vuelve rosado.
 */
export function aclararSobre(color: string, fondoOscuro: string, minimo = 3) {
  const lf = luminancia(fondoOscuro)
  for (let t = 0; t <= 1; t += 0.05) {
    const c = t === 0 ? color : mezclar(color, t)
    if (contraste(luminancia(c), lf) >= minimo) return c
  }
  return '#ffffff'
}

/** Texto claro u oscuro, el que más contraste dé sobre el fondo (WCAG 1.4.3, mínimo 4,5:1 en texto pequeño). */
export function textoSobre(fondo: string) {
  const l = luminancia(fondo)
  return contraste(l, luminancia(TEXTO_CLARO)) >= contraste(l, luminancia(TEXTO_OSCURO)) ? TEXTO_CLARO : TEXTO_OSCURO
}
