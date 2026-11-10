import { useEffect, useState } from 'react'
import { api, fecha, puedeEditar, soloFecha, type Canal, type Comunidad, type Contacto, type HistorialContacto, type Rol, type TipoCanal, type Zona } from '../api'
import Mapa, { COLOR_RIESGO } from './Mapa'

const TIPOS: TipoCanal[] = ['Sms', 'WhatsApp', 'Llamada', 'Email', 'Radio']
const PROCEDENCIAS = ['Asamblea comunal', 'Padrón municipal', 'Dirigente de la OTB', 'Registro en campaña de prevención', 'Solicitud propia']
const ACCION_CONTACTO: Record<string, string> = { Alta: 'Alta', Modificacion: 'Modificación', Baja: 'Baja' }

interface Destinatarios {
  destinatarios: { contactoId: string; nombre: string; comunidad: string; canales: Canal[] }[]
  excluidos: { contactoId: string; nombre: string; comunidad: string; motivos: string[] }[]
  comunidadesSinCanal: { comunidadId: string; nombre: string; zona: string | null; motivo: string }[]
}

const vacia = { nombre: '', municipio: '', poblacion: '', latitud: '', longitud: '', zonaId: '' }

/** RF-12: comunidades, contactos y canales. */
export default function Comunidades({ roles }: { roles: Rol[] }) {
  const [zonas, setZonas] = useState<Zona[]>([])
  const [comunidades, setComunidades] = useState<Comunidad[]>([])
  const [filtroZona, setFiltroZona] = useState('')
  const [detalle, setDetalle] = useState<Comunidad | null>(null)
  const [form, setForm] = useState<typeof vacia | null>(null)
  const [error, setError] = useState('')
  const [destinatarios, setDestinatarios] = useState<Destinatarios | null>(null)
  const editar = puedeEditar(roles)

  const verDestinatarios = () => api<Destinatarios>(`/api/destinatarios${filtroZona ? `?zonaIds=${filtroZona}` : ''}`)
    .then(setDestinatarios).catch((e) => setError((e as Error).message))

  const cargar = () => api<Comunidad[]>(`/api/comunidades${filtroZona ? `?zonaId=${filtroZona}` : ''}`).then(setComunidades)
  const abrir = (id: string) => api<Comunidad>(`/api/comunidades/${id}`).then(setDetalle)
  useEffect(() => { api<Zona[]>('/api/zonas').then(setZonas) }, [])
  useEffect(() => { cargar(); setDestinatarios(null) }, [filtroZona])

  async function guardarComunidad(e: React.FormEvent) {
    e.preventDefault(); setError('')
    if (!form) return
    const cuerpo = {
      nombre: form.nombre, municipio: form.municipio || null, zonaId: form.zonaId,
      poblacion: form.poblacion ? Number(form.poblacion) : null,
      latitud: form.latitud ? Number(form.latitud) : null, longitud: form.longitud ? Number(form.longitud) : null,
    }
    try {
      const c = detalle
        ? await api<Comunidad>(`/api/comunidades/${detalle.id}`, { method: 'PUT', json: cuerpo })
        : await api<Comunidad>('/api/comunidades', { method: 'POST', json: cuerpo })
      setForm(null); setDetalle(c); cargar()
    } catch (err) { setError((err as Error).message) }
  }

  async function eliminarComunidad(c: Comunidad) {
    if (!confirm(`¿Eliminar "${c.nombre}" y sus contactos?`)) return
    await api(`/api/comunidades/${c.id}`, { method: 'DELETE' }); setDetalle(null); cargar()
  }

  const zonaSel = zonas.filter((z) => !filtroZona || z.id === filtroZona)

  return (
    <section>
      <div className="barra">
        <h2>Comunidades y contactos</h2>
        <div className="acciones">
          <select value={filtroZona} onChange={(e) => setFiltroZona(e.target.value)}>
            <option value="">Todas las zonas</option>
            {zonas.map((z) => <option key={z.id} value={z.id}>{z.nombre}</option>)}
          </select>
          <button onClick={verDestinatarios}>Destinatarios disponibles</button>
          {editar && <button className="primario" onClick={() => { setDetalle(null); setForm({ ...vacia, zonaId: filtroZona }) }}>Nueva comunidad</button>}
        </div>
      </div>
      {error && <p className="error">{error}</p>}

      {destinatarios && (
        <div className="tarjeta">
          <div className="barra"><h3>Destinatarios disponibles {filtroZona ? 'en la zona' : 'en todas las zonas'}</h3><button onClick={() => setDestinatarios(null)}>Cerrar</button></div>
          <p className="tenue">Solo reciben alertas los contactos activos, autorizados y con al menos un canal.</p>
          <p><b>{destinatarios.destinatarios.length}</b> contacto(s) habilitado(s) · <b>{destinatarios.excluidos.length}</b> excluido(s) · <b>{destinatarios.comunidadesSinCanal.length}</b> comunidad(es) sin canal</p>
          {destinatarios.comunidadesSinCanal.length > 0 && (
            <p className="error">Sin canal: {destinatarios.comunidadesSinCanal.map((c) => `${c.nombre} (${c.motivo})`).join(' · ')}</p>
          )}
          {destinatarios.excluidos.length > 0 && (
            <p className="tenue">Excluidos: {destinatarios.excluidos.map((x) => `${x.nombre}, ${x.comunidad}: ${x.motivos.join(', ')}`).join(' · ')}</p>
          )}
        </div>
      )}

      <div className="dos-columnas">
        <div>
          <Mapa alto={300}
            capas={zonaSel.map((z) => ({ clave: z.id, geometria: z.geometria, color: COLOR_RIESGO[z.nivelRiesgo], etiqueta: z.nombre }))}
            puntos={comunidades.filter((c) => c.latitud != null).map((c) => ({ clave: c.id, lat: c.latitud!, lon: c.longitud!, etiqueta: c.nombre }))} />
          <table>
            <thead><tr><th>Comunidad</th><th>Zona</th><th>Población</th><th>Contactos habilitados</th></tr></thead>
            <tbody>
              {comunidades.map((c) => (
                <tr key={c.id} className="clicable" onClick={() => { setForm(null); abrir(c.id) }}>
                  <td>{c.nombre}</td><td>{c.zona}</td><td>{c.poblacion ?? '—'}</td>
                  <td>{c.contactosHabilitados} de {c.totalContactos}{c.sinCanal && <span className="etiqueta" style={{ background: '#c62828', marginLeft: 6 }}>sin canal</span>}</td>
                </tr>
              ))}
              {comunidades.length === 0 && <tr><td colSpan={4} className="tenue">No hay comunidades.</td></tr>}
            </tbody>
          </table>
        </div>

        <div>
          {form && (
            <form className="tarjeta" onSubmit={guardarComunidad}>
              <h3>{detalle ? `Editar ${detalle.nombre}` : 'Nueva comunidad'}</h3>
              <label>Nombre<input required value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} /></label>
              <label>Zona
                <select required value={form.zonaId} onChange={(e) => setForm({ ...form, zonaId: e.target.value })}>
                  <option value="">Elige una zona…</option>
                  {zonas.map((z) => <option key={z.id} value={z.id}>{z.nombre}</option>)}
                </select>
              </label>
              <label>Municipio<input value={form.municipio} onChange={(e) => setForm({ ...form, municipio: e.target.value })} /></label>
              <label>Población<input type="number" min={0} value={form.poblacion} onChange={(e) => setForm({ ...form, poblacion: e.target.value })} /></label>
              <div className="fila">
                <label>Latitud<input type="number" step="any" value={form.latitud} onChange={(e) => setForm({ ...form, latitud: e.target.value })} /></label>
                <label>Longitud<input type="number" step="any" value={form.longitud} onChange={(e) => setForm({ ...form, longitud: e.target.value })} /></label>
              </div>
              <div className="acciones">
                <button className="primario">Guardar</button>
                <button type="button" onClick={() => setForm(null)}>Cancelar</button>
              </div>
            </form>
          )}
          {!form && detalle && (
            <div className="tarjeta">
              <h3>{detalle.nombre}</h3>
              <p className="tenue">{detalle.zona} · {detalle.municipio ?? 'sin municipio'} · {detalle.poblacion ?? '?'} habitantes</p>
              {editar && (
                <div className="acciones">
                  <button onClick={() => setForm({
                    nombre: detalle.nombre, municipio: detalle.municipio ?? '', poblacion: detalle.poblacion?.toString() ?? '',
                    latitud: detalle.latitud?.toString() ?? '', longitud: detalle.longitud?.toString() ?? '', zonaId: detalle.zonaId,
                  })}>Editar</button>
                  <button className="peligro" onClick={() => eliminarComunidad(detalle)}>Eliminar</button>
                </div>
              )}
              {detalle.sinCanal && <p className="error">Esta comunidad no tiene ningún contacto activo, autorizado y con canal: no recibirá alertas.</p>}
              <h4>Contactos</h4>
              {(detalle.contactos ?? []).map((ct) => (
                <EditorContacto key={ct.id} contacto={ct} editar={editar} alCambiar={() => abrir(detalle.id)} />
              ))}
              {editar && <EditorContacto comunidadId={detalle.id} editar alCambiar={() => { abrir(detalle.id); cargar() }} />}
            </div>
          )}
          {!form && !detalle && <p className="tenue">Elige una comunidad para ver sus contactos.</p>}
        </div>
      </div>
    </section>
  )
}

