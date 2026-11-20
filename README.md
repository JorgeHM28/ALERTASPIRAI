# Sistema de Alertas Tempranas · Río Piraí

**Proyecto:** Alertas Piraí, Sistema de Alertas Tempranas de inundación del río Piraí. API ASP.NET Core sobre **.NET 10** con PostgreSQL + PostGIS.

**Rama de la entrega:** [`entrega-api-swagger`](https://github.com/JorgeHM28/ALERTASPIRAI/tree/entrega-api-swagger)

## Equipo

| Integrante | Usuario de GitHub |
|---|---|
| César Bayes | [`rodetias123-coder`](https://github.com/rodetias123-coder) |
| Jorge Herbas | [`JorgeHM28`](https://github.com/JorgeHM28) |
| Miguel Mendoza | [`mangelomendozac-prog`](https://github.com/mangelomendozac-prog) |
| Pablo Quiroz | [`pabloquirozxd`](https://github.com/pabloquirozxd) |

**Acceso del docente:** el repositorio es privado. Jorge Herbas (`JorgeHM28`), dueño del repositorio, gestiona el acceso del docente.

## Entrega de la API desde Visual Studio

1. Instala Visual Studio con soporte para .NET 10 y la carga de trabajo ASP.NET. Abre `SistemaAlertas.sln` y establece `SistemaAlertas.Api` como proyecto de inicio.
2. Para una base compartida, en el proyecto API abre **Administrar secretos de usuario** y configura la conexión que te entregue el equipo. Ejemplo de formato:

   ```json
   {
     "ConnectionStrings": {
       "Alertas": "Host=HOST;Port=5432;Database=BASE;Username=USUARIO;Password=CLAVE;SSL Mode=VerifyFull"
     }
   }
   ```

   La base debe admitir PostGIS y el usuario debe poder aplicar las migraciones. En un servicio administrado, habilita PostGIS en el esquema public o comprueba que sus tipos y funciones sean visibles para la conexión. Como alternativa local, abre Docker Desktop y ejecuta `docker compose up -d db` desde la carpeta de la solución. Así se usa la conexión de desarrollo incluida en `appsettings.json`, sin configurar secretos:

   ```
   Host=localhost;Port=5433;Database=sistema_alertas;Username=alertas;Password=alertas_dev
   ```

3. Elige el perfil **http** y pulsa **F5**. Al iniciar, la API aplica las migraciones pendientes de `src/SistemaAlertas.Infrastructure/Persistence/Migraciones`: `Sprint1Inicial`, `Sprint1CierreRequisitos` y `Sprint2VersionesMensaje`. No hace falta ejecutar `dotnet ef`. Datos iniciales en Development: el usuario `admin` y, cuando la tabla de zonas está vacía, la zona ficticia «Zona de prueba Pirai» (referencia `DEMO-01`) con la «Comunidad de prueba» vinculada. Visual Studio abre `http://localhost:5080/swagger`. Sin Visual Studio: `dotnet run --project src/SistemaAlertas.Api --launch-profile http`.
4. En Swagger, ejecuta **POST /api/auth/login** con los campos `nombreUsuario` y `contrasena`. Usa la cuenta de prueba suministrada por el equipo (en desarrollo local, `admin` y `Admin12345`). Copia el campo `token`, pulsa **Authorize** y pega solo su valor. Ejecuta **GET /api/zonas** para consultar los datos.
5. Para comprobar persistencia, crea una zona adicional, anota el ID devuelto, detén la API y vuelve a pulsar F5. Consulta ese ID en Swagger.


Sistema de alertas de inundación basado en CAP 1.2 (OASIS). Proyecto de Sistemas I (Scrum).

## Stack
- Backend: ASP.NET Core (.NET 10), EF Core + Npgsql + NetTopologySuite, autenticación JWT
- Base de datos: PostgreSQL 17 + PostGIS (Docker)
- Frontend: React 19 + Vite + TypeScript + Leaflet

## Cómo ejecutar

### Opción 1: todo con Docker (recomendada para el equipo)
Solo necesitas Docker Desktop abierto.

```bash
docker compose up -d --build     # base de datos, API y frontend
```

| Servicio | Dirección | Contenedor |
|---|---|---|
| Web | http://localhost:5173 | `sistema_alertas_web` |
| API | http://localhost:5080 (OpenAPI en `/openapi/v1.json`) | `sistema_alertas_api` |
| PostgreSQL + PostGIS | localhost:5433 | `sistema_alertas_db` |

Comandos útiles: `docker compose logs -f api` (ver logs), `docker compose down` (apagar) y `docker compose down -v` (apagar y borrar los datos).

### Opción 2: desarrollo local (para programar)
Requisitos: .NET SDK 10, Node 20 o superior y Docker Desktop.

```bash
docker compose up -d db                               # solo la base de datos
dotnet run --project src/SistemaAlertas.Api           # API en http://localhost:5080 (aplica migraciones al iniciar)
cd frontend && npm install && npm run dev             # Web en http://localhost:5173
```

No mezcles las dos opciones: si la API de Docker está corriendo, ocupa el puerto 5080.

Usuario inicial (solo en Development): **admin / Admin12345**. Cámbiale la contraseña después de entrar.

Para probar rápido en la web:
1. **Zonas → Importar GeoJSON** con `docs/ejemplos/zonas-pirai.geojson`.
2. **Comunidades → Nueva comunidad** dentro de una zona, y agrégale contactos con sus canales.
3. **Avisos → Importar CAP** con `docs/ejemplos/aviso-senamhi.xml` o `docs/ejemplos/aviso-cap.json`, o **Registrar a mano**.

## Pruebas

- **Unitarias:** `dotnet test` ejecuta las pruebas xUnit del dominio, la aplicación y los patrones (144 en esta entrega).
- **API con Postman:** la colección `postman/collections/SAT Rio Pirai` tiene una carpeta por requisito (RF-13, RF-09, RF-12 y RF-01), con 32 peticiones y 50 tests. Incluye los casos de error y de permisos, por ejemplo "Consulta no puede crear zonas → 403".
  - En la app de Postman, abre esta carpeta del repo (Local View) o impórtala con **Import**, elige el entorno **Local** y ejecuta la colección completa con **Run**. El login guarda el token solo.
  - Por consola: `postman collection run "postman/collections/SAT Rio Pirai" -e postman/environments/Local.environment.yaml`

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
postman/                           Colección y entorno de Postman (formato v3, YAML)
frontend/                          SPA React + Leaflet
docs/patrones.md                   Patrones creacionales usados y descartados
docs/ejemplos/                     GeoJSON y mensajes CAP de ejemplo
```

## Patrones de diseño
Ver [docs/patrones.md](docs/patrones.md).

## Notas
- En Mac con Apple Silicon la imagen `postgis/postgis` corre emulada (`platform: linux/amd64`), porque no publica una versión arm64.
- La clave JWT de `appsettings.Development.json` es solo para desarrollo. En producción se define con la variable de entorno `Jwt__Clave`.
