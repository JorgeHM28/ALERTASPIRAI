#!/usr/bin/env python3
"""Métrica reproducible de líneas físicas; no es una fórmula oficial de evaluación."""
from pathlib import Path
from collections import Counter
import json
root=Path(__file__).resolve().parents[1]
counts=Counter()
for p in (root/'src').rglob('*'):
    if not p.is_file() or any(x.lower() in {'bin','obj','lib','migraciones'} for x in p.parts): continue
    if p.suffix in {'.cs','.cshtml','.js','.ts','.tsx','.css'}:
        counts[p.suffix]+=len(p.read_text(encoding='utf-8-sig').splitlines())
total=sum(counts.values())
print(json.dumps({'lineas_fisicas':dict(counts),'total':total,'porcentaje_csharp':round(100*counts['.cs']/total,2),'porcentaje_csharp_y_razor':round(100*(counts['.cs']+counts['.cshtml'])/total,2),'excluye':'tests, migraciones, terceros, bin, obj y herramientas de traslado'},ensure_ascii=False,indent=2))
