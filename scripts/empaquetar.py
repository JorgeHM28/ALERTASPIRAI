#!/usr/bin/env python3
from pathlib import Path
import argparse,zipfile,subprocess
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--final',action='store_true');args=parser.parse_args()
if args.final:
    if not (root/'docs/evidencias/Evidencias.pdf').is_file():parser.error('Falta docs/evidencias/Evidencias.pdf con pruebas SQL Server reales.')
    if 'Pendiente de confirmar' in (root/'README.md').read_text():parser.error('Completar integrantes y usuarios GitHub antes de la entrega final.')
blocked={'bin','obj','.vs','.git','node_modules','testresults','entrega','__pycache__','.idea'}
files=[]
# Git respects exclusions for local secrets; untracked deliverable files are included.
if (root/'.git').exists():
    paths=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','-z'],cwd=root).decode().split('\0')
else:
    paths=[str(p.relative_to(root)) for p in root.rglob('*') if p.is_file()]
for name in sorted(set(paths)):
    p=root/name
    if not name or not p.is_file() or p.is_symlink():continue
    if any(x.lower() in blocked for x in Path(name).parts):continue
    if p.name.startswith('.env') or p.name in {'secrets.json','appsettings.Local.json'}:continue
    if p.name in {'.DS_Store','.env'} or p.suffix.lower() in {'.user','.suo','.bak','.dump','.zip'} or p.name.endswith('.transfer.json'):continue
    files.append((p,name))
out=root/'entrega';out.mkdir(exist_ok=True)
zip_path=out/('ALERTASPIRAI-Entrega-Final.zip' if args.final else 'ALERTASPIRAI-MVC-SQLServer-Traslado.zip')
with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED) as z:
    for p,name in files:z.write(p,'ALERTASPIRAI/'+name)
with zipfile.ZipFile(zip_path) as z:
    assert z.testzip() is None
    assert any(n.endswith('SqlServerInicial.cs') for n in z.namelist())
print(f'{zip_path}\nArchivos: {len(files)}\nTipo: {"final (revisar evidencias)" if args.final else "traslado; cierre SQL Server pendiente"}')
