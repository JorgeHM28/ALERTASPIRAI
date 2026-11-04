import { useEffect } from 'react'
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

function Encuadre({ capas, puntos }: { capas: CapaMapa[]; puntos: PuntoMapa[] }) {
  const mapa = useMap()
  const firma = capas.map((c) => c.clave).join() + puntos.map((p) => p.clave).join()
  useEffect(() => {
    const limites = L.latLngBounds([])
    capas.forEach((c) => limites.extend(L.geoJSON(c.geometria).getBounds()))
    puntos.forEach((p) => limites.extend([p.lat, p.lon]))
    if (limites.isValid()) mapa.fitBounds(limites, { padding: [20, 20], maxZoom: 13 })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [firma])
  return null
}

export default function Mapa({ capas, puntos = [], alto = 420 }: { capas: CapaMapa[]; puntos?: PuntoMapa[]; alto?: number }) {
  return (
    <MapContainer center={CENTRO_PIRAI} zoom={9} style={{ height: alto }} className="mapa">
      <TileLayer attribution="&copy; OpenStreetMap" url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
      {capas.map((c) => (
        <GeoJSON key={c.clave + JSON.stringify(c.geometria).length} data={c.geometria}
          style={{ color: c.color, weight: 2, fillOpacity: 0.3 }}
          eventHandlers={{ click: () => c.alClic?.() }}
          onEachFeature={(_, capa) => capa.bindTooltip(c.etiqueta)} />
      ))}
      {puntos.map((p) => (
        <CircleMarker key={p.clave} center={[p.lat, p.lon]} radius={6} pathOptions={{ color: '#0b3d61', fillOpacity: 0.9 }}>
          <Tooltip>{p.etiqueta}</Tooltip>
        </CircleMarker>
      ))}
      <Encuadre capas={capas} puntos={puntos} />
    </MapContainer>
  )
}
