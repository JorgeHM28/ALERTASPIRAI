import json,sys
from shapely.geometry import shape, Point, LineString, mapping
from shapely.ops import unary_union
from shapely.validation import make_valid
from pyproj import Geod
geod=Geod(ellps='WGS84')
G=sys.argv[1]; OUT=sys.argv[2]
basin=shape(json.load(open(G+'/cuenca.geojson'))['geometry'])
ba=abs(geod.geometry_area_perimeter(basin)[0])/1e6
riv=json.load(open(G+'/pirai.json'))['elements']
river=unary_union([LineString([(p['lon'],p['lat']) for p in e['geometry']]) for e in riv if e['tags']['name'].lower() in ('río piraí','rio piraí','rio pirai')])
NOMBRE={'Santa Rosa':'Santa Rosa del Sara','San Juan':'San Juan de Yapacaní','Okinawa Uno':'Okinawa Uno'}
zonas=[];munis=[]
for f in json.load(open(G+'/adm3.geojson'))['features']:
    g=shape(f['geometry'])
    if not g.intersects(basin): continue
    i=g.intersection(basin)
    ia=abs(geod.geometry_area_perimeter(i)[0])/1e6; ma=abs(geod.geometry_area_perimeter(g)[0])/1e6
    pct=100*ia/ma
    cauce=g.intersects(river)
    nivel='Alto' if cauce else ('Medio' if pct>=10 else 'Bajo')
    n=NOMBRE.get(f['properties']['shapeName'],f['properties']['shapeName'])
    s=g.simplify(0.0003,preserve_topology=True)
    if not s.is_valid: s=make_valid(s)
    if s.geom_type=='Polygon': pass
    munis.append((n,g))
    desc=(f"Municipio de la cuenca del río Piraí (Santa Cruz). {ia:,.0f} km² dentro de la cuenca ({pct:.1f} % del municipio). "
          + ("El cauce principal del Piraí atraviesa el municipio." if cauce else "El cauce principal del Piraí no atraviesa el municipio."))
    zonas.append({'type':'Feature','properties':{'nombre':n,'descripcion':desc,'nivelRiesgo':nivel},'geometry':mapping(s)})
zonas.sort(key=lambda z:z['properties']['nombre'])
json.dump({'type':'FeatureCollection','features':zonas},open(OUT+'/zonas-municipios.geojson','w'),ensure_ascii=False)
pl=[p for p in json.load(open(G+'/places.json'))['elements'] if 'name' in p['tags'] and '_' not in p['tags']['name']]
com=[]
for n,g in munis:
    dentro=[p for p in pl if g.contains(Point(p['lon'],p['lat']))]
    elegidos={}
    for p in dentro:
        pt=Point(p['lon'],p['lat']); d=pt.distance(river)*111
        if basin.contains(pt) and (p['tags']['place'] in('city','town') or d<=3): elegidos[p['id']]=p
    # cabecera: el lugar mas poblado del municipio, aunque quede fuera de la cuenca
    if dentro:
        cab=max(dentro,key=lambda p:(p['tags']['place'] in ('city','town'),int(p['tags'].get('population','0').replace('.','').replace(',','') or 0)))
        elegidos[cab['id']]=cab
    for p in elegidos.values():
        pop=p['tags'].get('population'); 
        try: pop=int(pop.replace('.','').replace(',','')) if pop else None
        except: pop=None
        com.append({'nombre':p['tags']['name'],'municipio':n,'poblacion':pop,'latitud':round(p['lat'],6),'longitud':round(p['lon'],6),
                    'distanciaRioKm':round(Point(p['lon'],p['lat']).distance(river)*111,1),'osmId':p['id']})
com.sort(key=lambda c:(c['municipio'],c['nombre']))
json.dump(com,open(OUT+'/comunidades.json','w'),ensure_ascii=False,indent=1)
from collections import Counter
print(len(zonas),'zonas', len(com),'comunidades'); print(Counter(c['municipio'] for c in com))
print(Counter(z['properties']['nivelRiesgo'] for z in zonas))
import os; print(os.path.getsize(OUT+'/zonas-municipios.geojson'))
