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

### Demostración con Supabase

1. Extrae el ZIP en una carpeta permanente. Instala Visual Studio con soporte para .NET 10 y la carga de trabajo **Desarrollo de ASP.NET y web**.
2. Abre PowerShell en la carpeta que contiene esta solución y ejecuta:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\Configurar-Demo.ps1
   ```

   Ingresa la clave de demostración enviada por el equipo por separado. Haz esto una sola vez por computadora. Si mueves el proyecto, repite la configuración para actualizar la ruta del certificado. El script guarda la conexión en los secretos de usuario del proyecto API y valida el servidor mediante el certificado incluido en `docs/supabase-ca.crt`.
3. Abre `SistemaAlertas.sln`, establece **SistemaAlertas.Api** como proyecto de inicio, selecciona **http** y pulsa **F5**. Swagger abre en `http://localhost:5080/swagger`.
4. En **POST /api/auth/login**, pulsa **Try it out**, envía `{"nombreUsuario":"admin","contrasena":"Admin12345"}` y copia el campo `token`. En **Authorize**, pega el token y confirma. Ejecuta **GET /api/zonas**. La cuenta de Swagger es distinta de la cuenta de conexión a PostgreSQL.
5. Sigue los casos en [pruebas de CRUD](docs/pruebas/swagger.md) y [errores](docs/pruebas/errores.md). El [PDF de evidencias](docs/Evidencias-Swagger.pdf) muestra las respuestas obtenidas.

La base compartida ya contiene las tres migraciones y la zona de prueba con su comunidad. La cuenta del docente tiene permisos de lectura y escritura en las tablas de la aplicación. En este modo se omite la migración al iniciar.

### Reproducir desde una base local vacía

Usa PostgreSQL con PostGIS. Para iniciar solo la base incluida ejecuta `docker compose up -d db`. Si usaste el script de demostración, elimina las dos configuraciones locales antes de probar contra Docker:

```powershell
dotnet user-secrets remove "ConnectionStrings:Alertas" --project src/SistemaAlertas.Api
dotnet user-secrets remove "BaseDeDatos:MigrarAlIniciar" --project src/SistemaAlertas.Api
```

El perfil de desarrollo usa la conexión de Docker incluida y aplica automáticamente las migraciones, hasta `Sprint3Difusion`. Crea los usuarios iniciales y, cuando la tabla de zonas está vacía, carga los datos reales de la cuenca: los 22 municipios como zonas y 48 localidades como comunidades (ver `datos/cuenca-pirai/README.md`). Para otro servidor configura tu propia cadena. Ejemplo sin credenciales reales:

```text
Host=HOST;Port=5432;Database=BASE;Username=USUARIO;Password=CLAVE;SSL Mode=VerifyFull;Search Path=public,extensions
```

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

Usuarios iniciales (solo en Development): **admin / Admin12345**, **operador / operador12345** y **aprobador / Aprobador12345**. Quien prepara un mensaje no puede aprobarlo, así que la aprobación la hace otra persona. Las cuentas de solo lectura las crea cada persona desde el login (**Crear una de consulta**). Cámbiales la contraseña después de entrar.

Para probar rápido en la web (el detalle está en [docs/sprint-3.md](docs/sprint-3.md)):
1. **Comunidades:** elige una localidad (por ejemplo, El Torno) y agrégale tu contacto con el canal SMS.
2. **Avisos → Importar CAP** con un archivo de `docs/ejemplos/sprint-3` (XML, JSON o Excel), o **Registrar a mano**.
3. **Mensajes:** prepara la versión, envíala a revisión, apruébala con otro usuario y difúndela.
4. **Seguimiento:** mira el avance por comunidad y confirma las tareas de radio, altavoz y redes.

## Pruebas

Plan, matriz de trazabilidad y defectos corregidos: [docs/sqa.md](docs/sqa.md).

- **Unitarias:** `dotnet test` ejecuta las pruebas xUnit del dominio, la aplicación, la cola SMS y los datos de la cuenca (205 en esta entrega, 81 % de cobertura de líneas).
- **API con Postman:** la colección `postman/collections/SAT Rio Pirai` tiene una carpeta por requisito, con 145 peticiones y 322 aserciones. Incluye los casos de error y de permisos, por ejemplo "Consulta no puede crear zonas → 403".
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
docs/patrones.md                   Patrones usados y descartados, y cómo encajan
docs/sprint-2.md, docs/sprint-3.md Qué hace cada sprint
docs/sqa.md                        Aseguramiento de la calidad
docs/ejemplos/sprint-3/            Avisos simulados (XML, JSON y Excel) y la plantilla Excel
datos/cuenca-pirai/                Municipios y localidades reales de la cuenca
scripts/                           Limpieza de la base y generación de datos y ejemplos
.github/workflows/ci.yml           Integración continua
```

## Patrones de diseño
Ver [docs/patrones.md](docs/patrones.md).

## Notas
- En Mac con Apple Silicon la imagen `postgis/postgis` corre emulada (`platform: linux/amd64`), porque no publica una versión arm64.
- La clave JWT de `appsettings.Development.json` es solo para desarrollo. En producción se define con la variable de entorno `Jwt__Clave`.
