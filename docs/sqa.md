# Aseguramiento de la calidad (SQA)

Sprint 3. Cómo se prueba el sistema, qué cubre cada prueba, cómo se mide y qué defectos se encontraron.

## Resumen

| Control | Herramienta | Resultado (27/11/2026) |
|---|---|---|
| Pruebas unitarias y de integración | xUnit, EF Core en memoria | 205 pruebas, todas pasan |
| Pruebas de la API real | Colección Postman (CLI) contra PostgreSQL/PostGIS | 145 peticiones y 322 aserciones, 0 fallas. Se puede correr dos veces seguidas: la segunda da 321, porque dos pruebas aceptan "ya estaba hecho" |
| Cobertura de código | coverlet (`tests/cobertura.runsettings`) | 81,4 % de líneas y 76,5 % de ramas (Domain 87,2 %, Application 81,0 %, Infrastructure 72,3 %) |
| Análisis estático .NET | Analizadores de .NET, conjunto recomendado (`Directory.Build.props`) | 0 avisos; en CI se compila con `-warnaserror` |
| Análisis estático frontend | TypeScript estricto (`tsc -b`) y ESLint | 0 errores, 2 advertencias de estilo de React (`Comunidades.tsx` y `Mapa.tsx`) |
| Integración continua | GitHub Actions (`.github/workflows/ci.yml`) | Backend, frontend y Postman en verde en GitHub |

## Cómo correr cada control

```bash
# Pruebas
dotnet test tests/SistemaAlertas.Tests

# Cobertura (deja coverage.cobertura.xml en ./cobertura)
dotnet test tests/SistemaAlertas.Tests --settings tests/cobertura.runsettings --collect:"XPlat Code Coverage" --results-directory cobertura

# Análisis estático como error
dotnet build SistemaAlertas.sln -warnaserror

# Frontend
cd frontend && npx tsc -b && npm run lint

# API real: levantar el sistema y correr la colección completa
docker compose up -d --build
postman collection run "postman/collections/SAT Rio Pirai" -e postman/environments/Local.environment.yaml
```

La colección deja datos de prueba en la base. Para no ensuciar la base de trabajo, se puede copiar a otra y apuntar la API a la copia:

```bash
docker exec sistema_alertas_db psql -U alertas -d postgres -c "CREATE DATABASE sistema_alertas_pruebas TEMPLATE sistema_alertas"
ConnectionStrings__Alertas="Host=localhost;Port=5433;Database=sistema_alertas_pruebas;Username=alertas;Password=alertas_dev" \
  ASPNETCORE_URLS=http://localhost:5081 dotnet run --no-launch-profile --project src/SistemaAlertas.Api
postman collection run "postman/collections/SAT Rio Pirai" -e postman/environments/Local.environment.yaml --env-var baseUrl=http://localhost:5081
```

## Estrategia

1. **Dominio primero.** Las reglas de negocio viven en las entidades (estados del aviso y de la versión, reintentos del SMS, tareas manuales, CAP emitido) y se prueban sin base ni red.
2. **Servicios con dobles de prueba.** La difusión, el módulo CAP y los decoradores SMS se prueban con fuentes, colas y proveedores falsos, para forzar fallos que con el sistema real son difíciles de provocar (el servidor web no responde, el proveedor SMS se cae).
3. **Persistencia con EF Core en memoria.** La cola SMS y la carga de la cuenca usan el mismo `AlertasDbContext` que la API, con un proveedor en memoria: son rápidas y corren en cualquier máquina.
4. **API real con Postman.** La colección recorre el sistema completo contra PostgreSQL/PostGIS: permisos, validaciones, el flujo aviso → versión → aprobación → difusión → seguimiento, y los casos de error.
5. **Datos de prueba propios.** Los avisos de `docs/ejemplos/sprint-3` (XML, JSON y Excel, válidos e inválidos) se usan en las pruebas automáticas y sirven para probar a mano.

## Matriz de trazabilidad

