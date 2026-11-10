# Patrones creacionales en el sistema

Criterio: un patrón se usa solo si resuelve un problema real del sistema. Se evaluaron siete opciones: cuatro se usan y tres se descartan, cada una con su justificación.

| Patrón | Decisión | Dónde | Sprint |
|---|---|---|---|
| Factory Method | Se usa | Importadores de aviso (`IImportadorAviso`), RF-01. Canal SMS por proveedor, RF-04 | S1 y S3 |
| Builder | Se usa | Construcción del mensaje CAP 1.2 | S1 |
| Singleton vía DI | Se usa | Fábricas, validador CAP y configuración (`AddSingleton`) | S1 |
| Prototype | Se usa | Versión n+1 de un mensaje aprobado (`record` + `with`), RF-11 | S2 |
| Abstract Factory | Se descarta | — | — |
| Singleton clásico | Se descarta | — | — |
| Object Pool | Se descarta | — | — |

## Dónde está en el código

| Patrón | Archivos |
|---|---|
| Factory Method | `src/SistemaAlertas.Application/Avisos/Importacion/IImportadorAviso.cs` (producto), `ImportadorCapXml.cs` y `ImportadorCapJson.cs` (productos concretos), `IFabricaImportadores.cs` y `FabricaImportadores.cs` (creador). Lo usa `ServicioAvisos.ImportarAsync`. |
| Builder | `src/SistemaAlertas.Application/Cap/MensajeCapBuilder.cs` (`MensajeCapBuilder` e `InfoCapBuilder`), que produce el `record MensajeCap` de `src/SistemaAlertas.Domain/Cap/MensajeCap.cs`. Lo usan los dos importadores y `ServicioAvisos.RegistrarManualAsync`. |
| Singleton vía DI | `src/SistemaAlertas.Application/DependencyInjection.cs` (importadores y fábrica) y `src/SistemaAlertas.Infrastructure/DependencyInjection.cs` (hasher, generador JWT, opciones). |
| Prototype | Sprint 2. `MensajeCap` ya es un `record` inmutable, así que `with` funcionará sin cambios. |

Las pruebas que demuestran cada patrón están en `tests/SistemaAlertas.Tests/MensajeCapBuilderTests.cs` e `ImportadoresTests.cs`.

## Patrones que se usan

### Factory Method
**Problema:** los avisos llegan en formatos distintos (XML CAP 1.2, registro manual y otras fuentes a futuro). El caso de uso "registrar aviso" no debe depender de cada formato.
**Solución:** una interfaz `IImportadorAviso` con una implementación por formato, y una fábrica que devuelve el importador correcto según el origen.
**Segundo uso (RF-04, Sprint 3):** el canal SMS. Cada proveedor (Twilio o el simulador para la demo) aporta un solo objeto, el emisor. Una fábrica lo crea según la configuración `Sms:Modo`.
**Beneficio:** agregar un formato o un proveedor nuevo es crear una clase nueva sin tocar el código existente (principio abierto/cerrado).

### Builder
**Problema:** un mensaje CAP 1.2 tiene muchos campos obligatorios y opcionales (`identifier`, `sender`, `sent`, `status`, `msgType`, `scope`, uno o varios `info`, `area` con polígonos). Un constructor con tantos parámetros es ilegible y permite crear mensajes inválidos.
**Solución:** un `MensajeCapBuilder` con métodos encadenados y un `Build()` que valida las reglas de CAP antes de devolver el mensaje.
**Beneficio:** mensajes siempre válidos y código legible.

### Singleton mediante inyección de dependencias
**Problema:** algunos servicios deben existir una sola vez en toda la aplicación, como la configuración, las fábricas y el validador CAP.
**Solución:** se registran con `services.AddSingleton<...>()` en ASP.NET Core. El contenedor garantiza que haya una única instancia.
**Cuidado:** el `DbContext` no es singleton. Es *scoped* (una instancia por petición), porque no es seguro entre hilos.

### Prototype (RF-11, Sprint 2)
**Problema:** un mensaje aprobado es inmutable, porque forma parte del registro de auditoría. Si hay que corregirlo, no se modifica: se crea la versión n+1.
**Solución:** el mensaje se modela como un `record` de C#. La expresión `with` clona el mensaje aprobado y cambia solo los campos necesarios, por ejemplo `var v2 = aprobado with { Version = aprobado.Version + 1, Estado = Borrador };`. Es el Prototype nativo del lenguaje.
**Beneficio:** la nueva versión hereda todos los campos sin copiarlos a mano, y la original queda intacta.

