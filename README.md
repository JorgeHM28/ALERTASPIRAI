# SAT Río Piraí · ALERTASPIRAI

Sistema de alertas tempranas con ASP.NET Core MVC y Web API, .NET 10, EF Core y SQL Server. La interfaz está desarrollada con vistas Razor; Leaflet se utiliza para los mapas. No requiere React ni Node para ejecutarse.

Repositorio: https://github.com/JorgeHM28/ALERTASPIRAI
Rama de esta migración: `pablo/migracion-aspnet-sqlserver`
Base: `sprint-2`, commit `feb34a1701ea0e6facbbc91d888fde9562b2ea4f`. El ZIP original coincide con esa revisión. No se modifica `sprint-2`.

## Estado de la entrega

La solución y sus migraciones SQL Server se generan y verifican desde macOS. Las pruebas que necesitan una instancia real de SQL Server se ejecutan por separado en Windows. El informe `docs/evidencias/Verificacion-parcial.pdf` identifica exactamente qué se verificó y qué sigue pendiente; **no sustituye el PDF final de evidencias de SQL Server**.

Faltan para cerrar la entrega: ejecutar la prueba SQL Server real, importar y conciliar el respaldo del integrante, completar nombres y usuarios del equipo y capturar el CRUD con reinicio. El ZIP de traslado no debe presentarse como una entrega final completa mientras falten esos pasos.

## Integrantes y participación

| Integrante / identificación disponible | Usuario GitHub | Estado |
| --- | --- | --- |
| Pablo, correo Git `pablo@quiroz.me` | Pendiente de confirmar | Migración MVC, SQL Server, pruebas y documentación; confirmar nombre completo y correo vinculado a GitHub |
| Jorge Luis Herbas Mallcu | Pendiente de confirmar | Nombre encontrado en el historial; confirmar usuario |
| César Bayes, registrado como `Cesar Bayes` en Git | Pendiente de confirmar | Nombre encontrado en el historial; confirmar usuario |
| Autor registrado como `mangelomendozac-prog` | Pendiente de confirmar | Confirmar nombre completo y cuenta |

Esta tabla identifica autores del historial, no certifica la lista completa de integrantes. Completarla con los datos reales antes de entregar. Cada integrante debe aportar cambios propios verificables. No se crean commits en nombre de otras personas.

## Entorno y versiones

- Visual Studio **Community 2026**, workload «Desarrollo de ASP.NET y web». Visual Studio Code es otro producto.
- SDK .NET **10.0.404**, fijado en `global.json`; versiones de parche posteriores de esa banda son aceptadas.
- EF Core y `dotnet-ef` **10.0.14**. Dependencias exactas en los `.csproj`.
- SQL Server 2022 o posterior y SSMS. La configuración local del usuario es `localhost\SQLEXPRESS`, autenticación Windows, identidad `PABLO\pablo`.
- Python 3 únicamente para scripts opcionales de medición, PDF y empaquetado; no es requisito de la aplicación.

SSMS administra el motor; no lo sustituye. En Apple Silicon, no se presupone soporte nativo de SQL Server en Windows ARM. Se debe comprobar la instancia ya instalada. Desde macOS, `localhost` se refiere al Mac, no a Windows.

## Configurar y abrir en Windows

1. Extraer el proyecto en `C:\Proyectos\ALERTASPIRAI` y abrir `SistemaAlertas.sln`.
2. Elegir `SistemaAlertas.Api` como proyecto de inicio. La utilidad de traslado no es el proyecto web.
3. En SSMS, conectar con Windows Authentication a `localhost\SQLEXPRESS` y ejecutar:

```sql
SELECT @@VERSION AS VersionMotor, @@SERVERNAME AS Servidor;
```

4. Revisar `src/SistemaAlertas.Api/appsettings.json`. Ejemplo local sin contraseñas:

```json
"ConnectionStrings": {
  "Alertas": "Server=localhost\\SQLEXPRESS;Database=AlertasPirai;Trusted_Connection=True;TrustServerCertificate=True"
}
```

`TrustServerCertificate=True` se usa solo para desarrollo local. Para un servidor remoto se configura un certificado válido y secretos locales. No guardar contraseñas ni claves de producción en Git.

5. Desde la raíz del proyecto, en PowerShell:

```powershell
dotnet --version
dotnet tool restore
dotnet restore
dotnet build
dotnet ef database update --project src/SistemaAlertas.Infrastructure --startup-project src/SistemaAlertas.Api -- --environment Development
```

Las migraciones ya están incluidas. **No ejecutar Add-Migration Inicial de nuevo**. No usar `EnsureCreated`. La aplicación no aplica migraciones automáticamente.

En la Consola del Administrador de paquetes de Visual Studio, la alternativa es:

```powershell
Update-Database -Project SistemaAlertas.Infrastructure -StartupProject SistemaAlertas.Api -Args '--environment Development'
```

## Crear administrador y datos de prueba

Para una base nueva de demostración, guardar una contraseña local de al menos 8 caracteres, con letras y números:

```powershell
dotnet user-secrets set "Semilla:ContrasenaAdmin" "REEMPLAZAR-POR-UNA-CONTRASENA-LOCAL-123" --project src/SistemaAlertas.Api
dotnet run --project src/SistemaAlertas.Api -- --sembrar-demo
```

El usuario inicial es `admin`. La contraseña es la que se configuró. La semilla crea una zona y una comunidad ficticias, identificadas como DEMO. No envía SMS y no reemplaza datos existentes. `--sembrar` crea solamente el primer administrador si la base no contiene usuarios.

