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
