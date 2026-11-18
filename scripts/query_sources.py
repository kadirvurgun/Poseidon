"""Bounded local FTS lookup of the active controlled-source chunks; no model calls."""
import argparse
from contextlib import closing
import hashlib
from io import BytesIO
import json
import os
from pathlib import Path
import re
import sqlite3
import sys
import uuid

from import_documents import paragraphs

ROOT = Path(__file__).resolve().parents[1]


def inside(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError('Source path must stay inside the repository')
    return path


def chunk_paths(root, active):
    configured = active['chunks']
    values = [configured] if isinstance(configured, str) else configured
    if not isinstance(values, list) or not values or not all(isinstance(value, str) for value in values):
        raise ValueError('Active chunks must be a path or nonempty path list')
    paths = [inside(root, value) for value in values]
    if len(set(paths)) != len(paths):
        raise ValueError('Active chunk paths must be unique')
    return paths


def supplement(root, active):
    """Pin original DOCX bytes before trusting an overlay, including on cache hits.

    The overlay is a requirements/planning source, never evidence of product,
    legal or expert acceptance. HTML prototypes are never executed or indexed.
    """
    config = active.get('planning_supplement')
    if not config or not config.get('default_query_sources_includes_supplement'):
        return [], b''
    raw = inside(root, config['manifest']).read_bytes()
    manifest = json.loads(raw)
    if manifest['baseline_id'] != config['baseline_id'] or manifest['source_root'] != config['source_root']:
        raise ValueError('Supplement baseline mismatch')
    source_root = inside(root, config['source_root'])
    records = manifest['files']
    if not records or len({r['source_path'] for r in records}) != len(records) or len({r['source_id'] for r in records}) != len(records):
        raise ValueError('Empty or duplicate supplement source')
    candidates, numbers = [], set()
    for record in records:
        path = inside(root, record['source_path'])
        if not path.is_relative_to(source_root):
            raise ValueError('Supplement source outside controlled root')
        data = path.read_bytes()
        if len(data) != record['bytes'] or hashlib.sha256(data).hexdigest() != record['sha256']:
            raise ValueError('Supplement source provenance mismatch')
        if record['source_class'] == 'prototype-reference-only':
            continue
        number = record['document_number']
        if record['source_class'] != 'candidate-unverified' or path.suffix.lower() != '.docx' or not re.fullmatch(r'\d{2}\.\d{2}', number or '') or number in numbers:
            raise ValueError('Unsupported or ambiguous supplement document')
        actual_number = re.match(r'\d{2}\.\d{2}', path.name)
        if not actual_number or actual_number.group() != number:
            raise ValueError('Supplement document identity mismatch')
        numbers.add(number)
        candidates.append({**record, '_validated_bytes': data})
    return candidates, raw


def indexed_rows(chunk_contents, overlay):
    superseded = {record['document_number'] for record in overlay}
    overlay_paths = {record['source_path'] for record in overlay}
    for chunk_bytes in chunk_contents:
        with BytesIO(chunk_bytes) as stream:
            for line in stream:
                c = json.loads(line)
                if c['source_path'] in overlay_paths:
                    # The overlay re-extracts this exact pinned original; never index it twice.
                    continue
                number = re.match(r'\d{2}\.\d{2}', Path(c['source_path']).name)
                kind = c['source_class']
                if kind == 'candidate-unverified' and number and number.group() in superseded:
                    kind = 'superseded-candidate-reference'
                yield c['source_path'], c['locator'], c['source_sha256'], kind, c['text']
    for record in overlay:
        count = 0
        # Extract the exact bytes already hashed, never reopen mutable source paths.
        for locator, text in paragraphs(BytesIO(record['_validated_bytes'])):
            count += 1
            yield record['source_path'], locator, record['sha256'], record['source_class'], text
        if count != record['paragraph_count']:
            raise ValueError('Supplement paragraph lineage mismatch')


def query(text, *, root=ROOT, limit=4, include_references=False):
    if not 1 <= limit <= 20 or not text.strip() or len(text) > 500:
        raise ValueError('Supply a bounded nonempty query and limit 1..20')
    root = Path(root).resolve()
    active = json.loads((root / 'docs/sources/active.json').read_text(encoding='utf-8'))
    baseline = active['baseline_id']
    if not re.fullmatch(r'[A-Za-z0-9._-]+', baseline):
        raise ValueError('Invalid baseline ID')
    chunks = chunk_paths(root, active)
    overlay, manifest_bytes = supplement(root, active)
    chunk_contents = [path.read_bytes() for path in chunks]
    fingerprint_hash = hashlib.sha256(b'fts-overlay-v1\0')
    for path, content in zip(chunks, chunk_contents):
        fingerprint_hash.update(path.as_posix().encode('utf-8')); fingerprint_hash.update(b'\0')
        fingerprint_hash.update(content); fingerprint_hash.update(b'\0')
    fingerprint_hash.update(manifest_bytes)
    fingerprint = fingerprint_hash.hexdigest()
    cache = root / '.runtime/source-index' / (baseline + '.sqlite3')
    cache.parent.mkdir(parents=True, exist_ok=True)
    rebuild = True
    if cache.exists():
        with closing(sqlite3.connect(cache)) as db:
            rebuild = db.execute('SELECT value FROM metadata WHERE key = ?', ('sha256',)).fetchone() != (fingerprint,)
    if rebuild:
        temporary = cache.with_suffix('.' + uuid.uuid4().hex + '.tmp')
        try:
            with closing(sqlite3.connect(temporary)) as db:
                db.execute('CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL)')
                db.execute('CREATE VIRTUAL TABLE source_fts USING fts5(source_path UNINDEXED, locator UNINDEXED, sha256 UNINDEXED, source_class UNINDEXED, text)')
                db.executemany('INSERT INTO source_fts VALUES (?,?,?,?,?)', indexed_rows(chunk_contents, overlay))
                db.execute('INSERT INTO metadata VALUES (?,?)', ('sha256', fingerprint))
                db.commit()
            os.replace(temporary, cache)
        finally:
            temporary.unlink(missing_ok=True)
    # User text is quoted as one FTS phrase, never interpreted as SQL/FTS syntax.
    phrase = '"' + text.replace('"', '""') + '"'
    condition = '' if include_references else " AND source_class = 'candidate-unverified'"
    with closing(sqlite3.connect(cache)) as db:
        rows = db.execute('SELECT source_path, locator, sha256, source_class, text FROM source_fts WHERE source_fts MATCH ?' + condition + ' ORDER BY rank LIMIT ?', (phrase, limit)).fetchall()
    return [{'source_path': row[0], 'locator': row[1], 'sha256': row[2], 'source_class': row[3], 'text': row[4][:2000], 'truncated': len(row[4]) > 2000} for row in rows]


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('query')
    parser.add_argument('--limit', type=int, default=4)
    parser.add_argument('--include-references', action='store_true')
    args = parser.parse_args()
    print(json.dumps(query(args.query, limit=args.limit, include_references=args.include_references), ensure_ascii=False, indent=2))