**Si se trasladan registros reales, no ejecutar la semilla:** importar primero sobre una base recién migrada y vacía. Se conservan usuarios y hashes de contraseña del origen. Consultar `tools/Traslado/README.md`.

## Ejecutar y abrir Swagger

```powershell
dotnet run --project src/SistemaAlertas.Api --launch-profile http
```

- Web pública: http://localhost:5080/
- Login MVC: http://localhost:5080/cuenta/login
- Swagger UI: http://localhost:5080/swagger
- OpenAPI: http://localhost:5080/openapi/v1.json

En Visual Studio, ejecutar con Ctrl+F5. El perfil HTTPS utiliza `https://localhost:7163`; usar la URL correspondiente al perfil seleccionado.

Para Swagger: ejecutar `POST /api/auth/login`, copiar `token`, pulsar **Authorize** y pegar solo su valor. Las cookies del login MVC no autentican la API. No incluir tokens en las capturas del PDF.

## Estructura y reglas

- `src/SistemaAlertas.Domain`: entidades, CAP y reglas de dominio.
- `src/SistemaAlertas.Application`: servicios y DTO compartidos entre MVC y API.
- `src/SistemaAlertas.Infrastructure`: SQL Server, EF Core, migraciones, seguridad y SMS.
- `src/SistemaAlertas.Api`: controladores REST, controladores MVC, modelos de vista, Razor y `wwwroot`.
- `tests/SistemaAlertas.Tests`: pruebas unitarias, modelo SQL Server, host y prueba integral opcional contra SQL Server real.
- `tools/Traslado`: exportación privada desde PostgreSQL e importación transaccional a SQL Server.
- `postman`: solicitudes del sistema; usar la URL y credenciales locales actuales.

El esquema mantiene usuarios, roles y bitácora; zonas, comunidades y contactos con sus canales e historial; avisos oficiales, versiones y destinos. Las áreas son `geometry` con SRID 4326. Las coordenadas GeoJSON usan longitud/latitud. `docs/modelo-relacional.md` resume relaciones y reglas de eliminación.

La API mantiene las rutas originales y añade `GET /api/contactos/{contactoId}` para que Location sea consultable. Los avisos oficiales son inmutables. Las versiones aprobadas se corrigen mediante Update o Cancel, no sobrescribiéndolas.

El recurso principal evaluable es **Comunidades**, relacionado con **Zonas**:

| Operación | Respuesta |
| --- | --- |
| GET `/api/comunidades` y `/{id}` | 200 |
| POST `/api/comunidades` | 201 y Location |
| PUT `/api/comunidades/{id}` | 200 |
| DELETE `/api/comunidades/{id}` | 204 |
| GET `/api/zonas` y `/{id}` | 200 |
| Nombre vacío, población negativa, zona inválida o ubicación fuera de zona | 400 |
| Recurso solicitado inexistente | 404 |
| Conflicto de clave única o relación detectado por SQL Server | 409 |

## Pruebas y persistencia

```powershell
dotnet test
```

La prueba integral SQL Server se omite explícitamente si no existe `ALERTAS_TEST_SQLSERVER`. Para ejecutarla:

```powershell
$env:ALERTAS_TEST_SQLSERVER = 'Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True'
dotnet test --filter FullyQualifiedName~SqlServerEndToEndTests
Remove-Item Env:ALERTAS_TEST_SQLSERVER
```

La cuenta necesita permiso para crear y eliminar una base de prueba. El test utiliza un nombre aleatorio `AlertasPrueba_*`, nunca la base `AlertasPirai`. Aplica migraciones, verifica CRUD/errores/geografía, inicia sesión MVC, reinicia el host sin resembrar, comprueba persistencia y elimina únicamente su propia base temporal.

Para las capturas de entrega, seguir `docs/evidencias/GUIA.md`. El PDF final debe mostrar las respuestas de SQL Server real, no las pruebas del modelo ni los ejemplos de este README.

## Medición del predominio de ASP.NET

```powershell
python scripts/medir_codigo.py
```

Se cuentan líneas físicas de `.cs`, `.cshtml`, `.js`, `.ts`, `.tsx` y `.css` dentro de `src`, excluyendo migraciones, binarios, librerías de terceros y archivos generados. Se informan C# y Razor por separado. Las pruebas y la utilidad de traslado no inflan el porcentaje. Este es un criterio técnico documentado; el profesor no ha fijado una fórmula oficial del 70%.

## Preparar ZIP

```powershell
python scripts/empaquetar.py
```

Genera un ZIP de **traslado** en `entrega`, sin `bin`, `obj`, `.vs`, `.git`, dependencias descargadas, secretos ni respaldos privados.

Después de completar `docs/evidencias/Evidencias.pdf` y los datos de integrantes:

```powershell
python scripts/empaquetar.py --final
```

Revisar el contenido y extraerlo en otra carpeta para repetir el procedimiento. El modo final exige el PDF y que no queden integrantes pendientes en el README.

## Límites conservados de Sprint 2

Cola SMS, tareas manuales y algunas bitácoras de difusión permanecen en memoria. Se pierden al reiniciar; comunidades, contactos, avisos, versiones y listas guardadas persisten en SQL Server. Los datos temporales no se pueden recuperar desde un respaldo de PostgreSQL.

Siguen pendientes del alcance anterior: catálogo de protocolos oficiales, validación CAP contra XSD completo y separación adicional entre quien prepara y quien aprueba. Los mapas base requieren Internet; los datos y la geometría propia no dependen de React ni de un CDN de Leaflet.
