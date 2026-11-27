# Sprint 2: mensajes, aprobación, destinos y web pública

Entrega: 20/11/2026. Requisitos: RF-02, RF-05, RF-06, RF-07 y RF-11.

## Flujo

1. Llega o se registra un **aviso oficial** (RF-01). No se modifica nunca.
2. Desde el aviso se **prepara una versión** del mensaje (`POST /api/avisos/{id}/versiones`). El tipo lo decide el sistema:
   - **Alert** si es la primera versión de ese evento.
   - **Update** si ya hay una versión aprobada, o si el aviso es un Update oficial del SENAMHI que referencia a un aviso registrado.
   - **Cancel** si el aviso oficial es un Cancel (y hay algo aprobado que cancelar).
   Los hechos (evento, urgencia, severidad, certeza), el nivel y el ámbito (vigencia y área) se copian del aviso y no se editan. El operador redacta el titular, la descripción y las instrucciones.
3. El borrador se corrige (`PUT /api/versiones/{id}`; un campo en `null` se conserva y `""` lo borra).
   Un borrador que no se va a usar se elimina con `DELETE /api/versiones/{id}`. Solo se eliminan borradores (lo que ya se envió a revisión, se rechazó o se aprobó es historial de RF-11) y solo los elimina quien los preparó o un administrador (si no, 403). El borrado queda en la bitácora del aviso.
   **Instrucciones oficiales (RF-05):** se eligen de un protocolo del catálogo con `PUT /api/versiones/{id}/protocolo` y `{ "protocoloId": "…" }` (o `null` para quitarlo). La versión copia el texto, el título y la cita (institución, documento y fecha). Mientras esté vinculada, las instrucciones no se editan a mano. Una alerta o actualización sin protocolo no pasa a revisión. La cancelación no lo exige. La corrección de una rechazada, y un Update que conserva las instrucciones, heredan el protocolo.
4. **Enviar a revisión** (`POST /api/versiones/{id}/enviar-revision`) genera el XML CAP 1.2 con el Builder y lo valida contra el **XSD oficial de OASIS** (`src/SistemaAlertas.Application/Cap/CAP-v1.2.xsd`, sin cambios, incluido como recurso del ensamblado). El XSD controla el orden de los elementos, las enumeraciones y el formato de las fechas. Si la versión está incompleta, o si el XML no es válido, queda como borrador y la respuesta dice por qué.
5. El **aprobador** revisa la fuente, las instrucciones, el contenido CAP, el tipo, las referencias y el alcance, y decide:
   - `POST /api/versiones/{id}/aprobar`: se guardan su identidad y la fecha. La versión queda inmutable.
   - **Separación de funciones:** quien preparó la versión no puede aprobarla, aunque sea administrador (400). En desarrollo se crea el usuario `aprobador` / `Aprobador12345` (rol Aprobador, `Semilla:ContrasenaAprobador`) para mostrar el flujo completo.
   - `POST /api/versiones/{id}/rechazar` con `{ "motivo": "…" }` (obligatorio). La versión vuelve al operador.
6. Una versión rechazada se corrige con `POST /api/versiones/{id}/corregir`, que crea la versión siguiente en borrador con el motivo a la vista (Prototype).
7. Una versión aprobada se corrige con `POST /api/versiones/{id}/update` o se cancela con `POST /api/versiones/{id}/cancel`. Al aprobarse, el Update o el Cancel reemplaza a la versión que referencia.
8. **Destinos** (RF-07): `GET /api/versiones/{id}/destinos/previa` muestra el alcance sin guardarlo. `POST /api/versiones/{id}/destinos` guarda la lista de una versión aprobada.
9. **Difusión** (`POST /api/difusion/{versionId}`): toma la versión aprobada vigente y los SMS de su lista de destinos.
10. **Web pública** (`GET /api/publico/avisos`, sin sesión): muestra la última versión aprobada de cada evento con texto, nivel en palabras, instrucciones y área. No muestra contactos ni datos internos.

## Referencias

