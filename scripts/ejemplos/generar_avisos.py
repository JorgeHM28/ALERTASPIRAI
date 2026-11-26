"""Genera los avisos simulados del Sprint 3 (XML, JSON y Excel) en docs/ejemplos/sprint-3.

Los avisos son SIMULADOS (no hubo convenio con SENAMHI ni SEARPI): el remitente es una dirección local del sistema y la
nota lo dice. Las áreas sí son reales: tramos del río Piraí que contienen localidades cargadas en datos/cuenca-pirai.
Uso: python scripts/ejemplos/generar_avisos.py   (necesita openpyxl)
"""
import json, pathlib
from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill, Alignment

OUT = pathlib.Path(__file__).resolve().parents[2] / 'docs' / 'ejemplos' / 'sprint-3'
OUT.mkdir(parents=True, exist_ok=True)
NS = 'urn:oasis:names:tc:emergency:cap:1.2'
REMITENTE = 'senamhi-simulado@sat-pirai.local'
NOMBRE = 'SENAMHI Santa Cruz (simulado)'
NOTA = 'Aviso simulado para pruebas del SAT Río Piraí. No es un aviso oficial.'

def rect(lat1, lon1, lat2, lon2):
    """Rectángulo en formato CAP (lat,lon), cerrado."""
    p = [(lat1, lon1), (lat1, lon2), (lat2, lon2), (lat2, lon1), (lat1, lon1)]
    return ' '.join(f'{a},{b}' for a, b in p)

# Tramos reales del Piraí (contienen localidades de datos/cuenca-pirai/comunidades.json).
TRAMO_EL_TORNO = ('Río Piraí, tramo El Torno (La Angostura a Junta Piraí)', rect(-17.95, -63.53, -18.18, -63.34))
TRAMO_PORONGO = ('Río Piraí, tramo Porongo y La Guardia', rect(-17.74, -63.38, -17.95, -63.22))
TRAMO_NORTE = ('Río Piraí, tramo norte (Montero, Portachuelo, General Saavedra y Mineros)', rect(-17.00, -63.42, -17.45, -63.19))

AVISOS = [
    dict(archivo='01-alerta-naranja-el-torno.xml', id='SENAMHI-SIM-2026-1201-001', sent='2026-12-01T07:00:00-04:00', tipo='Alert',
         refs=None, sev='Severe', urg='Expected', cer='Likely', efectivo='2026-12-01T07:00:00-04:00', expira='2026-12-31T23:00:00-04:00',
         titular='Alerta naranja por crecida del río Piraí en El Torno',
         desc='Se esperan lluvias de 60 a 90 mm en la cuenca alta en las próximas 24 horas. El nivel del río en La Angostura subió 1,2 m desde ayer.',
         instr='Aléjese de las riberas del Piraí. Prepare su mochila de emergencia y ubique la ruta de evacuación de su comunidad.',
         area=TRAMO_EL_TORNO),
    dict(archivo='02-actualizacion-roja-el-torno.xml', id='SENAMHI-SIM-2026-1201-002', sent='2026-12-01T13:00:00-04:00', tipo='Update',
         refs=f'{REMITENTE},SENAMHI-SIM-2026-1201-001,2026-12-01T07:00:00-04:00', sev='Extreme', urg='Immediate', cer='Observed',
         efectivo='2026-12-01T13:00:00-04:00', expira='2026-12-31T23:00:00-04:00',
         titular='Alerta roja: desborde del río Piraí en El Torno',
         desc='El río superó la cota de desborde en Jorochito y Limoncito. Se espera que el agua llegue a Santa Rita y Junta Piraí en 3 a 5 horas.',
         instr='Evacúe de inmediato hacia la zona alta o al punto de encuentro de su comunidad. No cruce el río ni zonas inundadas.',
         area=TRAMO_EL_TORNO),
    dict(archivo='03-cancelacion-el-torno.xml', id='SENAMHI-SIM-2026-1202-003', sent='2026-12-02T09:00:00-04:00', tipo='Cancel',
         refs=f'{REMITENTE},SENAMHI-SIM-2026-1201-002,2026-12-01T13:00:00-04:00', sev='Minor', urg='Past', cer='Observed',
         efectivo='2026-12-02T09:00:00-04:00', expira='2026-12-31T23:00:00-04:00',
         titular='Fin de la alerta por crecida del río Piraí en El Torno',
         desc='El nivel del río bajó por debajo de la cota de alerta en todo el tramo.',
         instr='Puede volver a su vivienda con precaución. Revise daños antes de reconectar la energía.',
         area=TRAMO_EL_TORNO),
]

