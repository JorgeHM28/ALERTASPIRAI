import { useEffect, useState } from 'react'
import { MapContainer, TileLayer } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import './index.css'

const API = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

// Centro aproximado de la cuenca del río Piraí (Santa Cruz, Bolivia)
const CENTRO_PIRAI: [number, number] = [-17.65, -63.25]

export default function App() {
  const [estadoApi, setEstadoApi] = useState('comprobando...')

  useEffect(() => {
    fetch(`${API}/api/salud`)
      .then((r) => r.json())
      .then((d) => setEstadoApi(d.estado))
      .catch(() => setEstadoApi('sin conexión'))
  }, [])

  return (
    <div className="app">
      <header>
        <h1>Sistema de Alertas Tempranas · Río Piraí</h1>
        <span>API: {estadoApi}</span>
      </header>
      <MapContainer center={CENTRO_PIRAI} zoom={9} className="mapa">
        <TileLayer
          attribution='&copy; OpenStreetMap contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
      </MapContainer>
    </div>
  )
}
