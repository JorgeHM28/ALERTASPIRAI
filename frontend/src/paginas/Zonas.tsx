import { useEffect, useState } from 'react'
import { api, puedeEditar, type NivelRiesgo, type Rol, type Zona } from '../api'
import Mapa, { COLOR_RIESGO } from './Mapa'

const NIVELES: NivelRiesgo[] = ['Bajo', 'Medio', 'Alto', 'MuyAlto']

/** RF-09: zonas de riesgo en GeoJSON. */
export default function Zonas({ rol }: { rol: Rol }) {
  const [zonas, setZonas] = useState<Zona[]>([])
  const [seleccion, setSeleccion] = useState<Zona | null>(null)
  const [mensaje, setMensaje] = useState<{ ok: boolean; texto: string } | null>(null)
  const editar = puedeEditar(rol)

  const cargar = () => api<Zona[]>('/api/zonas').then(setZonas).catch((e) => setMensaje({ ok: false, texto: e.message }))
  useEffect(() => { cargar() }, [])

  async function importar(archivo: File) {
    setMensaje(null)
    try {
      const r = await api<{ creadas: number; actualizadas: number }>('/api/zonas/importar', {
        method: 'POST', body: await archivo.text(), headers: { 'Content-Type': 'application/json' },
      })
      setMensaje({ ok: true, texto: `Importación lista: ${r.creadas} zona(s) nuevas y ${r.actualizadas} actualizada(s).` })
      cargar()
    } catch (e) { setMensaje({ ok: false, texto: (e as Error).message }) }
  }

  async function guardar(z: Zona) {
    try {
      await api(`/api/zonas/${z.id}`, { method: 'PUT', json: { nombre: z.nombre, descripcion: z.descripcion, nivelRiesgo: z.nivelRiesgo, geometria: z.geometria } })
      setMensaje({ ok: true, texto: 'Zona guardada.' }); cargar()
    } catch (e) { setMensaje({ ok: false, texto: (e as Error).message }) }
  }

  async function eliminar(z: Zona) {
    if (!confirm(`¿Eliminar la zona "${z.nombre}"?`)) return
    try { await api(`/api/zonas/${z.id}`, { method: 'DELETE' }); setSeleccion(null); cargar() }
    catch (e) { setMensaje({ ok: false, texto: (e as Error).message }) }
  }

  function descargar() {
    api<object>('/api/zonas/geojson').then((fc) => {
      const url = URL.createObjectURL(new Blob([JSON.stringify(fc, null, 2)], { type: 'application/geo+json' }))
      Object.assign(document.createElement('a'), { href: url, download: 'zonas.geojson' }).click()
      URL.revokeObjectURL(url)
    })
  }

  return (
    <section>
      <div className="barra">
        <h2>Zonas de riesgo</h2>
        <div className="acciones">
          <button onClick={descargar}>Descargar GeoJSON</button>
          {editar && (
            <label className="boton primario">Importar GeoJSON
              <input type="file" accept=".geojson,.json,application/geo+json" hidden
                onChange={(e) => { const f = e.target.files?.[0]; if (f) importar(f); e.target.value = '' }} />
            </label>
          )}
        </div>
      </div>
      {mensaje && <p className={mensaje.ok ? 'exito' : 'error'}>{mensaje.texto}</p>}
      <p className="tenue">El GeoJSON debe ser una FeatureCollection de polígonos en WGS84. Cada Feature lleva la propiedad <code>nombre</code> y, si quieres, <code>nivelRiesgo</code> (Bajo, Medio, Alto, MuyAlto) y <code>descripcion</code>.</p>

      <div className="dos-columnas">
        <Mapa capas={zonas.map((z) => ({
          clave: z.id, geometria: z.geometria, color: COLOR_RIESGO[z.nivelRiesgo],
          etiqueta: `${z.nombre} (${z.nivelRiesgo})`, alClic: () => setSeleccion(z),
        }))} alto={480} />
        <div>
          {seleccion ? (
            <EditorZona key={seleccion.id} zona={seleccion} editar={editar} alGuardar={guardar}
              alEliminar={eliminar} alCerrar={() => setSeleccion(null)} />
          ) : (
            <table>
              <thead><tr><th>Zona</th><th>Riesgo</th><th>km²</th><th>Comunidades</th></tr></thead>
              <tbody>
                {zonas.map((z) => (
                  <tr key={z.id} onClick={() => setSeleccion(z)} className="clicable">
                    <td>{z.nombre}</td>
                    <td><span className="etiqueta" style={{ background: COLOR_RIESGO[z.nivelRiesgo] }}>{z.nivelRiesgo}</span></td>
                    <td>{z.areaKm2}</td><td>{z.comunidades}</td>
                  </tr>
                ))}
                {zonas.length === 0 && <tr><td colSpan={4} className="tenue">Todavía no hay zonas. Importa un GeoJSON para empezar.</td></tr>}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </section>
  )
}

function EditorZona({ zona, editar, alGuardar, alEliminar, alCerrar }: {
  zona: Zona; editar: boolean; alGuardar: (z: Zona) => void; alEliminar: (z: Zona) => void; alCerrar: () => void
}) {
  const [z, setZ] = useState(zona)
  return (
    <div className="tarjeta">
      <h3>{zona.nombre}</h3>
      <label>Nombre<input value={z.nombre} disabled={!editar} onChange={(e) => setZ({ ...z, nombre: e.target.value })} /></label>
      <label>Nivel de riesgo
        <select value={z.nivelRiesgo} disabled={!editar} onChange={(e) => setZ({ ...z, nivelRiesgo: e.target.value as NivelRiesgo })}>
          {NIVELES.map((n) => <option key={n}>{n}</option>)}
        </select>
      </label>
      <label>Descripción<textarea value={z.descripcion ?? ''} disabled={!editar} onChange={(e) => setZ({ ...z, descripcion: e.target.value })} /></label>
      <p className="tenue">{zona.areaKm2} km² · {zona.comunidades} comunidad(es)</p>
      <div className="acciones">
        {editar && <button className="primario" onClick={() => alGuardar(z)}>Guardar</button>}
        {editar && <button className="peligro" onClick={() => alEliminar(zona)}>Eliminar</button>}
        <button onClick={alCerrar}>Volver</button>
      </div>
    </div>
  )
}
