import { useEffect, useState } from 'react'
import { api, ETIQUETA_NIVEL_RIESGO, puedeEditar, soloFecha, type NivelRiesgo, type Rol, type Zona } from '../api'
import Mapa, { COLOR_RIESGO } from './Mapa'
import { textoSobre } from '../color'
import { useConfirmar } from '../components/ui/useConfirmar'
import { useEnfocarAlAbrir } from '../components/ui/useEnfocarAlAbrir'

const NIVELES: NivelRiesgo[] = ['Bajo', 'Medio', 'Alto', 'MuyAlto']

/** RF-09: zonas de riesgo en GeoJSON. */
export default function Zonas({ roles }: { roles: Rol[] }) {
  const [zonas, setZonas] = useState<Zona[]>([])
  const [seleccion, setSeleccion] = useState<Zona | null>(null)
  const [mensaje, setMensaje] = useState<{ ok: boolean; texto: string } | null>(null)
  const editar = puedeEditar(roles)
  const { confirmar, dialogo } = useConfirmar()
  const [importando, setImportando] = useState(false)
  const [origen, setOrigen] = useState({ fuenteInstitucional: '', referenciaDocumento: '', fechaDocumento: '' })

  const cargar = () => api<Zona[]>('/api/zonas').then(setZonas).catch((e) => setMensaje({ ok: false, texto: e.message }))
  useEffect(() => { cargar() }, [])

  async function importar(archivo: File) {
    setMensaje(null)
    const texto = await archivo.text()
    // Antes de importar se avisa qué zonas existentes se actualizarían (una importación reemplaza su polígono).
    try {
      const fc = JSON.parse(texto) as { features?: { properties?: { nombre?: string; name?: string } }[] }
      const nombres = (fc.features ?? []).map(f => String(f.properties?.nombre ?? f.properties?.name ?? '').trim()).filter(Boolean)
      const existentes = new Map(zonas.map(z => [z.nombre.trim().toLowerCase(), z.nombre]))
      const actualizan = [...new Set(nombres.filter(n => existentes.has(n.toLowerCase())).map(n => existentes.get(n.toLowerCase())!))]
      if (actualizan.length > 0 && !await confirmar({
        titulo: `¿Actualizar ${actualizan.length} zona${actualizan.length > 1 ? 's' : ''} existente${actualizan.length > 1 ? 's' : ''}?`,
        texto: `El archivo trae ${nombres.length} zona${nombres.length > 1 ? 's' : ''}. Se reemplazará el polígono de: ${actualizan.join(', ')}. Las demás se crean como nuevas.`,
        etiqueta: 'Importar y actualizar',
      })) return
    } catch { /* si no es un JSON válido, el servidor devuelve el error */ }
    try {
      const q = Object.values(origen).some(Boolean) ? `?${new URLSearchParams(origen)}` : ''
      const r = await api<{ creadas: number; actualizadas: number }>(`/api/zonas/importar${q}`, {
        method: 'POST', body: texto, headers: { 'Content-Type': 'application/json' },
      })
      setMensaje({ ok: true, texto: `Importación lista: ${r.creadas} zona(s) nuevas y ${r.actualizadas} actualizada(s).` })
      setImportando(false)
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
    if (!await confirmar({ titulo: `¿Eliminar la zona «${z.nombre}»?`, texto: 'Se borra la zona y su polígono. No se puede deshacer.', etiqueta: 'Eliminar zona', peligro: true })) return
    try { await api(`/api/zonas/${z.id}`, { method: 'DELETE' }); setSeleccion(null); setMensaje({ ok: true, texto: `Zona «${z.nombre}» eliminada.` }); cargar() }
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
          {editar && <button className={importando ? '' : 'primario'} aria-expanded={importando} onClick={() => setImportando(!importando)}>Importar GeoJSON</button>}
        </div>
      </div>
      {mensaje && <p className={mensaje.ok ? 'exito' : 'error'} role="status">{mensaje.texto}</p>}
      {editar && importando && (
        <div className="tarjeta interior">
          <b>Importar zonas desde un archivo GeoJSON</b>
          <p className="tenue">El archivo debe ser una FeatureCollection de polígonos en WGS84. Cada Feature lleva la propiedad <code>nombre</code> y, si quieres, <code>nivelRiesgo</code> (Bajo, Medio, Alto, MuyAlto) y <code>descripcion</code>.</p>
          <b>Documento de origen</b>
          <p className="tenue">Obligatorio para las zonas nuevas. Un Feature puede traer los suyos en <code>fuenteInstitucional</code>, <code>referenciaDocumento</code> y <code>fechaDocumento</code>. Las zonas que ya existen conservan sus datos si dejas esto vacío.</p>
          <CamposOrigen valor={origen} alCambiar={setOrigen} />
          <div className="acciones">
            <label className="boton primario">Elegir archivo e importar
              <input type="file" accept=".geojson,.json,application/geo+json" hidden
                onChange={(e) => { const f = e.target.files?.[0]; if (f) importar(f); e.target.value = '' }} />
            </label>
            <button onClick={() => setImportando(false)}>Cancelar</button>
          </div>
        </div>
      )}

      <div className="dos-columnas con-tabla">
        <Mapa capas={zonas.map((z) => ({
          clave: z.id, geometria: z.geometria, color: COLOR_RIESGO[z.nivelRiesgo],
          etiqueta: `${z.nombre} (${ETIQUETA_NIVEL_RIESGO[z.nivelRiesgo]})`, alClic: () => setSeleccion(z),
        }))} alto={480} />
        <div>
          {seleccion ? (
            <EditorZona key={seleccion.id} zona={seleccion} editar={editar} alGuardar={guardar}
              alEliminar={eliminar} alCerrar={() => setSeleccion(null)} />
          ) : (
            <div className="table-scroll"><table>
              <thead><tr><th>Zona</th><th>Riesgo</th><th>Fuente</th><th>km²</th><th>Comunidades</th></tr></thead>
              <tbody>
                {zonas.map((z) => (
                  <tr key={z.id} onClick={() => setSeleccion(z)} className="clicable">
                    <td><button className="row-link" onClick={(e) => { e.stopPropagation(); setSeleccion(z) }}>{z.nombre}</button></td>
                    <td><span className="etiqueta" style={{ background: COLOR_RIESGO[z.nivelRiesgo], color: textoSobre(COLOR_RIESGO[z.nivelRiesgo]) }}>{ETIQUETA_NIVEL_RIESGO[z.nivelRiesgo]}</span></td>
                    <td>{z.fuenteInstitucional ?? <span className="etiqueta gris">sin fuente</span>}</td>
                    <td>{z.areaKm2}</td><td>{z.comunidades}</td>
                  </tr>
                ))}
                {zonas.length === 0 && <tr><td colSpan={5} className="tenue">Todavía no hay zonas. Importa un GeoJSON para empezar.</td></tr>}
              </tbody>
            </table></div>
          )}
        </div>
      </div>
      {dialogo}
    </section>
  )
}

function EditorZona({ zona, editar, alGuardar, alEliminar, alCerrar }: {
  zona: Zona; editar: boolean; alGuardar: (z: Zona) => void; alEliminar: (z: Zona) => void; alCerrar: () => void
}) {
  const [z, setZ] = useState(zona)
  const { panel, titulo } = useEnfocarAlAbrir<HTMLDivElement>(zona.id)
  return (
    <div className="tarjeta" ref={panel}>
      <h3 ref={titulo} tabIndex={-1}>{zona.nombre}</h3>
      <label>Nombre<input value={z.nombre} disabled={!editar} onChange={(e) => setZ({ ...z, nombre: e.target.value })} /></label>
      <label>Nivel de riesgo
        <select value={z.nivelRiesgo} disabled={!editar} onChange={(e) => setZ({ ...z, nivelRiesgo: e.target.value as NivelRiesgo })}>
          {NIVELES.map((n) => <option key={n} value={n}>{ETIQUETA_NIVEL_RIESGO[n]}</option>)}
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
    <div className={apilado ? 'apilado' : 'fila'}>
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