- Un Update o Cancel solo puede referirse a la versión vigente de su mismo evento, que debe estar aprobada, no reemplazada y no ser un Cancel. Cualquier otra referencia se rechaza como **incoherente**. Una versión que no existe se rechaza como **referencia inexistente**.
- Un Update o Cancel oficial importado cuyo `<references>` apunta a un aviso que no está registrado se guarda igual, porque es el documento oficial. No se puede preparar un mensaje desde él (referencia inexistente).
- Cada evento tiene a lo sumo una versión en curso (borrador o en revisión).

## Protocolos oficiales (RF-05)

- Catálogo en `GET /api/protocolos` (`?activos=true` para elegir). Cada protocolo tiene título, instrucciones, institución, documento de referencia y fecha del documento, todos obligatorios.
- Lo mantiene un administrador: `POST /api/protocolos`, `PUT /api/protocolos/{id}` y `POST /api/protocolos/{id}/desactivar` o `/activar`. No se borra, porque las versiones lo citan.
- Si se corrige un protocolo, las versiones ya preparadas conservan el texto y la cita que copiaron.
- En desarrollo (`Semilla:DatosDemo`) se cargan dos protocolos de ejemplo, marcados como «Datos de demostración».

## Destinos por comunidad (RF-07)

- **Alcance:** son las comunidades cuya zona toca el área de la versión. Si la comunidad tiene ubicación, esa ubicación debe caer dentro del área.
- **Contactos:** solo entran los habilitados (activos, autorizados y con canal), por los canales elegidos. Los canales deben estar entre los aprobados (`Destinos:CanalesAprobados`, por defecto Sms, WhatsApp, Llamada y Radio; en el Sprint 3 se quitó Llamada).
- **Sin duplicados:** la clave de la tabla `destinos_envio` es (lista, canal, dirección).
- **Pendientes:** una comunidad del alcance que no aporta ningún destino queda en `comunidades_pendientes` con su motivo.
- Los destinos se ven enmascarados para quien no tiene permiso de edición.

## Nivel (RF-06)

El nivel sale de la severidad oficial y siempre se escribe con palabras:

| Severidad | Nivel |
|---|---|
| Extreme | Alerta roja |
| Severe | Alerta naranja |
| Moderate | Alerta amarilla |
| Minor | Alerta verde |

- El SMS empieza con el nivel en mayúsculas (`ALERTA NARANJA: …`).
- La radio y el altavoz reciben el texto completo para leerlo en voz alta.
- En la web el color solo acompaña al texto.

## Pantallas

- **Página de inicio pública** (sin sesión): avisos vigentes con su texto y un mapa, cancelaciones de las últimas 24 h y qué significa cada nivel. El enlace `/?aviso={id}` abre un aviso concreto.
- **Avisos**: tiene el botón «Preparar mensaje» y explica por qué un aviso no se puede preparar.
- **Protocolos**: catálogo de protocolos oficiales. Todos lo consultan; el administrador lo mantiene.
- **Mensajes**: lista de versiones por estado. El borrador tiene un selector «Protocolo oficial» que llena las instrucciones y muestra su fuente. Para cada versión permite editar el borrador, eliminarlo (con confirmación), enviarlo a revisión, aprobar o rechazar con motivo, corregir, preparar un Update o un Cancel, revisar los destinos por comunidad y difundir.

## Base de datos

La migración `Sprint2VersionesMensaje` hace lo siguiente:
- Agrega `avisos.referencias` y `avisos.cadena_id`, y rellena los avisos existentes.
- Crea `versiones_mensaje`, `listas_destinos`, `destinos_envio` y `comunidades_pendientes`.

La migración `Sprint2ProtocolosOficiales` crea `protocolos_oficiales` y agrega a `versiones_mensaje` las columnas `protocolo_id`, `protocolo_titulo` y `protocolo_cita`.

## Pendiente para retomar

Nada del Sprint 2. El catálogo de protocolos (RF-05), la regla de que quien prepara no aprueba y la validación contra el XSD oficial quedaron hechos el 24/11/2026.
