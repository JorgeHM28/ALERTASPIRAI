# Traslado de PostgreSQL a SQL Server

La aplicación nueva solo utiliza SQL Server. Esta utilidad separada usa Npgsql exclusivamente para leer una copia consistente del origen.

## Antes de exportar

El integrante que conserva la base debe respaldarla con `pg_dump` y conservar el original. No borrar el volumen de Docker. Se recomienda restaurar el respaldo en una instancia PostgreSQL 17 con PostGIS y exportar desde esa copia. El esquema debe corresponder a Sprint 2: tres migraciones originales, archivadas aquí como referencia.

El archivo `docker-compose.postgres-original.yml.txt` es una copia histórica de la configuración anterior; contiene rutas relativas al repositorio antiguo. No es el arranque de la versión MVC.

## Exportar en el equipo que tiene PostgreSQL

Definir `ALERTAS_POSTGRES` en el entorno local con la conexión privada (host, puerto, base, usuario y contraseña), sin escribirla en Git. Después:

```powershell
dotnet run --project tools/Traslado/Datos -- exportar C:\Respaldos\pirai.transfer.json
```

La lectura usa una transacción RepeatableRead de solo lectura. Incluye las 13 tablas de negocio, registros relacionados, GUID, contraseñas hasheadas, fechas, JSON, XML y geometrías WKB con SRID. No copia `__EFMigrationsHistory`, porque pertenece al proveedor anterior.

No reescribe un archivo existente. El archivo contiene datos personales y hashes: transferirlo de forma privada y mantenerlo fuera del repositorio y del ZIP académico.

## Importar en Windows

1. Configurar una base destino nueva y vacía.
2. Aplicar las migraciones SQL Server desde la raíz del proyecto.
3. No ejecutar semilla.
4. Definir la conexión destino:

```powershell
$env:ALERTAS_SQLSERVER = 'Server=localhost\SQLEXPRESS;Database=AlertasPirai;Trusted_Connection=True;TrustServerCertificate=True'
dotnet run --project tools/Traslado/Datos -- importar C:\Respaldos\pirai.transfer.json
Remove-Item Env:ALERTAS_SQLSERVER
```

La importación verifica nombres de tablas y columnas, exige que todas las tablas estén vacías y trabaja en una transacción Serializable. Preserva identificadores e identidades. Para resolver las referencias circulares, suspende las restricciones solo dentro de la transacción y las vuelve a validar con `WITH CHECK CHECK` antes del commit. No deja restricciones deshabilitadas al confirmar.

Compara todas las filas importadas mediante valores canónicos y SHA-256, incluyendo WKB de geometrías, JSON y fechas. Una diferencia o violación de relación revierte la importación completa. No elimina, recorta ni renombra datos para ocultar incompatibilidades.

## Aceptación del traslado

- Revisar cantidades y reconciliación de las 13 tablas.
- Iniciar sesión con una cuenta existente.
- Consultar comunidades, contactos e historial con roles distintos.
- Abrir un aviso original y comprobar su SHA-256.
- Abrir versiones, mapas y destinos guardados.
- Detener y arrancar la aplicación; comprobar los mismos identificadores.

Hasta verificar esos pasos, conservar PostgreSQL como origen y no cambiar el entorno compartido. La utilidad está compilada, pero el traslado real requiere el archivo del integrante. Las colas y bitácoras que el sistema original mantenía solo en memoria no están en la exportación.
