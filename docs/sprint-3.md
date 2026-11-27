# Sprint 3: difusión, seguimiento, datos reales y SQA

Entrega: 4/12/2026. Requisitos: RF-04, RF-08, RNF-02, RNF-04 y RNF-08, más los datos reales de la cuenca, el registro propio de cuentas de consulta, la importación desde Excel y el aseguramiento de la calidad (ver `docs/sqa.md`).

## Datos

- **Reales:** los 22 municipios de la cuenca del Piraí (como zonas) y 48 localidades (como comunidades). Fuentes, criterio y cómo se regeneran: `datos/cuenca-pirai/README.md`. La API los carga sola al iniciar si la tabla de zonas está vacía (`Semilla:CuencaPirai`).
- **Simulados:** los avisos. Por indicación del docente no hay datos oficiales del SENAMHI ni del SEARPI. Los ejemplos de `docs/ejemplos/sprint-3` usan áreas reales del río, pero el remitente es `senamhi-simulado@sat-pirai.local` y la nota de cada aviso dice que es simulado.
- **Contactos:** ninguno. Cada integrante del equipo carga el suyo para recibir los SMS de la demo.
- **Usuarios:** los de los casos de uso (`admin`, `operador` y `aprobador`). Las cuentas de Consulta las crea cada persona con el registro propio.
- **Limpiar la base:** `scripts/limpiar-datos.sql` borra avisos, mensajes, zonas, comunidades y contactos, y conserva los tres usuarios. Antes de correrlo hay que sacar un respaldo con `pg_dump`.

## Flujo de la difusión (RF-04)

`POST /api/difusion/{versionId}` (rol Aprobador o Administrador), sobre una versión aprobada y vigente:

1. **Reglas** (Chain of Responsibility): el aviso no está descartado, la versión está aprobada, **no se difundió antes**, no fue reemplazada, está vigente y está completa.
2. **CAP emitido:** se toma el XML CAP 1.2 aprobado, se valida contra el XSD oficial, se calcula su SHA-256 y se archiva tal cual en `caps_emitidos`. Un Update o Cancel queda enlazado al CAP emitido de la versión que reemplaza. **Si el XML no es válido, no sale nada por ningún canal.**
3. **Web:** la versión queda marcada como publicada (`publicada_web_en`) y aparece en la página pública.
4. **SMS:** un envío por contacto habilitado de la lista de destinos (RF-07). Al difundir se vuelve a mirar cada contacto: si se desactivó o perdió la autorización después de aprobar, ya no recibe.
5. **Tareas manuales:** radio y redes sociales, una para toda el área; altavoz, una por comunidad del alcance (también las comunidades sin contactos). Cada tarea lleva el texto listo para leer o publicar.
6. **Bitácora:** el evento Difundido pasa la versión y el aviso a "Difundido" y queda guardado en `eventos_aviso`.

Cada canal es independiente: si uno falla, queda una incidencia en la bitácora y los demás siguen.

**Update y Cancel.** Al difundir un Update o un Cancel, antes que nada se detiene lo que seguía pendiente de las versiones anteriores del mismo evento: los SMS en cola y las tareas sin hacer quedan como **Cancelado**, con el motivo. Lo ya enviado o confirmado no se toca. Un Cancel nunca lleva las instrucciones de la alerta. Usa la instrucción del Cancel oficial o un texto de cierre ("El aviso anterior queda cancelado…"), y no se le vincula protocolo. Un aviso Cancel es apto para difundirse, porque la población debe saber que la alerta terminó.

## Cola SMS (RNF-02 y RNF-08)

- La cola es la tabla `trabajos_difusion`: sobrevive a un reinicio de la API.
- Un servicio en segundo plano revisa la cola cada 5 s (`Sms:IntervaloColaSegundos`). Toma lotes de 200 (`Sms:TamanoLote`) y manda 10 SMS a la vez (`Sms:EnviosSimultaneos`). En las pruebas, 1000 SMS con 20 ms de red cada uno terminan en unos 2 s.
- **Reintentos:** solo ante un fallo recuperable (proveedor saturado, sin red). Se reintenta a los 60 s, como máximo 2 veces (3 intentos en total). Un fallo definitivo, como un número inválido, no se reintenta.
- Antes de cada envío o reintento se revisa la versión. Si fue reemplazada por un Update o Cancel, ya no está aprobada o venció, el SMS queda **Cancelado** y no sale.
- Un aviso de prueba o simulacro (`status` Exercise o Test) no sale por un proveedor real: queda **Bloqueado**. Con el simulador sale marcado `[PRUEBA]`.

