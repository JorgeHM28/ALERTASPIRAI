#!/usr/bin/env python3
"""Genera un informe honesto desde el TRX real y una captura real de Swagger."""
from pathlib import Path
import xml.etree.ElementTree as ET
from reportlab.platypus import SimpleDocTemplate,Paragraph,Spacer,Table,TableStyle,Image,PageBreak
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet,ParagraphStyle
from reportlab.lib.pagesizes import A4
root=Path(__file__).resolve().parents[1]
trx=root/'tests/SistemaAlertas.Tests/TestResults/verificacion.trx'
tree=ET.parse(trx);ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
c=tree.find('.//t:Counters',ns).attrib
c['notExecuted']=str(sum(r.get('outcome')=='NotExecuted' for r in tree.findall('.//t:UnitTestResult',ns)))
if int(c['failed']):raise SystemExit('Hay pruebas fallidas; no se genera un informe de aprobación.')
out=root/'docs/evidencias/Verificacion-parcial.pdf'
styles=getSampleStyleSheet();styles.add(ParagraphStyle(name='TituloPirai',fontName='Helvetica-Bold',fontSize=27,leading=31,textColor=colors.HexColor('#164c41'),spaceAfter=16))
styles['BodyText'].fontSize=10;styles['BodyText'].leading=15
story=[]
def p(t,style='BodyText'):story.append(Paragraph(t,styles[style]));story.append(Spacer(1,9))
p('SAT Río Piraí','TituloPirai');p('Evidencias parciales de la migración MVC y SQL Server','Heading2')
p('Validación local en macOS. Rama: pablo/migracion-aspnet-sqlserver. Base: feb34a1. Este documento no acredita todavía las pruebas de persistencia en SQL Server y no reemplaza el PDF final solicitado en Teams.')
p('Resultados obtenidos','Heading2')
tabla=Table([['Comprobación','Resultado'],['Pruebas aprobadas',c['passed']],['Pruebas fallidas',c['failed']],['Pruebas omitidas',c['notExecuted']],['SDK utilizado','.NET 10.0.404'],['Migración SQL Server','Generada con EF Core 10.0.14'],['Modelo y snapshot','Sin diferencias pendientes'],['Swagger y login Razor','HTTP 200; observados en navegador'],['API sin Bearer','HTTP 401 verificado'],['Login sin antifalsificación','HTTP 400 verificado']],colWidths=[310,175])
tabla.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#164c41')),('TEXTCOLOR',(0,0),(-1,0),colors.white),('FONTNAME',(0,0),(-1,0),'Helvetica-Bold'),('FONTSIZE',(0,0),(-1,-1),9),('BOTTOMPADDING',(0,0),(-1,-1),9),('TOPPADDING',(0,0),(-1,-1),9),('GRID',(0,0),(-1,-1),.3,colors.HexColor('#d8e1d9'))]));story.append(tabla);story.append(Spacer(1,16))
p('Prueba omitida y condiciones pendientes','Heading2')
p('La prueba CrudErroresGeografiaMvcYPersistenciaTrasReiniciarHost se omite cuando no está configurada ALERTAS_TEST_SQLSERVER. No se sustituyó por SQLite ni por una base en memoria. La instancia indicada por el usuario es localhost\\SQLEXPRESS, con autenticación Windows.')
p('Falta aplicar la migración y ejecutar CRUD, errores 400/404 y GET tras reinicio contra esa instancia; importar y conciliar la base que conserva un integrante; confirmar integrantes y usuarios GitHub. No se fabricaron resultados ni capturas para esos pasos.')
p('La guía de ejecución y capturas está en docs/evidencias/GUIA.md. El TRX se obtiene con dotnet test --logger "trx;LogFileName=verificacion.trx". La utilidad de traslado está compilada; no se ha ejecutado contra registros reales.')
story.append(PageBreak());p('Swagger UI en ejecución','TituloPirai');p('Captura real del servidor local: GET /swagger/index.html, HTTP 200. Se observan operaciones REST y el botón Authorize. La captura demuestra disponibilidad de la documentación, no ejecución exitosa del CRUD contra SQL Server.')
img=Image(str(root/'docs/evidencias/parciales/swagger-local.jpg'));factor=min(480/img.imageWidth,610/img.imageHeight);img.drawWidth=img.imageWidth*factor;img.drawHeight=img.imageHeight*factor;story.append(img)
def pie(canvas,doc):canvas.setFont('Helvetica',8);canvas.setFillColor(colors.HexColor('#45635a'));canvas.drawString(42,25,'ALERTASPIRAI - Verificación parcial, no entrega final');canvas.drawRightString(A4[0]-42,25,str(doc.page))
SimpleDocTemplate(str(out),pagesize=A4,rightMargin=42,leftMargin=42,topMargin=38,bottomMargin=42).build(story,onFirstPage=pie,onLaterPages=pie)
print(out)