## Patrones que se descartan

### Abstract Factory
Este patrón sirve para crear *familias* de objetos relacionados que deben ser compatibles entre sí. En este sistema solo el SMS es un canal automático, y cada proveedor aporta un solo objeto. Como no hay una familia de productos, Factory Method alcanza. Abstract Factory solo agregaría interfaces sin beneficio.

### Singleton clásico (constructor privado y `Instance` estático)
Oculta las dependencias, impide reemplazar la instancia por un mock en las pruebas unitarias y obliga a gestionar a mano la seguridad entre hilos. El contenedor de DI de ASP.NET Core ya da una única instancia sin esos problemas, por eso se usa la variante vía DI.

### Object Pool
Sirve para reutilizar objetos caros de crear. El único recurso caro del sistema son las conexiones a PostgreSQL, y Npgsql ya trae su propio pool de conexiones. Implementar otro pool duplicaría esa funcionalidad.

# Patrones estructurales en el sistema (Sprint 2)

Mismo criterio: se usan los que resuelven un problema real. Cuatro se usan y tres se descartan.

| Patrón | Decisión | Dónde | RF |
|---|---|---|---|
| Adapter | Se usa | Proveedor SMS (Twilio), XML CAP 1.2, geometrías PostGIS a GeoJSON | RF-04, RF-01, RF-09 |
| Facade | Se usa | `ServicioDifusion` (difusión multicanal) y `ModuloCap` (importar, generar, validar CAP) | RF-04, RF-01 |
| Decorator | Se usa | Reintentos, bitácora y modo prueba sobre `IEmisorSms` | RNF-08, RNF-04 |
| Proxy (protección) | Se usa | `ProxyRepositorioContactos` enmascara los números | RF-12 |
| Bridge | Se descarta | — | — |
| Composite | Se descarta | — | — |
| Flyweight | Se descarta | — | — |

## Dónde está en el código

| Patrón | Archivos |
|---|---|
| Adapter (SMS) | `Application/Difusion/Sms/IEmisorSms.cs` (objetivo), `Infrastructure/Sms/TwilioEmisor.cs` (adapta el SDK de Twilio), `Infrastructure/Sms/SimuladorEmisor.cs`, `Infrastructure/Sms/RegistroSms.cs` (elige por `Sms:Modo`). |
| Adapter (CAP) | `Application/Avisos/Importacion/ImportadorCapXml.cs`: XML CAP 1.2 de OASIS → `MensajeCap` → entidad `Aviso`. |
| Adapter (GeoJSON) | `Application/Abstracciones/IConversorGeoJson.cs` (objetivo), `Infrastructure/Geo/ConversorGeoJson.cs` (adapta NetTopologySuite y su serializador). Lo usan `ZonasController` y `AvisosController`. |
| Facade | `Application/Difusion/ServicioDifusion.cs` con los subsistemas de `Application/Difusion/Abstracciones.cs` (`IPublicadorWeb`, `IDestinatariosDifusion`, `IColaSms`, `IGestorTareasManuales`, `IRegistroIncidencias`, `IFuenteVersionesAprobadas`) e implementaciones en `Infrastructure/Difusion/`. `Application/Cap/IModuloCap.cs` y `ModuloCap.cs` con `ValidadorCap.cs`. Endpoints `POST /api/difusion/{versionAprobadaId}` y `POST /api/avisos/validar-cap`. |
| Decorator | `Infrastructure/Sms/Decoradores/`: `EmisorSmsDecorador` (base), `EmisorConReintentos`, `EmisorConBitacora`, `EmisorModoPrueba`. Cadena armada en `RegistroSms.AddCanalSms`. |
| Proxy | `Application/Comunidades/IRepositorioContactos.cs` (sujeto), `Infrastructure/Persistence/Repositorios/RepositorioContactos.cs` (real), `ProxyRepositorioContactos.cs` (proxy), `Application/Comunidades/EnmascaradorDatos.cs`, `Api/Comun/UsuarioActualHttp.cs`. |

