import { useEffect, useRef } from 'react'

/**
 * Al abrir un detalle (cuando cambia `clave`), lo lleva a la vista y mueve el foco a su título. En pantallas medianas el
 * detalle queda debajo de la lista y, sin esto, abrirlo no cambia nada de lo que se ve. En pantallas anchas, donde ya está
 * al lado, solo se mueve el foco.
 */
export function useEnfocarAlAbrir<T extends HTMLElement>(clave: unknown) {
  const panel = useRef<T>(null)
  const titulo = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    if (!clave) return
    const apilado = window.matchMedia('(max-width: 1250px)').matches
    // Salto directo, no animado: un desplazamiento suave puede quedarse a medias si el navegador no dibuja cuadros.
    if (apilado) panel.current?.scrollIntoView({ block: 'start', behavior: 'auto' })
    titulo.current?.focus({ preventScroll: true })
  }, [clave])

  return { panel, titulo }
}
