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
| Prototype | `Domain/Mensajes/ContenidoMensaje.cs` (`record` que se clona con `with`) y `VersionMensaje.Corregir` (constructor de copia: la versión n+1 de una rechazada). `ServicioVersiones.DerivarAsync` clona el contenido aprobado para preparar un Update o Cancel. |

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
**Solución:** el contenido de cada versión es el `record ContenidoMensaje`. Corregir una versión rechazada crea la versión n+1 clonándola (`Contenido with { }`, misma referencia, mismo tipo, mismo área) en borrador y con el motivo del rechazo a la vista; preparar un Update o Cancel clona el contenido aprobado y cambia solo la redacción (`Contenido.Redactar(...)`, que también usa `with`). Es el Prototype nativo del lenguaje.
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
| Decorator | Se usa | Bitácora y modo prueba sobre `IEmisorSms` (y reintentos inmediatos, disponibles) | RNF-08, RNF-04 |
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
**Cambio en el Sprint 3:** RNF-08 pide esperar 60 s entre reintentos. Esperar dentro de la misma llamada bloquearía la cola, así que la cadena que usa la API quedó en `EmisorConBitacora` → `EmisorModoPrueba` → proveedor, y el reintento lo agenda la cola persistente: `EnvioSms.RegistrarIntento` guarda el próximo intento 60 s después, solo si el fallo es recuperable y como máximo 2 veces. `EmisorConReintentos` sigue en el código y en las pruebas para reintentos inmediatos.

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

# Patrones de comportamiento en el sistema (Sprint 2)

Mismo criterio: se usan los que resuelven un problema real. De los nueve, siete se usan y dos se descartan.

