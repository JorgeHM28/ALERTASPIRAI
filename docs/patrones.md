# Patrones creacionales en el MVP

Criterio: un patrón se usa solo si resuelve un problema real del sistema. Para cada uno de los cinco patrones creacionales (GoF) se indica si se aplica y por qué.

| Patrón | ¿Se usa? | Dónde |
|---|---|---|
| Factory Method | Sí | Importadores de aviso (`IImportadorAviso`) |
| Builder | Sí | Construcción del mensaje CAP 1.2 |
| Singleton | Sí, vía DI | Servicios sin estado y configuración |
| Prototype | Planificado (Sprint 2) | RF-11: versionado de mensajes aprobados |
| Abstract Factory | Planificado (Sprint 3) | RF-04: canal SMS (Twilio o simulador) |

Los cinco patrones están justificados, pero se implementan en el sprint donde aparece el requisito que los necesita. En el MVP del Sprint 1 solo se implementan los tres primeros.

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

## Prototype — planificado para el Sprint 2 (RF-11)
**Problema:** un mensaje aprobado es inmutable, porque forma parte del registro de auditoría. Si hay que corregirlo, no se modifica: se crea la versión n+1.
**Solución:** el mensaje se modela como un `record` de C#, y la expresión `with` clona el aprobado cambiando solo los campos necesarios, por ejemplo `var v2 = aprobado with { Version = aprobado.Version + 1, Estado = Borrador };`. Ese es el Prototype nativo del lenguaje.
**Beneficio:** la nueva versión hereda todos los campos sin copiarlos a mano y la original queda intacta.
**Por qué no en el Sprint 1:** RF-01 solo registra avisos. El versionado llega con la aprobación (RF-11).

## Abstract Factory — planificado para el Sprint 3 (RF-04)
**Problema:** el canal SMS necesita una *familia* de objetos que deben ser compatibles entre sí: un emisor de mensajes y un receptor de reportes. En producción se usa Twilio y en desarrollo o en la demo se usa un simulador. Mezclar un emisor de Twilio con un receptor simulado sería un error.
**Solución:** `ICanalSmsFactory` con dos implementaciones, `TwilioFactory` y `SimuladorFactory`, elegidas por la configuración `Sms:Modo`. Cada fábrica crea su emisor y su receptor.
**Diferencia con Factory Method:** Factory Method crea *un* producto (un importador). Abstract Factory crea una *familia coherente* de productos.
**Por qué no en el Sprint 1:** la difusión por SMS no forma parte del alcance de este sprint.