| Requisito | Pruebas xUnit | Carpeta Postman |
|---|---|---|
| RF-01 Registro e importación de avisos | `ImportadoresTests`, `AvisoAptitudTests`, `ModuloCapTests`, `EjemplosSprint3Tests` | RF-01 Avisos CAP; RF-04 RF-08 (Excel) |
| RF-02 Versiones Alert, Update y Cancel (un Cancel no lleva instrucciones de evacuación) | `VersionesMensajeTests`, `MensajeCapBuilderTests` | RF-02 RF-11 Mensajes y aprobación; RF-06 (Preparar Cancel) |
| RF-04 Difusión multicanal y CAP emitido; un Update o Cancel detiene lo pendiente de la versión anterior | `ServicioDifusionTests`, `TrabajosDifusionTests`, `ColaSmsTests`, `PatronesComportamientoTests` | RF-06 Web pública y difusión; RF-04 RF-08 |
| RF-05 Protocolos oficiales | `VersionesMensajeTests` | RF-02 RF-11 Mensajes y aprobación |
| RF-06 Texto y nivel en palabras | `VersionesMensajeTests` | RF-06 Web pública y difusión |
| RF-07 Destinos por comunidad | `VersionesMensajeTests` | RF-07 Destinos por comunidad |
| RF-08 Seguimiento | `DatosCuencaPiraiTests` (conteos) | RF-04 RF-08 Difusión y seguimiento |
| RF-09 Zonas | `ConversorGeoJsonTests`, `DominioTests`, `DatosCuencaPiraiTests` | RF-09 Zonas GeoJSON |
| RF-11 Aprobación y rechazo | `VersionesMensajeTests`, `PatronesComportamientoTests` | RF-02 RF-11 Mensajes y aprobación |
| RF-12 Comunidades y contactos | `DominioTests`, `ProxyRepositorioContactosTests` | RF-12 Comunidades y contactos |
| RF-13 Usuarios, roles y registro propio | `DominioTests` | RF-13 Usuarios y permisos; RF-04 RF-08 (registro) |
| RNF-02 Lote SMS en menos de 5 minutos | `ColaSmsTests` (1000 SMS, envío en paralelo) | — |
| RNF-04 Bitácora | `DecoradoresSmsTests`, `PatronesComportamientoTests` | RF-04 RF-08 (bitácora del aviso) |
| RNF-08 Reintentos solo ante fallos recuperables, revisando antes que la versión siga vigente | `TrabajosDifusionTests`, `ColaSmsTests`, `DecoradoresSmsTests` | — |

## Reglas de análisis estático que no se aplican

Están en `.editorconfig`, cada una con su motivo:

- **CA1707:** los nombres de las pruebas usan guiones bajos para leerse como frases.
- **CA1725:** los parámetros se llaman `ct` aunque la interfaz diga `cancellationToken`.
- **CA1710:** el código está en español (`ExcepcionDominio`).
- **CA1848 y CA1873:** el registro de alto rendimiento (`LoggerMessage`) no compensa con el volumen de logs del sistema.
- **CA1861:** las migraciones las genera EF Core.

## Defectos encontrados y corregidos en el Sprint 3