function EditorContacto({ contacto, comunidadId, editar, alCambiar }: {
  contacto?: Contacto; comunidadId?: string; editar: boolean; alCambiar: () => void
}) {
  const [abierto, setAbierto] = useState(false)
  const [nombre, setNombre] = useState(contacto?.nombre ?? '')
  const [cargo, setCargo] = useState(contacto?.cargo ?? '')
  const [activo, setActivo] = useState(contacto?.activo ?? true)
  const [procedencia, setProcedencia] = useState(contacto?.procedencia ?? '')
  const [autorizado, setAutorizado] = useState(contacto?.autorizado ?? false)
  const [referencia, setReferencia] = useState(contacto?.referenciaAutorizacion ?? '')
  const [fechaAut, setFechaAut] = useState(contacto?.fechaAutorizacion ?? '')
  const [historial, setHistorial] = useState<HistorialContacto[] | null>(null)
  const [canales, setCanales] = useState<Canal[]>(contacto?.canales ?? [{ tipo: 'Sms', destino: '+591', preferido: true }])
  const [error, setError] = useState('')

  async function guardar() {
    setError('')
    const cuerpo = {
      nombre, cargo: cargo || null, activo, canales, procedencia, autorizado,
      referenciaAutorizacion: referencia || null, fechaAutorizacion: fechaAut || null,
    }
    try {
      if (contacto) await api(`/api/contactos/${contacto.id}`, { method: 'PUT', json: cuerpo })
      else await api(`/api/comunidades/${comunidadId}/contactos`, { method: 'POST', json: cuerpo })
      setAbierto(false)
      if (!contacto) {
        setNombre(''); setCargo(''); setProcedencia(''); setAutorizado(false); setReferencia(''); setFechaAut('')
        setCanales([{ tipo: 'Sms', destino: '+591', preferido: true }])
      }
      alCambiar()
    } catch (e) { setError((e as Error).message) }
  }

  async function eliminar() {
    if (contacto && confirm(`¿Eliminar a ${contacto.nombre}?`)) { await api(`/api/contactos/${contacto.id}`, { method: 'DELETE' }); alCambiar() }
  }

  async function verHistorial() {
    if (!contacto) return
    if (historial) { setHistorial(null); return }
    try { setHistorial(await api<HistorialContacto[]>(`/api/contactos/${contacto.id}/historial`)) }
    catch (e) { setError((e as Error).message) }
  }

  if (!abierto) {
    return contacto ? (
      <div className="contacto">
        <div>
          <b>{contacto.nombre}</b>{contacto.cargo && <span className="tenue"> · {contacto.cargo}</span>}
          {contacto.habilitado
            ? <span className="etiqueta" style={{ background: '#2e7d32' }}>recibe alertas</span>
            : <span className="etiqueta gris" title="No está entre los destinatarios disponibles">excluido: {contacto.motivosNoHabilitado.join(', ')}</span>}
          <div className="tenue">{contacto.canales.map((c) => `${c.preferido ? '★ ' : ''}${c.tipo}: ${c.destino}`).join(' · ')}</div>
          <div className="tenue">Procedencia: {contacto.procedencia} · {contacto.autorizado
            ? `Autorizado (${contacto.referenciaAutorizacion}${contacto.fechaAutorizacion ? `, ${soloFecha(contacto.fechaAutorizacion)}` : ''})`
            : 'Sin autorización'}</div>
          {historial && (
            <ul className="tenue">
              {historial.length === 0 && <li>Sin cambios registrados desde que existe el historial.</li>}
              {historial.map((h) => <li key={h.id}>{fecha(h.fecha)} · {ACCION_CONTACTO[h.accion]} por {h.usuario ?? '?'}{h.cambios ? `: ${h.cambios}` : ''}</li>)}
            </ul>
          )}
          {error && <p className="error">{error}</p>}
        </div>
        <div className="acciones">
          <button onClick={verHistorial}>{historial ? 'Ocultar historial' : 'Historial'}</button>
          {editar && <><button onClick={() => setAbierto(true)}>Editar</button><button className="peligro" onClick={eliminar}>×</button></>}
        </div>
      </div>
    ) : <button onClick={() => setAbierto(true)}>+ Agregar contacto</button>
  }

  const cambiarCanal = (i: number, c: Partial<Canal>) =>
    setCanales(canales.map((x, j) => (j === i ? { ...x, ...c } : c.preferido ? { ...x, preferido: false } : x)))

  return (
    <div className="tarjeta interior">
      <div className="fila">
        <label>Nombre<input value={nombre} onChange={(e) => setNombre(e.target.value)} /></label>
        <label>Cargo<input value={cargo} onChange={(e) => setCargo(e.target.value)} placeholder="Presidente OTB" /></label>
      </div>
      <label>Procedencia del dato<input list="procedencias" required maxLength={150} value={procedencia}
        onChange={(e) => setProcedencia(e.target.value)} placeholder="De dónde se obtuvo el contacto" /></label>
      <datalist id="procedencias">{PROCEDENCIAS.map((p) => <option key={p} value={p} />)}</datalist>
      <div className="fila">
        <label className="check"><input type="checkbox" checked={activo} onChange={(e) => setActivo(e.target.checked)} /> Activo</label>
        <label className="check"><input type="checkbox" checked={autorizado} onChange={(e) => setAutorizado(e.target.checked)} /> Autorizó recibir alertas</label>
      </div>
      {autorizado && (
        <div className="fila">
          <label>Referencia de la autorización<input required maxLength={200} value={referencia} onChange={(e) => setReferencia(e.target.value)} placeholder="Acta de asamblea 14/2026" /></label>
          <label>Fecha de autorización<input type="date" max={new Date().toISOString().slice(0, 10)} value={fechaAut} onChange={(e) => setFechaAut(e.target.value)} /></label>
        </div>
      )}
      {(!activo || !autorizado) && <p className="tenue">Este contacto no estará entre los destinatarios hasta que esté activo y autorizado.</p>}
      <b>Canales</b>
      {canales.map((c, i) => (
        <div className="fila" key={i}>
          <select value={c.tipo} onChange={(e) => cambiarCanal(i, { tipo: e.target.value as TipoCanal })}>
            {TIPOS.map((t) => <option key={t}>{t}</option>)}
          </select>
          <input value={c.destino} onChange={(e) => cambiarCanal(i, { destino: e.target.value })}
            placeholder={c.tipo === 'Email' ? 'correo@ejemplo.com' : c.tipo === 'Radio' ? 'Emisora / frecuencia' : '+59171234567'} />
          <label className="check"><input type="radio" checked={c.preferido} onChange={() => cambiarCanal(i, { preferido: true })} /> preferido</label>
          <button onClick={() => setCanales(canales.filter((_, j) => j !== i))}>×</button>
        </div>
      ))}
      <button onClick={() => setCanales([...canales, { tipo: 'Email', destino: '', preferido: false }])}>+ Canal</button>
      {error && <p className="error">{error}</p>}
      <div className="acciones">
        <button className="primario" onClick={guardar}>Guardar contacto</button>
        <button onClick={() => setAbierto(false)}>Cancelar</button>
      </div>
    </div>
  )
}
