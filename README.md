# Sistema de Alertas Tempranas · Río Piraí

Sistema de alertas de inundación basado en CAP 1.2 (OASIS). Proyecto de Sistemas I (Scrum).

## Stack
- Backend: ASP.NET Core (.NET 10), EF Core + Npgsql + NetTopologySuite, autenticación JWT
- Base de datos: PostgreSQL 17 + PostGIS (Docker)
- Frontend: React 19 + Vite + TypeScript + Leaflet

## Cómo ejecutar
Requisitos: .NET SDK 10, Node 20 o superior y Docker Desktop.

```bash
docker compose up -d                                  # PostgreSQL + PostGIS en el puerto 5433
dotnet run --project src/SistemaAlertas.Api           # API en http://localhost:5080 (aplica migraciones al iniciar)
cd frontend && npm install && npm run dev             # Web en http://localhost:5173
```

Usuario inicial (solo en Development): **admin / Admin12345**. Cámbiale la contraseña después de entrar.

Para probar rápido, en la web:
1. **Zonas → Importar GeoJSON** con `docs/ejemplos/zonas-pirai.geojson`.
2. **Comunidades → Nueva comunidad** dentro de una zona y agrégale contactos con sus canales.
3. **Avisos → Importar CAP** con `docs/ejemplos/aviso-senamhi.xml` o `docs/ejemplos/aviso-cap.json`, o **Registrar a mano**.

Pruebas: `dotnet test` (39 pruebas del Builder, los importadores, la fábrica y las reglas de dominio).

## Alcance del Sprint 1

| Requisito | Qué hace | API |
|---|---|---|
| RF-13 Usuarios y permisos | Login con JWT, 4 roles (Administrador, Operador, Aprobador, Consulta), alta, edición, activar o desactivar y cambio de contraseña. Siempre queda al menos un Administrador activo. | `/api/auth`, `/api/usuarios` |
| RF-09 Zonas en GeoJSON | Polígonos WGS84 guardados en PostGIS, validados (tipo, rango, autointersección). Importación de una FeatureCollection y exportación para Leaflet. | `/api/zonas`, `/api/zonas/importar`, `/api/zonas/geojson` |
| RF-12 Comunidades, contactos y canales | Comunidades por zona (con ubicación validada dentro del polígono), contactos con canales SMS, WhatsApp, llamada, email o radio, y un canal preferido. | `/api/comunidades`, `/api/contactos` |
| RF-01 Registro de avisos e importación CAP | Importa CAP 1.2 en XML o JSON (detecta el formato), valida contra el estándar, evita duplicados y guarda el área en PostGIS. El registro manual genera el CAP desde las zonas elegidas. | `/api/avisos`, `/api/avisos/importar`, `/api/avisos/{id}/cap` |

### Permisos por rol

| Rol | Ver datos | Editar zonas, comunidades y avisos | Gestionar usuarios |
|---|---|---|---|
| Administrador | ✔ | ✔ | ✔ |
| Operador | ✔ | ✔ | — |
| Aprobador | ✔ | — (aprobará mensajes en el Sprint 2) | — |
| Consulta | ✔ | — | — |

La matriz está en `src/SistemaAlertas.Api/Comun/Politicas.cs`.

## Estructura
```
src/SistemaAlertas.Domain          Entidades y reglas (Usuario, Zona, Comunidad, Contacto, Aviso, modelo CAP)
src/SistemaAlertas.Application     Casos de uso, Builder CAP, Factory Method de importadores
src/SistemaAlertas.Infrastructure  EF Core + PostGIS, migraciones, hash de contraseñas, JWT
src/SistemaAlertas.Api             Controladores REST, autorización por rol, manejo de errores
tests/SistemaAlertas.Tests         Pruebas xUnit
frontend/                          SPA React + Leaflet
docs/patrones.md                   Patrones creacionales usados y descartados
docs/ejemplos/                     GeoJSON y mensajes CAP de ejemplo
```

## Patrones de diseño
Ver [docs/patrones.md](docs/patrones.md).

## Notas
- En Mac con Apple Silicon la imagen `postgis/postgis` corre emulada (`platform: linux/amd64`), porque no publica una versión arm64.
- La clave JWT de `appsettings.Development.json` es solo para desarrollo. En producción se define con la variable de entorno `Jwt__Clave`.
