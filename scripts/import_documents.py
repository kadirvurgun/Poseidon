"""Import an immutable local controlled baseline and produce traceable text chunks.

Uses only Python's standard library. No model, network or embedding calls.
"""

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[1]
W = '{http://schemas.openxmlformats.org/wordprocessingml/2006/main}'
S = '{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'
R = '{http://schemas.openxmlformats.org/officeDocument/2006/relationships}'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def paragraphs(path):
    """Return actual document paragraph locators, including table paragraphs."""
    with ZipFile(path) as archive:
        root = ET.fromstring(archive.read('word/document.xml'))
        for index, paragraph in enumerate(root.iter(W + 'p'), 1):
            text = ''.join(node.text or '' for node in paragraph.iter(W + 't')).strip()
            if text:
                yield f'paragraph:{index}', text


def spreadsheet_rows(path):
    """Extract cached values, preserving sheet names and cell references."""
    with ZipFile(path) as archive:
        shared = []
        if 'xl/sharedStrings.xml' in archive.namelist():
            root = ET.fromstring(archive.read('xl/sharedStrings.xml'))
            shared = [''.join(n.text or '' for n in item.iter(S + 't'))
                      for item in root.findall(S + 'si')]
        relationships = ET.fromstring(archive.read('xl/_rels/workbook.xml.rels'))
        targets = {n.attrib['Id']: n.attrib['Target'] for n in relationships}
        workbook = ET.fromstring(archive.read('xl/workbook.xml'))
        for sheet in workbook.find(S + 'sheets'):
            target = targets[sheet.attrib[R + 'id']]
            target = target.lstrip('/') if target.startswith('/') else 'xl/' + target
            root = ET.fromstring(archive.read(target))
            for row in root.iter(S + 'row'):
                cells = []
                for cell in row.findall(S + 'c'):
                    value = cell.find(S + 'v')
                    value = value.text or '' if value is not None else ''
                    if cell.get('t') == 's':
                        value = shared[int(value)] if value else ''
                    elif cell.get('t') == 'inlineStr':
                        value = ''.join(n.text or '' for n in cell.iter(S + 't'))
                    if value:
                        cells.append(f'{cell.get("r")}: {value}')
                if cells:
                    yield f'sheet:{sheet.get("name")}/row:{row.get("r")}', ' | '.join(cells)


def source_class(relative):
    if any(part.startswith('26.01_POSEIDON_Core') for part in relative.parts):
        return 'embedded-source-reference'
    if relative.parts[0] == '07_TEST_PILOT_RELEASE' and re.match(r'(?:2[7-9]|[3-9][0-9])\.\d+', relative.name):
        return 'historical-validation-reference'
    if relative.parts[0] == '90_SOURCE_RESEARCH':
        return 'reference-only'
    if relative.parts[0] == '99_ARCHIVE':
        return 'archive-only'
    return 'candidate-unverified'


def extract(path):
    if path.suffix.lower() == '.docx':
        yield from paragraphs(path)
    elif path.suffix.lower() == '.xlsx':
        yield from spreadsheet_rows(path)
    elif path.suffix.lower() in {'.md', '.txt', '.json', '.yaml', '.yml', '.sh'}:
        lines = path.read_text(encoding='utf-8-sig').splitlines()
        for index in range(0, len(lines), 40):
            text = '\n'.join(lines[index:index + 40]).strip()
            if text:
                yield f'lines:{index + 1}-{min(index + 40, len(lines))}', text


