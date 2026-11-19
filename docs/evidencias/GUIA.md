# Evidencias finales en Swagger y SQL Server

Usar una base DEMO distinta de los registros trasladados. Ejecutar con `Sms:Modo=Simulador`. Guardar capturas legibles sin tokens ni contraseñas, rotuladas con método, ruta y estado HTTP. No reutilizar capturas de otra API.

1. Iniciar la API y abrir Swagger. Capturar las operaciones de Comunidades y Zonas.
2. Iniciar sesión con `POST /api/auth/login` y autorizar el token.
3. `GET /api/zonas` -> 200. Copiar el ID de la zona DEMO.
4. `POST /api/comunidades` con el siguiente cuerpo (reemplazar ZonaId):

```json
{"nombre":"Evidencia persistente","municipio":"Santa Cruz","poblacion":120,"latitud":-17.75,"longitud":-63.2,"zonaId":"ID-DE-LA-ZONA-DEMO"}
```

Capturar **201**, cuerpo y cabecera **Location**. Guardar el ID retornado.

5. GET al Location -> **200**. GET listado -> **200**. GET zona relacionada -> **200**.
6. PUT al ID creado, mismo cuerpo con `poblacion: 150` -> **200**.
7. POST con `nombre: ""` -> **400**. POST con `poblacion: -1` -> **400**. POST con un GUID de zona inexistente -> **400**.
8. GET `/api/comunidades/11111111-1111-1111-1111-111111111111` -> **404**.
9. En SSMS ejecutar, usando el ID real:

```sql
USE AlertasPirai;
SELECT id, nombre, poblacion, zona_id FROM comunidades WHERE id = 'ID-CREADO';
SELECT * FROM __EFMigrationsHistory;
```

10. Capturar GET del registro con población 150. Detener completamente la API desde Visual Studio o Ctrl+C y volver a iniciarla. No aplicar seed, borrar base, importar ni crear el registro otra vez.
11. Ejecutar de nuevo GET con **el mismo ID** -> **200**, población 150. Capturar la respuesta después del reinicio. La aplicación puede exigir un nuevo login JWT.
12. DELETE del ID -> **204**. GET posterior -> **404**.

Reunir las capturas en `docs/evidencias/Evidencias.pdf`, con títulos y explicación breve de cada operación. Incluir imagen del reinicio y consulta SQL para contextualizar la persistencia. El PDF parcial del Mac solo acredita Swagger disponible y las pruebas locales; no prueba persistencia de SQL Server.

## Confirmaciones antes de entregar

- [ ] PDF con operaciones exitosas, 201/Location, 400, 404 y GET tras reinicio.
- [ ] Test SQL Server real aprobado.
- [ ] Traslado real conciliado; respaldo original conservado.
- [ ] Integrantes y usuarios GitHub confirmados en README.
- [ ] Commits propios de cada integrante disponibles para revisión.
- [ ] ZIP extraído y ejecutado siguiendo README.
