import { useEffect, useRef } from 'react'
import { GeoJSON, MapContainer, TileLayer, useMap, CircleMarker, Tooltip } from 'react-leaflet'
import L from 'leaflet'
import type { NivelRiesgo } from '../api'

export const COLOR_RIESGO: Record<NivelRiesgo, string> = {
  Bajo: '#2e7d32', Medio: '#f9a825', Alto: '#ef6c00', MuyAlto: '#c62828',
}

const CENTRO_PIRAI: [number, number] = [-17.65, -63.25]

export interface CapaMapa {
  clave: string
  geometria: GeoJSON.Geometry
  color: string
  etiqueta: string
  alClic?: () => void
}

export interface PuntoMapa { clave: string; lat: number; lon: number; etiqueta: string }

function Encuadre({ capas, puntos, zoomMaximo }: { capas: CapaMapa[]; puntos: PuntoMapa[]; zoomMaximo: number }) {
  const mapa = useMap()
  const firma = capas.map((c) => c.clave).join() + puntos.map((p) => p.clave).join()
  const movido = useRef(false)
  const encuadrar = () => {
    const limites = L.latLngBounds([])
    capas.forEach((c) => limites.extend(L.geoJSON(c.geometria).getBounds()))
    puntos.forEach((p) => limites.extend([p.lat, p.lon]))
    if (limites.isValid()) mapa.fitBounds(limites, { padding: [20, 20], maxZoom: zoomMaximo, animate: false })
  }
  useEffect(() => { movido.current = false; encuadrar() },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [firma])
  // Si el contenedor aún no tenía tamaño cuando se calculó el encuadre (la página se dibuja con el panel oculto o se
  // redimensiona), el mapa quedaba en un zoom que mostraba todo el país. Se vuelve a encuadrar al cambiar de tamaño,
  // salvo que la persona ya haya movido el mapa.
  useEffect(() => {
    const contenedor = mapa.getContainer()
    const marcar = () => { movido.current = true }
    contenedor.addEventListener('pointerdown', marcar)
    contenedor.addEventListener('wheel', marcar, { passive: true })
    contenedor.addEventListener('keydown', marcar)
    const observador = new ResizeObserver(() => { mapa.invalidateSize(); if (!movido.current) encuadrar() })
    observador.observe(contenedor)
    return () => {
      observador.disconnect()
      contenedor.removeEventListener('pointerdown', marcar)
      contenedor.removeEventListener('wheel', marcar)
      contenedor.removeEventListener('keydown', marcar)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mapa, firma, zoomMaximo])
  return null
}

/** Sin `alto`, la altura la define el CSS de quien lo usa (por ejemplo, la landing la reduce en el celular). */
export default function Mapa({ capas, puntos = [], alto, zoomMaximo = 13 }: { capas: CapaMapa[]; puntos?: PuntoMapa[]; alto?: number; zoomMaximo?: number }) {
  return (
    <MapContainer center={CENTRO_PIRAI} zoom={9} style={alto ? { height: alto } : undefined} className="mapa">
      <TileLayer attribution="&copy; OpenStreetMap" url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
      {capas.map((c) => (
        <GeoJSON key={c.clave + JSON.stringify(c.geometria).length} data={c.geometria}
          interactive={!!c.alClic}
          style={{ color: c.color, weight: 2, fillOpacity: 0.3 }}
          eventHandlers={{ click: () => c.alClic?.() }}
          onEachFeature={(_, capa) => {
            const texto = document.createElement('span')
            texto.textContent = c.etiqueta
            capa.bindTooltip(texto)
          }} />
      ))}
      {puntos.map((p) => (
        <CircleMarker key={p.clave} center={[p.lat, p.lon]} radius={6} pathOptions={{ color: '#0b3d61', fillOpacity: 0.9 }}>
          <Tooltip>{p.etiqueta}</Tooltip>
        </CircleMarker>
      ))}
      <Encuadre capas={capas} puntos={puntos} zoomMaximo={zoomMaximo} />
    </MapContainer>
  )
}
