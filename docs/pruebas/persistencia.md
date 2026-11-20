# Persistencia de la zona y su comunidad de prueba

El 19 de noviembre de 2026 se ejecutó la API .NET 10 dos veces contra la misma base PostgreSQL/PostGIS local, `entrega_195441`. El primer arranque creó la base, aplicó las migraciones y cargó los datos. Entre arranques se detuvo el proceso de la API con Ctrl+C.

## Reproducir desde PowerShell

1. Desde la raíz de la solución, inicia PostgreSQL con `docker compose up -d db`. Usa una base nueva y conserva su nombre durante los dos arranques. Para la conexión de desarrollo incluida:

   ```powershell
   $cfg = Get-Content src/SistemaAlertas.Api/appsettings.json -Raw | ConvertFrom-Json
   $basePrueba = 'prueba_persistencia_' + (Get-Date -Format yyyyMMddHHmmss)
   $env:ConnectionStrings__Alertas = $cfg.ConnectionStrings.Alertas.Replace('Database=sistema_alertas', "Database=$basePrueba")
   dotnet run --project src/SistemaAlertas.Api --launch-profile http -- --urls http://localhost:52083
   ```

2. Abre `http://localhost:52083/api/salud` y comprueba que `baseDeDatos` devuelve `ok`. Conéctate a la base de prueba desde un cliente SQL y ejecuta las dos consultas:

   ```sql
   SELECT migration_id FROM "__EFMigrationsHistory" ORDER BY migration_id;

   SELECT z.id AS zona_id, z.nombre, c.id AS comunidad_id, c.nombre
   FROM zonas z JOIN comunidades c ON c.zona_id = z.id
   WHERE z.referencia_documento = 'DEMO-01';
   ```

3. Guarda el resultado, detén la API y repite solo el comando `dotnet run` en la misma terminal. Vuelve a consultar salud y ejecuta ambas consultas sobre la misma base.

## Resultado observado

| Arranque | Salud | Migraciones | Filas relacionadas | ID de zona | ID de comunidad |
|---|---|---:|---:|---|---|
| Primero | ok | 3 | 1 | 633bf8d9-a931-4d3d-9ff4-f8dc7aa8b173 | 088daa18-31d5-4262-9d72-c05ac15e7a73 |
| Después del reinicio | ok | 3 | 1 | 633bf8d9-a931-4d3d-9ff4-f8dc7aa8b173 | 088daa18-31d5-4262-9d72-c05ac15e7a73 |

Ambas consultas devolvieron la zona «Zona de prueba Pirai» y su «Comunidad de prueba». El historial registró `20261104154650_Sprint1Inicial`, `20261110152745_Sprint1CierreRequisitos` y `20261115070344_Sprint2VersionesMensaje` en los dos arranques. Los IDs se generan en cada base nueva y deben coincidir al repetir la consulta sobre esa misma base.
