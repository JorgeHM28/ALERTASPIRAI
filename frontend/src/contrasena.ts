/** Reglas de una contraseña nueva: mínimo 8 caracteres, con letras y con números. */
export function reglasContrasena(c: string) {
  return [
    { texto: 'Al menos 8 caracteres', ok: c.length >= 8 },
    { texto: 'Con letras', ok: /[A-Za-zÁÉÍÓÚáéíóúÑñ]/.test(c) },
    { texto: 'Con números', ok: /\d/.test(c) },
  ]
}
