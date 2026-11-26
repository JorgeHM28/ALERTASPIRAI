# Datos reales de la cuenca del río Piraí

Estos archivos son los datos geográficos con los que arranca el sistema: los 22 municipios de la cuenca del Piraí (como zonas, RF-09) y sus localidades (como comunidades, RF-12). Los avisos, en cambio, son simulados (ver `docs/ejemplos/sprint-3`).

| Archivo | Contenido |
|---|---|
| `zonas-municipios.geojson` | 22 municipios con su límite oficial, nivel de riesgo y descripción. |
| `comunidades.json` | 48 localidades: la cabecera de cada municipio y los poblados a 3 km o menos del cauce principal. Sin contactos. |
| `cuenca-pirai.geojson` | Contorno de la cuenca hasta la confluencia (solo referencia, no se carga). |

## Fuentes

- **Límites municipales:** GeoBolivia (Infraestructura de Datos Espaciales del Estado Plurinacional de Bolivia), capa de municipios de 2015, publicada en geoBoundaries (gbOpen BOL ADM3). Licencia abierta.
- **Cuenca:** HydroSHEDS, HydroBASINS nivel 12 v1c (WWF / USGS). Se tomó la subcuenca del Piraí cerca de la confluencia (-64,12; -16,30) y todas las que drenan hacia ella: unos 14.500 km².
- **Cauce del Piraí y localidades:** OpenStreetMap (© colaboradores de OpenStreetMap, ODbL). La población es la que trae cada localidad en OSM (`population`).

## Cómo se eligieron

1. **Municipios:** son los que se cruzan con la cuenca. Salen 22, el mismo número que usa el documento del proyecto. Mairana, Postrervalle y Cabezas apenas la tocan (menos del 1 % de su territorio).
2. **Nivel de riesgo:** la app lo exige y no hay un dato oficial por municipio, así que se calculó con este criterio, que queda escrito en la descripción de cada zona:
   - **Alto:** el cauce principal del Piraí pasa por el municipio.
   - **Medio:** al menos el 10 % del municipio está dentro de la cuenca.
   - **Bajo:** el resto.
3. **Geometría:** es el límite completo del municipio, simplificado con una tolerancia de 0,0003° (unos 30 m) para que el mapa cargue rápido. El polígono queda válido.
4. **Comunidades:** la localidad más poblada de cada municipio (su cabecera), más los poblados dentro de la cuenca que están a 3 km o menos del cauce.

## Cómo se cargan

La API los carga al iniciar si `Semilla:CuencaPirai` es `true` y la tabla de zonas está vacía (ver `SemillaCuencaPirai`). Pasan por las mismas validaciones del dominio que una carga manual.

Para regenerarlos: `scripts/geo/cuenca.py` y `scripts/geo/dataset.py` (Python con shapely, pyshp y pyproj).
