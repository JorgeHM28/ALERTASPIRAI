# Validación y errores

En Swagger, inicia sesión y pulsa **Authorize**. Usa una zona temporal con nombre único. Sustituye los IDs de esta ejecución por los que devuelva tu API. Para persistencia, detén y reinicia la API después del PUT y repite el GET.

## Geometría inválida

`POST /api/zonas` → **400**. Proceso 1496.

```json
{
  "nombre": "Geometría inválida",
  "descripcion": "Ensayo de entrega",
  "nivelRiesgo": "Medio",
  "geometria": {
    "type": "Point",
    "coordinates": [
      -63.2,
      -17.8
    ]
  },
  "fuenteInstitucional": "Equipo de pruebas",
  "referenciaDocumento": "QA-01",
  "fechaDocumento": "2026-01-01"
}
```

Respuesta: {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Datos no válidos","status":400,"detail":"La geometría debe ser Polygon o MultiPolygon, no Point.","traceId":"00-d7fbd0e37a8345b6b14471a516d4c0f7-31fc6c39fe5e2f24-00"}

## Nombre obligatorio

`POST /api/zonas` → **400**. Proceso 1496.

```json
{
  "nombre": "",
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

Respuesta: {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Datos no válidos","status":400,"detail":"El nombre de la zona es obligatorio.","traceId":"00-b97bd60db9601ab6e716a027f976293e-2b4fe7ee3a4fa077-00"}

## Referencia a una zona inexistente

`POST /api/comunidades` → **400**. Proceso 1496.

```json
{
  "nombre": "Ensayo de referencia",
  "municipio": "Porongo",
  "poblacion": 100,
  "zonaId": "00000000-0000-0000-0000-000000000001"
}
```

Respuesta: {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Datos no válidos","status":400,"detail":"La zona indicada no existe.","traceId":"00-7cc3157f9253b7b958f892c9453f0398-68e29605faa13b49-00"}

## Consulta de la zona eliminada

`GET /api/zonas/91443279-ee5d-455f-9b11-7b1be85c94a8` → **404**. Proceso 1496.

Respuesta: {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,"traceId":"00-50de2e2969642524dbd2a45c1bdc36da-dacc4489b835923a-00"}