def import_set(source, repo_root=ROOT, *, baseline_id='poseidon-v46.3',
               output_relative='docs/sources', copy_originals=True):
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]*', baseline_id):
        raise ValueError('Invalid baseline ID')
    repo_root = repo_root.resolve()
    source = source.resolve(strict=True)
    if not source.is_dir() or not (source / 'README_FIRST.txt').is_file():
        raise ValueError('Expected the controlled-set folder containing README_FIRST.txt')
    files = sorted(p for p in source.rglob('*') if p.is_file())
    originals = repo_root / 'sources' / baseline_id if copy_originals else source
    output = (repo_root / output_relative).resolve()
    if not output.is_relative_to(repo_root) or output == repo_root or output.is_relative_to(source) or output.is_relative_to(originals):
        raise ValueError('Output must be inside the repo and outside the source set')
    if not copy_originals and not source.is_relative_to(repo_root):
        raise ValueError('In-place sources must be inside the repo')
    # Re-importing an existing index may not silently rewrite its provenance.
    prior_manifest = output / 'manifest.json'
    if prior_manifest.exists():
        prior = json.loads(prior_manifest.read_text(encoding='utf-8'))
        expected = {record['source_path']: record['sha256'] for record in prior['files']}
        current = {(originals / path.relative_to(source)).relative_to(repo_root).as_posix(): digest(path.read_bytes()) for path in files}
        if expected != current:
            raise ValueError('Baseline changed; use a new version directory')
    # Validate the whole baseline before copying or replacing generated files.
    for path in files:
        destination = originals / path.relative_to(source)
        if destination.exists() and digest(destination.read_bytes()) != digest(path.read_bytes()):
            raise ValueError(f'Baseline changed; use a new version directory: {path.name}')
    records, chunks = [], []
    texts = []
    for path in files:
        relative = path.relative_to(source)
        data = path.read_bytes()
        source_path = (originals / relative).relative_to(repo_root).as_posix()
        source_id = ('v46.3' if baseline_id == 'poseidon-v46.3' else baseline_id) + ':' + relative.as_posix()
        source_sha = digest(data)
        destination = originals / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        if not destination.exists():
            shutil.copy2(path, destination)
        entries = list(extract(path))
        text_path = None
        if entries:
            text_relative = Path('text') / relative.with_suffix(relative.suffix + '.md')
            text_path = (output.relative_to(repo_root) / text_relative).as_posix()
            text = f'# {path.name}\n\nSource: {source_path}\nSHA256: {source_sha}\n\n'
            for locator, content in entries:
                text += f'## {locator}\n\n{content}\n\n'
                content_sha = digest(content.encode('utf-8'))
                chunks.append({
                    'chunk_id': digest(f'{source_id}:{locator}:{content_sha}'.encode('utf-8')),
                    'source_id': source_id, 'source_path': source_path,
                    'source_sha256': source_sha, 'source_class': source_class(relative),
                    'locator': locator, 'text': content, 'content_sha256': content_sha,
                })
            texts.append((output / text_relative, text))
        match = re.match(r'\d+\.\d+', path.name)
        records.append({
            'source_id': source_id, 'document_number': match.group() if match else None,
            'source_path': source_path, 'sha256': source_sha, 'bytes': len(data),
            'source_class': source_class(relative), 'text_path': text_path,
            'chunk_count': len(entries),
            'extraction': 'text-only' if entries else 'original-only',
        })
    output.mkdir(parents=True, exist_ok=True)
    for path, text in texts:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text.rstrip() + '\n', encoding='utf-8')
    manifest = {'baseline': 'POSEIDON v46.3 CONTROLLED CANDIDATE', 'baseline_id': baseline_id,
                'parser_version': 1, 'files': records}
    (output / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (output / 'chunks.jsonl').write_text(''.join(json.dumps(c, ensure_ascii=False) + '\n' for c in chunks), encoding='utf-8')
    return {'files': len(records), 'text_files': len(texts), 'chunks': len(chunks)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--baseline-id', default='poseidon-v46.3')
    parser.add_argument('--output-relative', default='docs/sources')
    parser.add_argument('--in-place', action='store_true', help='Index original files already inside this repo without copying them')
    arguments = parser.parse_args()
    print(json.dumps(import_set(arguments.source, baseline_id=arguments.baseline_id,
                                output_relative=arguments.output_relative,
                                copy_originals=not arguments.in_place), ensure_ascii=False))
