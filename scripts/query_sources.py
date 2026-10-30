"""Bounded local FTS lookup of the active controlled-source chunks; no model calls."""
import argparse
from contextlib import closing
import hashlib
import json
import os
from pathlib import Path
import re
import sqlite3
import uuid

ROOT = Path(__file__).resolve().parents[1]


def inside(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError('Source path must stay inside the repository')
    return path


def query(text, *, root=ROOT, limit=4, include_references=False):
    if not 1 <= limit <= 20 or not text.strip() or len(text) > 500:
        raise ValueError('Supply a bounded nonempty query and limit 1..20')
    root = Path(root).resolve()
    active = json.loads((root / 'docs/sources/active.json').read_text(encoding='utf-8'))
    baseline = active['baseline_id']
    if not re.fullmatch(r'[A-Za-z0-9._-]+', baseline):
        raise ValueError('Invalid baseline ID')
    chunks = inside(root, active['chunks'])
    fingerprint = hashlib.sha256(chunks.read_bytes()).hexdigest()
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
                with chunks.open(encoding='utf-8') as stream:
                    for line in stream:
                        c = json.loads(line)
                        db.execute('INSERT INTO source_fts VALUES (?,?,?,?,?)', (c['source_path'], c['locator'], c['source_sha256'], c['source_class'], c['text']))
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
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('query')
    parser.add_argument('--limit', type=int, default=4)
    parser.add_argument('--include-references', action='store_true')
    args = parser.parse_args()
    print(json.dumps(query(args.query, limit=args.limit, include_references=args.include_references), ensure_ascii=False, indent=2))
