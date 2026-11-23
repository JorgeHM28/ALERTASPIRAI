import { useEffect } from 'react'
import { ArrowUpRight, Waves } from 'lucide-react'

export function GlassNav({ alIngresar }: { alIngresar: () => void }) {
  useEffect(() => {
    const desktop = matchMedia('(min-width: 1024px) and (pointer: fine)')
    const reduced = matchMedia(
      '(prefers-reduced-motion: reduce), (prefers-reduced-transparency: reduce), (prefers-contrast: more), (forced-colors: active)',
    )
    const dark = matchMedia('(prefers-color-scheme: dark)')
    let generation = 0
    let lens: { destroy(): void } | undefined
    function clear() {
      generation++
      lens?.destroy()
      lens = undefined
    }
    async function sync() {
      clear()
      const attempt = generation
      if (document.hidden || !desktop.matches || reduced.matches) return
      try {
        const { default: liquidGL } = await import('liquid-gl')
        if (attempt !== generation) return
        lens = liquidGL({
          target: '#pirai-lens',
          snapshot: '.pirai-curves',
          engine: 'webgl',
          tint: dark.matches ? 'rgba(16,36,49,.82)' : 'rgba(244,249,246,.76)',
          resolution: 0.7,
          refraction: 0.008,
          bevelDepth: 0.035,
          bevelWidth: 0.12,
          frost: 0.25,
          aberration: 0,
          shadow: false,
          specular: false,
          tilt: false,
          reveal: 'none',
          content: false,
        })
      } catch {
        /* La superficie CSS sigue disponible. */
      }
    }
    const queries = [desktop, reduced, dark]
    queries.forEach((query) => query.addEventListener('change', sync))
    document.addEventListener('visibilitychange', sync)
    void sync()
    return () => {
      clear()
      queries.forEach((query) => query.removeEventListener('change', sync))
      document.removeEventListener('visibilitychange', sync)
    }
  }, [])
  return (
    <header className="pirai-nav">
      <div id="pirai-lens" aria-hidden="true" />
      <a className="pirai-brand" href="#avisos">
        <Waves aria-hidden="true" /> SAT Río Piraí
      </a>
      <nav aria-label="Navegación pública">
        <a href="#avisos">Avisos</a>
        <a href="#mapa">Mapa</a>
        <a href="#funciones">Funciones del sistema</a>
      </nav>
      <button onClick={alIngresar}>
        Ingresar al sistema <ArrowUpRight size={16} aria-hidden="true" />
      </button>
    </header>
  )
}
