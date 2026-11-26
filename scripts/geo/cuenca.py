import json, sys, shapefile
from shapely.geometry import shape, Point, mapping
from shapely.ops import unary_union
G=sys.argv[1]
r=shapefile.Reader(G+'/lev12/hybas_sa_lev12_v1c')
flds=[f[0] for f in r.fields[1:]]
bb=(-65.5,-19.8,-62.5,-15.5)
recs={}
for sr in r.iterShapeRecords():
    b=sr.shape.bbox
    if b[2]<bb[0] or b[0]>bb[2] or b[3]<bb[1] or b[1]>bb[3]: continue
    a=dict(zip(flds,sr.record))
    recs[a['HYBAS_ID']]=(a,shape(sr.shape.__geo_interface__))
print('basins in bbox',len(recs))
pt=Point(float(sys.argv[2]),float(sys.argv[3]))
pour=[k for k,(a,g) in recs.items() if g.contains(pt)][0]
up={}
for k,(a,g) in recs.items(): up.setdefault(a['NEXT_DOWN'],[]).append(k)
sel=set();st=[pour]
while st:
    k=st.pop(); sel.add(k); st+=up.get(k,[])
print('pour',pour,'n',len(sel),'UP_AREA',recs[pour][0]['UP_AREA'])
basin=unary_union([recs[k][1] for k in sel]).buffer(0)
json.dump({'type':'Feature','properties':{},'geometry':mapping(basin)},open(G+'/cuenca.geojson','w'))
