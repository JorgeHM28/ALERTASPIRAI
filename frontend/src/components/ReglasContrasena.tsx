import { reglasContrasena } from '../contrasena'

/** Las reglas de la contraseña, visibles mientras se escribe; cada una dice si se cumple, también para lectores de pantalla. */
export function ReglasContrasena({ id, valor }: { id: string; valor: string }) {
  return (
    <ul id={id} className="reglas">
      {reglasContrasena(valor).map(r => (
        <li key={r.texto} className={r.ok ? 'cumple' : ''}>
          <span aria-hidden="true">{r.ok ? '✔' : '○'}</span> {r.texto}<span className="solo-lector">{r.ok ? ' (cumplido)' : ' (falta)'}</span>
        </li>
      ))}
    </ul>
  )
}