Pruebas: `ConversorGeoJsonTests`, `ImportadorCapAdapterTests` (en `ImportadoresTests.cs`), `TwilioEmisorTests`, `DecoradoresSmsTests`, `ProxyRepositorioContactosTests`, `ModuloCapTests` y `ServicioDifusionTests`.

## Patrones que se usan

### Adapter
**Problema:** el sistema depende de interfaces ajenas que no controla: el SDK de Twilio, el XML CAP 1.2 del SENAMHI y las geometrías de PostGIS/NetTopologySuite, mientras que el mapa Leaflet espera GeoJSON.
**Solución:** una interfaz propia por necesidad (`IEmisorSms`, `IImportadorAviso`, `IConversorGeoJson`) y una clase que traduce hacia la interfaz ajena. `TwilioEmisor` convierte `MensajeSms` en `CreateMessageOptions` y los errores HTTP de Twilio (429, 5xx, 4xx) en un `ResultadoEnvioSms` que dice si el fallo es recuperable.
**Beneficio:** cambiar de proveedor SMS o de formato no toca los casos de uso. El simulador (`Sms:Modo = Simulador`, por defecto) permite la demo sin credenciales; las de Twilio van en user-secrets.

### Facade
**Problema:** difundir una alerta (RF-04) implica publicar en la web, buscar destinatarios con SMS, crear los trabajos SMS y crear las tareas de radio, altavoz y redes sociales. El controlador no debería coordinar cinco subsistemas.
**Solución:** `ServicioDifusion.DifundirAsync(versionAprobadaId)` hace todo en una llamada. Cada canal corre por separado: si la web falla, queda una incidencia y los SMS y las tareas siguen. `ModuloCap` hace lo mismo para CAP: importar, generar y validar desde un solo punto.
**Beneficio:** un punto de entrada simple; los subsistemas pueden cambiar (por ejemplo, la cola en memoria por una persistente en el Sprint 3) sin tocar a quien difunde.

### Decorator
**Problema:** el envío SMS necesita reintentos ante fallos pasajeros (RNF-08), bitácora de cada acción (RNF-04) y protección para que un aviso de prueba no llegue a la población. Meter todo eso en cada proveedor lo duplicaría.
**Solución:** decoradores que implementan `IEmisorSms` y envuelven a otro. Cadena: `EmisorConBitacora` → `EmisorModoPrueba` → `EmisorConReintentos` → proveedor. Los reintentos son como máximo 2 adicionales y solo ante un fallo recuperable. La bitácora va por fuera para registrar el resultado final; el modo prueba va antes de los reintentos para que un envío bloqueado no se reintente.
**Beneficio:** cada responsabilidad está en una clase pequeña y probada por separado, y sirve igual para Twilio o el simulador.

### Proxy de protección
**Problema:** los teléfonos de los contactos (RF-12) son datos personales. Un usuario de consulta o aprobador no necesita verlos completos, y la web pública nunca debe recibirlos.
**Solución:** `ProxyRepositorioContactos` tiene la misma interfaz que el repositorio real y controla el acceso: con permiso de edición devuelve los datos completos; con sesión pero sin permiso enmascara (`+591 7*****89`, `j***@porongo.bo`); sin sesión no deja ningún dígito. También enmascara el historial. La difusión usa el repositorio real porque es un proceso interno que necesita los números.
**Beneficio:** la regla de privacidad está en un solo lugar; los controladores reciben el proxy sin saberlo.

## Patrones que se descartan

### Bridge
Sirve cuando una abstracción y su implementación deben variar en dos dimensiones independientes (por ejemplo, tipos de mensaje × proveedores). Aquí solo varía el proveedor del canal SMS; los demás canales son manuales. Una sola jerarquía con Adapter alcanza; Bridge agregaría una segunda jerarquía sin uso.

### Composite
Sirve para tratar igual a objetos individuales y a grupos anidados en árbol. Las zonas, comunidades y contactos tienen una relación fija de dos niveles (zona → comunidad → contacto), no un árbol de profundidad variable, y los polígonos ya se unen con `MultiPolygon` de NetTopologySuite. Composite no resuelve ningún problema.

### Flyweight
Sirve para compartir estado entre una enorme cantidad de objetos pequeños y ahorrar memoria. El sistema maneja decenas de zonas y cientos de contactos, no millones de objetos; además .NET ya internaliza cadenas y las enumeraciones CAP son valores. El ahorro sería nulo y complicaría el código.