def xml(a):
    refs = f'\n  <references>{a["refs"]}</references>' if a['refs'] else ''
    return f'''<?xml version="1.0" encoding="UTF-8"?>
<alert xmlns="{NS}">
  <identifier>{a["id"]}</identifier>
  <sender>{REMITENTE}</sender>
  <sent>{a["sent"]}</sent>
  <status>Actual</status>
  <msgType>{a["tipo"]}</msgType>
  <scope>Public</scope>
  <note>{NOTA}</note>{refs}
  <info>
    <language>es-BO</language>
    <category>Met</category>
    <event>Crecida del río Piraí</event>
    <urgency>{a["urg"]}</urgency>
    <severity>{a["sev"]}</severity>
    <certainty>{a["cer"]}</certainty>
    <effective>{a["efectivo"]}</effective>
    <expires>{a["expira"]}</expires>
    <senderName>{NOMBRE}</senderName>
    <headline>{a["titular"]}</headline>
    <description>{a["desc"]}</description>
    <instruction>{a["instr"]}</instruction>
    <area>
      <areaDesc>{a["area"][0]}</areaDesc>
      <polygon>{a["area"][1]}</polygon>
    </area>
  </info>
</alert>
'''

for a in AVISOS:
    (OUT / a['archivo']).write_text(xml(a), encoding='utf-8')

def cap_json(identificador, sent, sev, urg, cer, titular, desc, instr, area, status='Actual', tipo='Alert', refs=None):
    return {
        'identifier': identificador, 'sender': REMITENTE, 'sent': sent, 'status': status, 'msgType': tipo, 'scope': 'Public',
        'note': NOTA, 'references': refs,
        'info': [{
            'language': 'es-BO', 'category': ['Met'], 'event': 'Crecida del río Piraí', 'urgency': urg, 'severity': sev, 'certainty': cer,
            'senderName': NOMBRE, 'headline': titular, 'description': desc, 'instruction': instr,
            'effective': sent, 'expires': '2026-12-31T23:00:00-04:00',
            'area': [{'areaDesc': area[0], 'polygon': [area[1]]}],
        }],
    }

(OUT / '04-alerta-amarilla-norte.json').write_text(json.dumps(cap_json(
    'SENAMHI-SIM-2026-1203-004', '2026-12-03T08:00:00-04:00', 'Moderate', 'Future', 'Possible',
    'Alerta amarilla por posible crecida del río Piraí en el norte integrado',
    'El caudal aguas arriba sigue alto. Podría subir el nivel en Montero, Portachuelo, General Saavedra y Mineros en las próximas 48 horas.',
    'Esté atento a los avisos oficiales. Aleje a niños y animales de la orilla y no saque áridos del cauce.', TRAMO_NORTE),
    ensure_ascii=False, indent=2), encoding='utf-8')

(OUT / '06-simulacro-porongo.json').write_text(json.dumps(cap_json(
    'SENAMHI-SIM-2026-1205-006', '2026-12-05T10:00:00-04:00', 'Severe', 'Expected', 'Likely',
    'SIMULACRO: alerta naranja en Porongo y La Guardia',
    'Ejercicio de evacuación coordinado con las comunidades ribereñas. No hay crecida real.',
    'Participe del simulacro: diríjase al punto de encuentro de su comunidad.', TRAMO_PORONGO, status='Exercise'),
    ensure_ascii=False, indent=2), encoding='utf-8')

# Negativos para las pruebas: no deben registrarse.
(OUT / '90-invalido-sin-info.xml').write_text(f'''<?xml version="1.0" encoding="UTF-8"?>
<alert xmlns="{NS}">
  <identifier>SENAMHI-SIM-INVALIDO-1</identifier>
  <sender>{REMITENTE}</sender>
  <sent>2026-12-01T07:00:00-04:00</sent>
  <status>Actual</status>
  <msgType>Alert</msgType>
  <scope>Public</scope>
</alert>
''', encoding='utf-8')
(OUT / '91-invalido-update-sin-referencia.json').write_text(json.dumps(cap_json(
    'SENAMHI-SIM-INVALIDO-2', '2026-12-01T07:00:00-04:00', 'Severe', 'Expected', 'Likely', 'x', 'x', 'x', TRAMO_NORTE, tipo='Update'),
    ensure_ascii=False, indent=2), encoding='utf-8')

