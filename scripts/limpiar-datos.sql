-- Deja la base sin datos de prueba: borra avisos, mensajes, zonas, comunidades, contactos y protocolos,
-- y los usuarios de Consulta (esas cuentas las crea cada persona con el registro).
-- Conserva los usuarios de los casos de uso (Administrador, Operador y Aprobador).
-- Antes de correrlo, sacar un respaldo:
--   docker exec sistema_alertas_db pg_dump -U alertas -d sistema_alertas -Fc > respaldos/antes.dump
-- Uso:
--   docker exec -i sistema_alertas_db psql -U alertas -d sistema_alertas -v ON_ERROR_STOP=1 < scripts/limpiar-datos.sql
BEGIN;

DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY[
    'destinos_envio', 'comunidades_pendientes', 'listas_destinos', 'versiones_mensaje', 'avisos',
    'contacto_canales', 'contacto_historial', 'contactos', 'comunidades', 'zonas', 'protocolos_oficiales',
    'envios_sms', 'tareas_manuales', 'trabajos_difusion', 'caps_emitidos', 'eventos_bitacora']
  LOOP
    IF to_regclass(t) IS NOT NULL THEN
      EXECUTE format('TRUNCATE TABLE %I CASCADE', t);
    END IF;
  END LOOP;
END $$;

-- Usuarios de Consulta: se borran con sus roles y su bitácora.
CREATE TEMP TABLE usuarios_a_borrar AS
  SELECT u.id FROM usuarios u
  WHERE NOT EXISTS (SELECT 1 FROM usuario_roles r WHERE r.usuario_id = u.id AND r.rol <> 'Consulta');
DELETE FROM usuario_bitacora WHERE usuario_id IN (SELECT id FROM usuarios_a_borrar);
DELETE FROM usuario_roles WHERE usuario_id IN (SELECT id FROM usuarios_a_borrar);
DELETE FROM usuarios WHERE id IN (SELECT id FROM usuarios_a_borrar);

-- La bitácora de usuarios conserva solo lo que toca a las cuentas que quedan.
DELETE FROM usuario_bitacora b WHERE NOT EXISTS (SELECT 1 FROM usuarios u WHERE u.id = b.usuario_id);

COMMIT;

SELECT u.nombre_usuario, string_agg(r.rol, ', ') AS roles
FROM usuarios u LEFT JOIN usuario_roles r ON r.usuario_id = u.id
GROUP BY u.nombre_usuario ORDER BY 1;
