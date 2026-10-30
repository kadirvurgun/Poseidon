#!/usr/bin/env python3
"""Read canonical 11.01 XLSX tables for prototype validation; stdlib, read-only.
This helper does not recalculate formulas, modify or author a workbook.
"""
import json
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
ROOT = Path(__file__).resolve().parent.parent
source = next(ROOT.glob('03_REQUIREMENTS_REGULATORY/11.01*.xlsx')) if len(sys.argv) == 1 else Path(sys.argv[1])
with zipfile.ZipFile(source) as z:
    shared = []
    if 'xl/sharedStrings.xml' in z.namelist():
        shared = [''.join(t.itertext()) for t in ET.fromstring(z.read('xl/sharedStrings.xml'))]
    relationships = {r.attrib['Id']: r.attrib['Target'] for r in ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
    sheets = {}
    for sheet in ET.fromstring(z.read('xl/workbook.xml')).findall('s:sheets/s:sheet', NS):
        target = relationships[sheet.attrib['{' + REL + '}id']]
        if target.startswith('/'): target = target.lstrip('/')
        else: target = 'xl/' + target
        rows = []
        for row in ET.fromstring(z.read(target)).findall('s:sheetData/s:row', NS):
            values = []
            for cell in row:
                col = ''.join(c for c in cell.attrib['r'] if c.isalpha())
                index = 0
                for c in col: index = index * 26 + ord(c) - 64
                while len(values) < index: values.append(None)
                value = cell.find('s:v', NS)
                value = value.text if value is not None else None
                if cell.attrib.get('t') == 's': value = shared[int(value)] if value is not None else ''
                elif cell.attrib.get('t') == 'inlineStr': value = ''.join(cell.find('s:is', NS).itertext())
                elif value is not None:
                    try: value = float(value) if '.' in value else int(value)
                    except ValueError: pass
                values[index - 1] = value
            rows.append(values)
        sheets[sheet.attrib['name']] = rows
    fields = sheets['2316 Alan v46.3']
    screens = sheets['196 Ekran v46.3']
    assert fields[0][0] == 'Screen ID' and fields[0][3] == 'Field Key'
    assert screens[0][4] == 'Actions'
    mapping = {'k':3,'l':4,'u':5,'s':6,'m':7,'d':8,'o':9,'r':10,'v':12,'of':16,'e':18,'fr':20,'t':21}
    by_screen = {}
    for row in fields[1:]:
        if row and row[0]: by_screen.setdefault(row[0], []).append({key: row[i] if i < len(row) and row[i] is not None else '' for key, i in mapping.items()})
    metadata = {r[0]: {'fields':r[3], 'actions':r[4]} for r in screens[1:] if r and r[0]}
    print(json.dumps({'source':str(source.relative_to(ROOT)) if source.is_relative_to(ROOT) else str(source), 'fields':by_screen, 'screens':metadata, 'noImplicitDefaultKeys':sorted(r[0]+'.'+r[3] for r in fields[1:] if r and r[0] and ((r[8]=='No' and r[5]!='checkbox') or r[8]=='Prototype value if valid' or (r[6]=='Date/Time' and r[8]=='Yes')))}, ensure_ascii=False))
