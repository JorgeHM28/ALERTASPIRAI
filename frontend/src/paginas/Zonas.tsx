import { useEffect, useState } from 'react'
import { api, puedeEditar, soloFecha, type NivelRiesgo, type Rol, type Zona } from '../api'
import Mapa, { COLOR_RIESGO } from './Mapa'

const NIVELES: NivelRiesgo[] = ['Bajo', 'Medio', 'Alto', 'MuyAlto']

/** RF-09: zonas de riesgo en GeoJSON. */
export default function Zonas({ roles }: { roles: Rol[] }) {
  const [zonas, setZonas] = useState<Zona[]>([])
  const [seleccion, setSeleccion] = useState<Zona | null>(null)
  const [mensaje, setMensaje] = useState<{ ok: boolean; texto: string } | null>(null)
  const editar = puedeEditar(roles)
  const [origen, setOrigen] = useState({ fuenteInstitucional: '', referenciaDocumento: '', fechaDocumento: '' })

  const cargar = () => api<Zona[]>('/api/zonas').then(setZonas).catch((e) => setMensaje({ ok: false, texto: e.message }))
  useEffect(() => { cargar() }, [])

  async function importar(archivo: File) {
    setMensaje(null)
    try {
      const q = Object.values(origen).some(Boolean) ? `?${new URLSearchParams(origen)}` : ''
      const r = await api<{ creadas: number; actualizadas: number }>(`/api/zonas/importar${q}`, {
        method: 'POST', body: await archivo.text(), headers: { 'Content-Type': 'application/json' },
      })
      setMensaje({ ok: true, texto: `Importación lista: ${r.creadas} zona(s) nuevas y ${r.actualizadas} actualizada(s).` })
      cargar()
    } catch (e) { setMensaje({ ok: false, texto: (e as Error).message }) }
  }

  async function guardar(z: Zona) {
    try {
      await api(`/api/zonas/${z.id}`, {
        method: 'PUT', json: {
          nombre: z.nombre, descripcion: z.descripcion, nivelRiesgo: z.nivelRiesgo, geometria: z.geometria,
          fuenteInstitucional: z.fuenteInstitucional || null, referenciaDocumento: z.referenciaDocumento || null, fechaDocumento: z.fechaDocumento || null,
        },
      })
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
      {editar && (
        <div className="tarjeta interior">
          <b>Documento de origen del archivo a importar</b>
          <p className="tenue">Obligatorio para las zonas nuevas. Un Feature puede traer los suyos en <code>fuenteInstitucional</code>, <code>referenciaDocumento</code> y <code>fechaDocumento</code>. Las zonas que ya existen conservan sus datos si dejas esto vacío.</p>
          <CamposOrigen valor={origen} alCambiar={setOrigen} />
        </div>
      )}

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
              <thead><tr><th>Zona</th><th>Riesgo</th><th>Fuente</th><th>km²</th><th>Comunidades</th></tr></thead>
              <tbody>
                {zonas.map((z) => (
                  <tr key={z.id} onClick={() => setSeleccion(z)} className="clicable">
                    <td>{z.nombre}</td>
                    <td><span className="etiqueta" style={{ background: COLOR_RIESGO[z.nivelRiesgo] }}>{z.nivelRiesgo}</span></td>
                    <td>{z.fuenteInstitucional ?? <span className="etiqueta gris">sin fuente</span>}</td>
                    <td>{z.areaKm2}</td><td>{z.comunidades}</td>
                  </tr>
                ))}
                {zonas.length === 0 && <tr><td colSpan={5} className="tenue">Todavía no hay zonas. Importa un GeoJSON para empezar.</td></tr>}
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
      <b>Documento de origen</b>
      {editar ? (
        <CamposOrigen valor={{ fuenteInstitucional: z.fuenteInstitucional ?? '', referenciaDocumento: z.referenciaDocumento ?? '', fechaDocumento: z.fechaDocumento ?? '' }}
          alCambiar={(o) => setZ({ ...z, ...o })} apilado />
      ) : (
        <p>{zona.fuenteInstitucional ?? 'Sin fuente registrada'} · {zona.referenciaDocumento ?? '—'} · {soloFecha(zona.fechaDocumento)}</p>
      )}
      <p className="tenue">{zona.areaKm2} km² · {zona.comunidades} comunidad(es)</p>
      <div className="acciones">
        {editar && <button className="primario" onClick={() => alGuardar(z)}>Guardar</button>}
        {editar && <button className="peligro" onClick={() => alEliminar(zona)}>Eliminar</button>}
        <button onClick={alCerrar}>Volver</button>
      </div>
    </div>
  )
}

type Origen = { fuenteInstitucional: string; referenciaDocumento: string; fechaDocumento: string }

/** RF-09: fuente institucional, referencia y fecha del documento del que sale la zona. */
function CamposOrigen({ valor, alCambiar, apilado }: { valor: Origen; alCambiar: (o: Origen) => void; apilado?: boolean }) {
  return (
    <div className={apilado ? '' : 'fila'}>
      <label>Fuente institucional<input list="fuentes-zona" maxLength={150} placeholder="SENAMHI Santa Cruz" value={valor.fuenteInstitucional}
        onChange={(e) => alCambiar({ ...valor, fuenteInstitucional: e.target.value })} /></label>
      <datalist id="fuentes-zona">
        <option value="SENAMHI Santa Cruz" /><option value="Gobernación de Santa Cruz" /><option value="Viceministerio de Defensa Civil" /><option value="Gobierno Municipal" />
      </datalist>
      <label>Referencia del documento<input maxLength={200} placeholder="Mapa de riesgo MR-07/2026" value={valor.referenciaDocumento}
        onChange={(e) => alCambiar({ ...valor, referenciaDocumento: e.target.value })} /></label>
      <label>Fecha del documento<input type="date" max={new Date().toISOString().slice(0, 10)} value={valor.fechaDocumento}
        onChange={(e) => alCambiar({ ...valor, fechaDocumento: e.target.value })} /></label>
    </div>
  )
}
