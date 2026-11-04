# Patrones creacionales en el MVP

Criterio: un patrón se usa solo si resuelve un problema real del sistema. Para cada uno de los cinco patrones creacionales (GoF) se indica si se aplica y por qué.

| Patrón | ¿Se usa? | Dónde |
|---|---|---|
| Factory Method | Sí | Importadores de aviso (`IImportadorAviso`) |
| Builder | Sí | Construcción del mensaje CAP 1.2 |
| Singleton | Sí, vía DI | Servicios sin estado y configuración |
| Abstract Factory | No | — |
| Prototype | No (por ahora) | — |

## Factory Method — se usa
**Problema:** los avisos llegan en formatos distintos (XML CAP 1.2, registro manual, y a futuro otras fuentes como SENAMHI). El caso de uso "registrar aviso" no debe depender de cada formato.
**Solución:** una interfaz `IImportadorAviso` con una implementación por formato, y una fábrica que devuelve el importador correcto según el origen.
**Beneficio:** agregar un formato nuevo es crear una clase nueva sin tocar el código existente (principio abierto/cerrado).

## Builder — se usa
**Problema:** un mensaje CAP 1.2 tiene muchos campos obligatorios y opcionales (`identifier`, `sender`, `sent`, `status`, `msgType`, `scope`, uno o varios `info`, `area` con polígonos). Un constructor con tantos parámetros es ilegible y permite crear mensajes inválidos.
**Solución:** un `MensajeCapBuilder` con métodos encadenados y un `Build()` que valida las reglas de CAP antes de devolver el mensaje.
**Beneficio:** mensajes siempre válidos y código legible.

## Singleton — se usa, mediante inyección de dependencias
**Problema:** algunos servicios deben existir una sola vez en toda la aplicación (configuración, la fábrica de importadores, el validador CAP).
**Solución:** se registran con `services.AddSingleton<...>()` en ASP.NET Core. No se implementa el Singleton clásico con constructor privado y campo estático.
**Por qué así:** el Singleton clásico dificulta las pruebas unitarias y oculta dependencias. Con DI se consigue una única instancia y además se puede reemplazar por un mock en los tests.
**Cuidado:** el `DbContext` no es singleton (es *scoped*, una instancia por petición) porque no es seguro entre hilos.

## Abstract Factory — no se usa
Sirve para crear *familias* de objetos relacionados que deben ser compatibles entre sí (por ejemplo, componentes de UI para Windows o macOS). En el MVP no hay familias de productos: cada importador produce un solo tipo de objeto (un aviso). Usarlo agregaría capas sin beneficio. Se reconsideraría si cada canal de difusión (SMS, WhatsApp, correo) necesitara su propio conjunto de formateador, emisor y validador.

## Prototype — no se usa por ahora
Sirve cuando crear un objeto es costoso y conviene clonar uno existente. En el MVP crear un aviso es barato. Un posible uso futuro son las *plantillas de alerta* (clonar una alerta tipo "crecida nivel rojo" y cambiar solo la zona). Si se implementan, se evaluará este patrón.