| # | Defecto | Cómo se encontró | Corrección |
|---|---|---|---|
| 1 | Importar un Excel daba error 500: su tipo MIME (65 caracteres) no entraba en `avisos.tipo_contenido_original` (64). | Postman, carpeta RF-04 RF-08 | La columna admite 100 caracteres (migración `Sprint3Difusion`). |
| 2 | Si una comunidad o un contacto se borraba después de guardar la lista de destinos, difundir fallaba por clave foránea. Además el error dejaba cambios pendientes que hacían fallar a los canales siguientes y a la bitácora. | Postman, carpeta RF-06 | Al difundir se vuelve a mirar cada contacto (debe existir y seguir activo y autorizado) y cada comunidad. Si un canal falla, se descartan sus cambios pendientes (`GuardarODescartarAsync`). |
| 3 | Un Update cuya versión anterior se aprobó pero nunca se difundió no se podía difundir: exigía un CAP previo que no existía. | Postman, carpeta RF-06 | El enlace al CAP previo queda vacío en ese caso; el XML conserva su `<references>`. |
| 4 | Una versión se podía difundir dos veces y duplicaba los SMS y las tareas. | Revisión del flujo | Nueva regla `ReglaNoDifundidaAntes` en la cadena de difusión y CAP emitido único por versión. |
| 5 | La fecha del documento de origen en el GeoJSON de zonas dependía de la configuración regional del servidor. | Analizador CA1305 | Se formatea con la cultura invariante. |
| 6 | El nombre de zona se comparaba en minúsculas según la configuración regional. | Analizadores CA1304 y CA1311 | Diccionario sin distinción de mayúsculas (`OrdinalIgnoreCase`) y minúsculas invariantes. |
| 7 | El login y el registro aceptaban intentos sin límite (fuerza bruta, cuentas masivas). | Revisión de seguridad | Límite por IP (`LimiteAcceso:PorMinuto`, 10 por defecto). |
| 8 | La cola SMS reintentaba dentro de la misma llamada con esperas de medio segundo; con los 60 s que pide RNF-08 habría bloqueado la cola. | Revisión de RNF-08 | El reintento lo agenda la cola persistente (`EnvioSms.ProximoIntento`). |

### Defectos del informe de pruebas del equipo (27/11/2026)

Un integrante del equipo probó el Sprint 3 por la interfaz y por la API y encontró estos cuatro. Están corregidos y cada uno tiene su prueba.

| # | Defecto | Corrección | Prueba |
|---|---|---|---|
| 9 | **RF-04:** un Update o Cancel no detenía los SMS ni las tareas pendientes de la versión anterior: el mensaje viejo podía salir después del nuevo. | Al difundir un Update o Cancel, el primer comando (`DetenerDespachosAnteriores`) cierra como **Cancelado** lo pendiente de las otras versiones del evento, con el motivo. Lo ya enviado o confirmado no se toca. | `ColaSmsTests`, Postman "Difundir el Cancel detiene lo pendiente" |
| 10 | **RNF-08:** los envíos y reintentos no revisaban que la versión siguiera aprobada y vigente. | Antes de cada envío o reintento la cola consulta `VersionMensaje.MotivoParaDetenerDifusion`: si la versión fue reemplazada, ya no está aprobada o venció, el SMS se cancela y no sale. Un Cancel vale aunque haya vencido. | `ColaSmsTests`, `TrabajosDifusionTests` |
| 11 | **Riesgo para la población:** una cancelación salía con las instrucciones de evacuación de la alerta. La copiaba de la versión que cancelaba, o la pisaba al vincular un protocolo. | Un Cancel no hereda el texto ni el protocolo de la alerta. Usa la instrucción oficial del Cancel o un texto de cierre (`InstruccionCancelNeutra`), y no admite protocolo. En la pantalla no aparece el selector de protocolo para un Cancel. | `VersionesMensajeTests`, Postman "Preparar Cancel" |
| 12 | Un aviso Cancel figuraba como "no apto para difusión", pero se difundía. | La regla es que **sí** se difunde: la población debe saber que la alerta terminó. Un Cancel enlazado a su alerta es apto sin expiración, área ni instrucción propias. Si no se encuentra el aviso que cancela, el motivo es "referencia inexistente". | `AvisoAptitudTests` |

## Riesgos que quedan

- **Twilio no se probó con una cuenta real.** El adaptador está probado con un cliente simulado; los SMS de la demo los "envía" el simulador.
- **El nivel de riesgo de cada municipio es un criterio propio**, no un dato oficial (ver `datos/cuenca-pirai/README.md`).
- **Reintento real de SMS con Twilio** (fallo del proveedor, 60 s, máximo 2): probado solo con proveedores falsos en `ColaSmsTests`, porque el simulador nunca falla.