# ---------- Excel ----------
CAMPOS = [
    ('identificador', 'Código único del aviso', 'SENAMHI-SIM-2026-1204-005'),
    ('remitente', 'Correo de quien emite', REMITENTE),
    ('enviado', 'Fecha y hora de emisión (AAAA-MM-DDTHH:MM-04:00)', '2026-12-04T06:30:00-04:00'),
    ('estado', 'Actual, Exercise (simulacro) o Test', 'Actual'),
    ('tipo', 'Alert, Update o Cancel', 'Alert'),
    ('referencias', 'Solo para Update o Cancel: remitente,identificador,enviado del aviso anterior', ''),
    ('nota', 'Observación', NOTA),
    ('categoria', 'Met (meteorológico) o Geo, Safety...', 'Met'),
    ('evento', 'Qué pasa', 'Crecida del río Piraí'),
    ('urgencia', 'Immediate, Expected, Future o Past', 'Immediate'),
    ('severidad', 'Extreme (roja), Severe (naranja), Moderate (amarilla) o Minor (verde)', 'Extreme'),
    ('certeza', 'Observed, Likely, Possible o Unlikely', 'Observed'),
    ('nombreRemitente', 'Institución', NOMBRE),
    ('titular', 'Titular corto', 'Alerta roja por crecida del río Piraí en Porongo y La Guardia'),
    ('descripcion', 'Qué se observó y qué se espera', 'El nivel del río en el puente de La Guardia subió 2 m en 6 horas. Se esperan desbordes en Villa Guadalupe, Sombrerito y San José de Potrerito.'),
    ('instruccion', 'Qué debe hacer la población', 'Evacúe hacia la zona alta o al punto de encuentro de su comunidad. No cruce el río ni zonas inundadas.'),
    ('vigenteDesde', 'Desde cuándo vale', '2026-12-04T06:30:00-04:00'),
    ('expira', 'Hasta cuándo vale', '2026-12-31T23:00:00-04:00'),
    ('area', 'Nombre del área (se puede repetir con su polígono)', TRAMO_PORONGO[0]),
    ('poligono', 'Polígono CAP: lat,lon separados por espacios, el primero igual al último', TRAMO_PORONGO[1]),
]

def libro(con_valores: bool, nombre: str):
    wb = Workbook()
    ws = wb.active
    ws.title = 'Aviso'
    ws.append(['Campo', 'Valor', 'Qué poner'])
    for c in ws[1]:
        c.font = Font(bold=True, color='FFFFFF'); c.fill = PatternFill('solid', fgColor='14213D')
    for campo, ayuda, valor in CAMPOS:
        ws.append([campo, valor if con_valores else '', ayuda])
    ws.column_dimensions['A'].width = 18; ws.column_dimensions['B'].width = 70; ws.column_dimensions['C'].width = 60
    for fila in ws.iter_rows(min_row=2):
        for c in fila: c.alignment = Alignment(wrap_text=True, vertical='top')
    ayuda = wb.create_sheet('Instrucciones')
    for linea in [
        'Plantilla de aviso del SAT Río Piraí (CAP 1.2).',
        'Llene la columna "Valor" de la hoja "Aviso". No cambie los nombres de la columna "Campo".',
        'Las filas "area" y "poligono" se pueden repetir en pares para varias áreas.',
        'Importe el archivo en Avisos > Importar CAP. El sistema lo valida y genera el XML CAP 1.2.',
    ]:
        ayuda.append([linea])
    ayuda.column_dimensions['A'].width = 110
    wb.save(OUT / nombre)

libro(True, '05-alerta-roja-porongo.xlsx')
libro(False, 'plantilla-aviso.xlsx')

# Excel sin la hoja "Aviso" (negativo).
wb = Workbook(); wb.active.title = 'Datos'; wb.active.append(['fecha', 'nivel_m']); wb.active.append(['2026-12-01', 3.2])
wb.save(OUT / '92-invalido-excel-sin-hoja.xlsx')
print('Ejemplos generados en', OUT)
