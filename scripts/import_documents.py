"""Import an immutable local controlled baseline and produce traceable text chunks.

Uses only Python's standard library. No model, network or embedding calls.
"""

import argparse
import hashlib
from html.parser import HTMLParser
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


class SourceHtmlParser(HTMLParser):
    """Extract visible prototype copy and bounded script source without CSS noise."""
    block_tags = {'address', 'article', 'aside', 'blockquote', 'button', 'caption', 'dd', 'div',
                  'dl', 'dt', 'fieldset', 'figcaption', 'figure', 'footer', 'form', 'h1', 'h2',
                  'h3', 'h4', 'h5', 'h6', 'header', 'label', 'legend', 'li', 'main', 'nav', 'ol',
                  'option', 'p', 'section', 'summary', 'table', 'td', 'th', 'tr', 'ul'}

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.hidden = 0
        self.script = 0
        self.visible = []
        self.scripts = []
        self._current = []

    def _flush(self):
        text = re.sub(r'\s+', ' ', ' '.join(self._current)).strip()
        if text:
            self.visible.append(text)
        self._current = []

    def handle_starttag(self, tag, attrs):
        tag = tag.lower()
        if tag in self.block_tags:
            self._flush()
        if tag in {'style', 'template', 'noscript'}:
            self.hidden += 1
        elif tag == 'script' and self.hidden == 0:
            self.script += 1
            self.scripts.append([])

    def handle_endtag(self, tag):
        tag = tag.lower()
        if tag == 'script' and self.script:
            self.script -= 1
        elif tag in {'style', 'template', 'noscript'} and self.hidden:
            self.hidden -= 1
        if tag in self.block_tags:
            self._flush()

    def handle_data(self, data):
        if self.hidden:
            return
        if self.script:
            self.scripts[-1].append(data)
        elif data.strip():
            self._current.append(data)

    def finish(self):
        self._flush()


def bounded_text(value, maximum=4000):
    """Yield stable nonempty character windows, preferring whitespace boundaries."""
    start = 0
    while start < len(value):
        end = min(start + maximum, len(value))
        if end < len(value):
            boundary = value.rfind(' ', start + maximum // 2, end)
            if boundary > start:
                end = boundary
        text = value[start:end].strip()
        if text:
            yield start, end, text
        start = end


def html_sections(path):
    parser = SourceHtmlParser()
    parser.feed(path.read_text(encoding='utf-8-sig'))
    parser.close()
    parser.finish()
    visible = '\n'.join(parser.visible)
    for start, end, text in bounded_text(visible):
        yield f'html-visible:chars:{start + 1}-{end}', text
    for index, parts in enumerate(parser.scripts, 1):
        script = ''.join(parts)
        for start, end, text in bounded_text(script):
            yield f'html-script:{index}/chars:{start + 1}-{end}', text


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
    elif path.suffix.lower() in {'.html', '.htm'}:
        yield from html_sections(path)
    elif path.suffix.lower() in {'.md', '.txt', '.json', '.yaml', '.yml', '.sh'}:
        lines = path.read_text(encoding='utf-8-sig').splitlines()
        for index in range(0, len(lines), 40):
            text = '\n'.join(lines[index:index + 40]).strip()
            if text:
                yield f'lines:{index + 1}-{min(index + 40, len(lines))}', text


def import_set(source, repo_root=ROOT, *, baseline_id='poseidon-v46.3',
               output_relative='docs/sources', copy_originals=True, chunk_shard_bytes=None):
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
                'parser_version': 2, 'files': records}
    lines = [json.dumps(c, ensure_ascii=False) + '\n' for c in chunks]
    chunk_files = []
    if chunk_shard_bytes is not None:
        if chunk_shard_bytes < 1024:
            raise ValueError('Chunk shard size must be at least 1024 bytes')
        shard, size = [], 0
        for line in lines:
            encoded = len(line.encode('utf-8'))
            if shard and size + encoded > chunk_shard_bytes:
                chunk_files.append(shard); shard, size = [], 0
            shard.append(line); size += encoded
        if shard:
            chunk_files.append(shard)
        names = [f'chunks-{index:04d}.jsonl' for index in range(1, len(chunk_files) + 1)]
        for name, shard in zip(names, chunk_files):
            (output / name).write_text(''.join(shard), encoding='utf-8')
        manifest['chunk_files'] = names
    else:
        names = ['chunks.jsonl']
        (output / names[0]).write_text(''.join(lines), encoding='utf-8')
        manifest['chunk_files'] = names
    (output / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return {'files': len(records), 'text_files': len(texts), 'chunks': len(chunks), 'chunk_files': names}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--baseline-id', default='poseidon-v46.3')
    parser.add_argument('--output-relative', default='docs/sources')
    parser.add_argument('--in-place', action='store_true', help='Index original files already inside this repo without copying them')
    parser.add_argument('--chunk-shard-bytes', type=int, help='Write bounded numbered JSONL shards instead of one chunk file')
    arguments = parser.parse_args()
    print(json.dumps(import_set(arguments.source, baseline_id=arguments.baseline_id,
                                output_relative=arguments.output_relative,
                                copy_originals=not arguments.in_place,
                                chunk_shard_bytes=arguments.chunk_shard_bytes), ensure_ascii=False))