| Patrón | Decisión | Dónde | RF |
|---|---|---|---|
| State | Se usa | Ciclo de vida de la versión del mensaje (Borrador → En revisión → Aprobada → Difundida, o Rechazada) y del aviso | RF-02, RF-11 |
| Chain of Responsibility | Se usa | Reglas que la versión aprobada debe pasar antes de difundirse | RF-04, RF-11 |
| Command | Se usa | Un comando por canal de difusión (web, SMS, tareas manuales) y los trabajos SMS en cola | RF-04 |
| Observer | Se usa | Eventos del aviso: bitácora, log y paso a "Difundido" | RNF-04, RF-11 |
| Strategy | Se usa | Redacción del mensaje para radio, altavoz y redes sociales | RF-04 |
| Template Method | Se usa | Pasos comunes de los importadores CAP en XML y JSON | RF-01 |
| Iterator | Se usa (nativo de C#) | Cola SMS (`ReadAllAsync`), comandos de canal, observadores, `Infos()` de los importadores | RF-04 |
| Mediator | Se descarta | — | — |
| Memento | Se descarta | — | — |

## Dónde está en el código

| Patrón | Archivos |
|---|---|
| State | `Domain/Mensajes/EstadosVersion.cs` (`EstadoVersionBase`, `EstadoVersionBorrador`, `EstadoVersionEnRevision`, `EstadoVersionAprobada`, `EstadoVersionRechazada`, `EstadoVersionDifundida`) y `VersionMensaje.Redactar/EnviarARevision/Aprobar/Rechazar/Corregir`. El aviso conserva su propio State (`Domain/Avisos/EstadosAviso.cs`): pasa a Aprobado cuando se aprueba su primera versión. Endpoints `POST /api/versiones/{id}/aprobar` y `/rechazar` (permiso Aprobación). |
| Chain of Responsibility | `Application/Difusion/ReglasDifusion.cs` (`ReglaDifusion` y sus eslabones). La usa `Infrastructure/Difusion/FuenteVersionesAprobadas.cs`. |
| Command | `Application/Difusion/ComandosDifusion.cs` (`IComandoDifusion`, `PublicarEnWeb`, `EncolarSms`, `CrearTareasManuales`, `ContextoDifusion`). El invocador es `ServicioDifusion`. `TrabajoSms` es el comando encolado que ejecuta `ProcesadorColaSms`. |
| Observer | `Application/Avisos/Eventos/EventosAviso.cs` (sujeto `PublicadorEventosAvisos`, `IObservadorAvisos`), `Infrastructure/Difusion/ObservadoresAvisos.cs` (`BitacoraAvisosEnMemoria`, `ObservadorLogAvisos`, `MarcarAvisoDifundido`). Endpoint `GET /api/avisos/{id}/eventos`. |
| Strategy | `Application/Difusion/RedaccionCanales.cs` (`IRedaccionCanal` y una estrategia por canal), elegida por canal en `GestorTareasManualesEnMemoria`. |
| Template Method | `Application/Avisos/Importacion/ImportadorCapBase.cs`; `ImportadorCapXml` e `ImportadorCapJson` solo implementan los pasos. |
| Iterator | `Api/Comun/ProcesadorColaSms.cs` (`await foreach` sobre `ReadAllAsync`), `ServicioDifusion` (`foreach` de comandos), `PublicadorEventosAvisos` (`foreach` de observadores). Se usa el de C# (`IEnumerable<T>`, `IAsyncEnumerable<T>`), no una implementación propia. |

Pruebas: `PatronesComportamientoTests` (State, cadena, Observer, Command, Strategy y Template Method), `ServicioDifusionTests` (Facade y Command) e `ImportadoresTests` (Template Method con Adapter). `VersionesMensajeTests` cubre las versiones (State y Prototype), las referencias, los destinos y el nivel en texto. En total 144 pruebas; la colección Postman pasa completa (119 peticiones, 260 aserciones).

## Patrones que se usan

### State
**Problema:** RF-11 exige que solo se difunda lo aprobado y que una versión aprobada no se edite. Con un `enum` y `if` repartidos, cada acción (editar, enviar a revisión, aprobar, rechazar, corregir, difundir) tendría que preguntar en qué estado está la versión, y un estado nuevo obligaría a revisar todos esos `if`.
**Solución:** cada estado es una clase que decide qué acciones permite y a cuál lleva. `VersionMensaje` delega en su `EstadoActual`. Se guarda solo el `enum` (como texto) y de él sale el objeto de estado.
**Reglas:** solo el borrador se edita; pasa a revisión solo si está completo (titular o descripción, instrucciones, área, expiración vigente), si no queda como borrador. Solo una versión en revisión se aprueba o se rechaza, y el rechazo exige motivo. Una rechazada no se edita: se corrige creando la versión siguiente (Prototype). Una aprobada es inmutable: se corrige con un Update o se cancela con un Cancel que la referencia.
**Beneficio:** las transiciones válidas están en un solo lugar y las prueba `PatronesComportamientoTests`.

### Chain of Responsibility
**Problema:** antes de difundir hay varias verificaciones (aviso no descartado, versión aprobada, no reemplazada por un Update o Cancel, no vencida, completa). Estaban escritas en secuencia dentro de la fuente de versiones; agregar una nueva era editar ese método.
**Solución:** cada verificación es un eslabón con una sola responsabilidad. El primero que encuentra un problema corta la cadena con su motivo. La cadena se arma una vez (`CadenaPorDefecto`) y se registra como Singleton vía DI.
**Beneficio:** el motivo que ve el operador es preciso ("está Registrado: un aprobador debe aprobarlo") y una regla nueva es una clase más. `ReglaVigente` mira la fecha en el momento de difundir, porque el aviso pudo vencer después de aprobarse.

### Command
**Problema:** la fachada de difusión tenía los tres canales escritos a mano. Un canal nuevo (WhatsApp, sirenas) obligaba a modificarla.
**Solución:** cada canal es un comando con `EjecutarAsync(contexto)`. `ServicioDifusion` es el invocador: los ejecuta en el orden registrado, aísla los fallos y registra las incidencias. Los trabajos SMS ya eran comandos encolados que se ejecutan después en segundo plano.
**Beneficio:** agregar un canal es registrar una clase en `DependencyInjection`; la fachada no cambia.

### Observer
**Problema:** cuando un aviso se registra, aprueba, descarta, vence o difunde, hay que dejarlo en la bitácora (RNF-04), escribir en el log y, al difundirse, cambiar su estado. Hacerlo dentro de cada caso de uso los acopla a todas esas reacciones.
**Solución:** los casos de uso publican un `EventoAviso`. `PublicadorEventosAvisos` avisa a cada `IObservadorAvisos` registrado. Si un observador falla, los demás reciben igual el evento y la acción principal no se deshace.
**Beneficio:** una reacción nueva (por ejemplo, un correo al COED) es otro observador. En el Sprint 3 la bitácora pasó a la tabla `eventos_aviso` (`BitacoraAvisosPersistente`) detrás de la misma interfaz: los casos de uso no cambiaron.

### Strategy
**Problema:** cada canal manual necesita un texto distinto: la radio lee el texto completo, el altavoz necesita una frase corta y repetible y las redes llevan el enlace. Era un `switch` dentro del gestor de tareas.
**Solución:** una estrategia de redacción por canal (`IRedaccionCanal`). El gestor elige la estrategia por canal en tiempo de ejecución.
**Beneficio:** cambiar cómo se redacta un canal no toca al gestor; las pruebas inyectan una estrategia distinta. En el Sprint 3 la elección quedó en `RedactorTareas` (el contexto de la estrategia), que usa el gestor persistente.

### Template Method
**Problema:** los importadores XML y JSON repetían el mismo algoritmo (leer, llenar el encabezado, llenar cada `info`, construir) y nada impedía que uno nuevo se saltara la validación del Builder.
**Solución:** `ImportadorCapBase<TAlerta, TInfo>.Importar` fija el orden de los pasos y no se puede redefinir. Cada formato implementa solo `Leer`, `LlenarEncabezado`, `Infos` y `LlenarInfo`. `Reconoce` es un gancho con una implementación por defecto.
**Beneficio:** un formato nuevo escribe solo lo que cambia y siempre termina en la validación CAP. Así entró el Excel en el Sprint 3: `ImportadorCapExcel` solo implementa los cuatro pasos y redefine el gancho `Reconoce` (un .xlsx en base64 empieza con `UEsDB`).

### Iterator (nativo de C#)
**Problema:** la cola SMS, la lista de comandos de canal y la de observadores son colecciones distintas que hay que recorrer sin depender de cómo están guardadas.
**Solución:** se usa el Iterator que trae el lenguaje: `IEnumerable<T>` y `foreach`. No se escribe una implementación propia, igual que el Singleton se usa vía DI y no en su forma clásica.
**Beneficio:** quien recorre no depende de cómo se guarda la colección. En el Sprint 3 la cola SMS pasó de un `Channel` en memoria a la tabla `trabajos_difusion`: `DespachadorSms` recorre cada lote con el mismo `foreach`.

## Patrones que se descartan

### Mediator
Sirve cuando muchos objetos se comunican entre sí y conviene centralizar esa comunicación. Aquí la coordinación ya la resuelven la fachada de difusión (un punto de entrada) y el Observer (avisos desacoplados). Un mediador o una librería como MediatR agregaría una capa más de indirección para un sistema de este tamaño.

### Memento
Sirve para guardar y restaurar el estado de un objeto, por ejemplo para "deshacer". Un mensaje aprobado no se deshace: se corrige creando la versión n+1 (Prototype) y el historial queda en la bitácora. Restaurar un estado anterior iría contra la trazabilidad que pide RF-11.

# Cómo encajan los tres grupos de patrones

Los 15 patrones que se usan (4 creacionales, 4 estructurales y 7 de comportamiento) no están aislados: cada uno resuelve un tramo del recorrido de un aviso, del SENAMHI a la población.

| Etapa | Qué pasa | Patrones |
|---|---|---|
| 1. Llega el aviso (RF-01) | `ModuloCap` recibe el XML, JSON o Excel, la fábrica elige el importador, este sigue los pasos fijos y traduce el formato ajeno, y el Builder valida el CAP 1.2 | Facade → Factory Method → Template Method → Adapter → Builder |
| 2. Se registra | `ServicioAvisos` guarda el aviso en estado Registrado y publica el evento | State, Observer |
| 3. Se prepara (RF-02) | `ServicioVersiones` crea la versión Alert, Update o Cancel desde el aviso; el Builder arma el XML CAP 1.2 y el módulo CAP lo valida al enviar a revisión; corregir clona la versión | State, Prototype, Builder, Facade |
| 4. Se aprueba (RF-11) | El estado de la versión decide si se puede aprobar o rechazar; la bitácora lo registra | State, Observer |
| 5. Se difunde (RF-04) | La fachada pide la versión; la cadena de reglas la verifica (también que no se haya difundido antes); emite y archiva el CAP final; ejecuta los comandos de canal; cada canal manual redacta con su estrategia | Facade, Chain of Responsibility, Command, Strategy |
| 6. Sale el SMS | Cada envío SMS queda en la tabla de trabajos; el despachador recorre los lotes; el emisor está decorado con bitácora y modo prueba, y adapta Twilio o el simulador; el reintento lo agenda el propio envío | Command, Iterator, Decorator, Adapter, Factory Method |
| 7. Cierre y seguimiento (RF-08) | El evento Difundido pasa la versión y el aviso a ese estado y queda en la bitácora; el panel de seguimiento muestra el avance y las tareas confirmadas | Observer, State |
| Transversal | Datos de contacto protegidos, objetos de una sola instancia, geometrías a GeoJSON | Proxy, Singleton vía DI, Adapter |

Reglas que mantienen el equilibrio:
- **Un patrón, un problema.** Ninguna clase combina dos patrones sin necesidad. Donde dos se tocan, uno usa al otro por su interfaz: la cadena consulta al State (`PermiteDifundir`), la fachada invoca comandos y publica al Observer, el Template Method termina en el Builder.
- **Todo pasa por DI.** Las fábricas, la cadena, las estrategias, los comandos, los decoradores y los observadores se arman en `DependencyInjection.cs` y `RegistroSms.cs`. Cambiar una pieza es cambiar un registro, no el código que la usa.
- **La persistencia no cambió la difusión (Sprint 3).** La cola SMS, las tareas, la publicación web y la bitácora pasaron de memoria a la base detrás de las mismas interfaces (`IColaSms`, `IGestorTareasManuales`, `IPublicadorWeb`, `IBitacoraAvisos`). La fachada solo sumó un paso: emitir el CAP antes de los canales.
- **Las versiones no cambiaron la difusión.** Al llegar las versiones del mensaje (Sprint 2), solo se reemplazó la fuente de versiones (`FuenteVersionesAprobadas`) y los destinos (`DestinatariosDesdeLista`, RF-07): la fachada, los comandos y las estrategias quedaron igual.
- **Lo que no resuelve nada se descarta y se dice por qué:** Abstract Factory, Singleton clásico, Object Pool, Bridge, Composite, Flyweight, Mediator y Memento.
