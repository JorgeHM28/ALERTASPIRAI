# Pruebas del CRUD y persistencia

En Swagger, inicia sesión y pulsa **Authorize**. Usa una zona temporal con nombre único. Sustituye los IDs de esta ejecución por los que devuelva tu API. Para persistencia, detén y reinicia la API después del PUT y repite el GET.

## Listado de zonas

`GET /api/zonas` → **200**. Proceso 25932.

## Comunidades de la zona precargada

`GET /api/comunidades?zonaId=1723a432-8fd7-4537-8196-dd770635d36b` → **200**. Proceso 25932.

## Creación y cabecera Location

`POST /api/zonas` → **201**. Proceso 25932.

```json
{
  "nombre": "Prueba docente 1795142960391",
  "descripcion": "Ensayo de entrega",
  "nivelRiesgo": "Medio",
  "geometria": {
    "type": "Polygon",
    "coordinates": [
      [
        [
          -63.2,
          -17.8
        ],
        [
          -63.1,
          -17.8
        ],
        [
          -63.1,
          -17.7
        ],
        [
          -63.2,
          -17.8
        ]
      ]
    ]
  },
  "fuenteInstitucional": "Equipo de pruebas",
  "referenciaDocumento": "QA-01",
  "fechaDocumento": "2026-01-01"
}
```

Location: http://127.0.0.1:52118/api/zonas/91443279-ee5d-455f-9b11-7b1be85c94a8

## Consulta por ID

`GET /api/zonas/91443279-ee5d-455f-9b11-7b1be85c94a8` → **200**. Proceso 25932.

## Actualización antes del reinicio

`PUT /api/zonas/91443279-ee5d-455f-9b11-7b1be85c94a8` → **200**. Proceso 25932.

```json
{
  "nombre": "Prueba docente 1795142960391 revisada",
  "descripcion": "Ensayo de entrega",
  "nivelRiesgo": "Medio",
  "geometria": {
    "type": "Polygon",
    "coordinates": [
      [
        [
          -63.2,
          -17.8
        ],
        [
          -63.1,
          -17.8
        ],
        [
          -63.1,
          -17.7
        ],
        [
          -63.2,
          -17.8
        ]
      ]
    ]
  },
  "fuenteInstitucional": "Equipo de pruebas",
  "referenciaDocumento": "QA-01",
  "fechaDocumento": "2026-01-01"
}
```

## Persistencia después de reiniciar la API

`GET /api/zonas/91443279-ee5d-455f-9b11-7b1be85c94a8` → **200**. Proceso 1496.

## Eliminación de la zona temporal

`DELETE /api/zonas/91443279-ee5d-455f-9b11-7b1be85c94a8` → **204**. Proceso 1496.