## Seguimiento (RF-08)

- `GET /api/difusion/seguimiento`: versiones difundidas con su avance (porcentaje de trabajos terminados).
- `GET /api/difusion/seguimiento/{versionId}`: el CAP emitido, la publicación web, el avance por comunidad (SMS y altavoz), las tareas y cada SMS con sus intentos. También indica si el lote de SMS terminó dentro de los 5 minutos. Los teléfonos se ven enmascarados sin permiso de edición.
- `GET /api/difusion/{versionId}/cap`: el XML exacto que se emitió.
- `POST /api/difusion/tareas/{id}/confirmar` con `{ "observacion": "…" }` (opcional) y `POST /api/difusion/tareas/{id}/no-realizada` con el motivo (obligatorio). Son para Operador o Administrador. Una tarea cerrada no se vuelve a cerrar.
- **Pantalla Seguimiento:** se actualiza sola cada 10 s (RF-08 pide menos de 1 minuto). Los estados están escritos en palabras y el color solo acompaña.

## Importar desde Excel

- La plantilla es `docs/ejemplos/sprint-3/plantilla-aviso.xlsx`: en la hoja "Aviso" va el campo en la columna A y el valor en la B. Las filas `area` y `poligono` se pueden repetir en pares.
- En **Avisos > Importar CAP** se elige el `.xlsx`. La API lo recibe en base64 por el mismo `POST /api/avisos/importar` y lo reconoce por su firma.
- Sigue los mismos pasos que XML y JSON (Template Method) y termina en el Builder: se valida igual. El original se guarda con su SHA-256, calculado sobre el archivo, y se descarga como `.xlsx`.

## Registro propio y acceso

- `POST /api/auth/registro` con nombre de usuario, nombre completo, email y contraseña. La cuenta nace con el rol **Consulta** y vuelve con la sesión iniciada. Los otros roles los da un Administrador.
- El login y el registro tienen un límite de intentos por IP (`LimiteAcceso:PorMinuto`, 10 por defecto y 600 en desarrollo, para que corra la colección Postman).

## Base de datos

La migración `Sprint3Difusion`:

- Crea `caps_emitidos`, `trabajos_difusion` (una sola tabla para envío SMS y tarea manual, con la columna `tipo`) y `eventos_aviso`.
- Agrega `versiones_mensaje.publicada_web_en`.
- Amplía `avisos.tipo_contenido_original` a 100 caracteres (el tipo MIME del Excel).

Relación con el diagrama ER: CAP emitido, Trabajo de difusión (Envío SMS y Tarea manual) y Bitácora quedan como tablas. El **Canal** se guarda como texto en cada trabajo (`Sms`, `Radio`, `Altavoz`, `RedesSociales`), igual que el canal de cada contacto. La relación "Dispone" (Comunidad y Canal, con su responsable) queda para cuando haya acuerdos con las comunidades.

## Archivos de prueba

| Archivo | Qué es |
|---|---|
| `01-alerta-naranja-el-torno.xml` | Alert, severidad Severe (naranja), tramo El Torno |
| `02-actualizacion-roja-el-torno.xml` | Update del 01, Extreme (roja) |
| `03-cancelacion-el-torno.xml` | Cancel del 02 |
| `04-alerta-amarilla-norte.json` | Alert, Moderate (amarilla), Montero, Portachuelo, General Saavedra y Mineros |
| `05-alerta-roja-porongo.xlsx` | Alert, Extreme (roja), Porongo y La Guardia, en la plantilla Excel |
| `06-simulacro-porongo.json` | Simulacro (`status` Exercise) |
| `plantilla-aviso.xlsx` | Plantilla vacía |
| `90-`, `91-`, `92-…` | Inválidos: sin `<info>`, Update sin `references` y Excel sin la hoja "Aviso" |

Se regeneran con `scripts/ejemplos/generar_avisos.py`. Los avisos vencen el 31/12/2026.
