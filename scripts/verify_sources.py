"""Verify byte provenance and chunk lineage for the active analysis baseline."""
from collections import Counter
import hashlib
import json
from pathlib import Path

from query_sources import ROOT, inside


def verify(root=ROOT):
    root = Path(root).resolve()
    active = json.loads((root / 'docs/sources/active.json').read_text(encoding='utf-8'))
    source_root = inside(root, active['source_root'])
    manifest = json.loads(inside(root, active['manifest']).read_text(encoding='utf-8'))
    records = manifest['files']
    by_path = {record['source_path']: record for record in records}
    if not records or len(by_path) != len(records) or len({r['source_id'] for r in records}) != len(records):
        raise ValueError('Empty or duplicate source provenance')
    for relative, record in by_path.items():
        path = inside(root, relative)
        if not path.is_relative_to(source_root) or hashlib.sha256(path.read_bytes()).hexdigest() != record['sha256']:
            raise ValueError('Source provenance mismatch: ' + relative)
    counts, seen = Counter(), set()
    with inside(root, active['chunks']).open(encoding='utf-8') as stream:
        for line in stream:
            chunk = json.loads(line)
            record = by_path.get(chunk['source_path'])
            content_hash = hashlib.sha256(chunk['text'].encode('utf-8')).hexdigest()
            identity = hashlib.sha256(f"{chunk['source_id']}:{chunk['locator']}:{content_hash}".encode('utf-8')).hexdigest()
            if record is None or chunk['source_sha256'] != record['sha256'] or chunk['source_id'] != record['source_id'] or chunk['source_class'] != record['source_class'] or chunk['content_sha256'] != content_hash or chunk['chunk_id'] != identity or identity in seen:
                raise ValueError('Chunk lineage mismatch or duplicate')
            counts[chunk['source_path']] += 1
            seen.add(identity)
    if any(counts[r['source_path']] != r['chunk_count'] for r in records):
        raise ValueError('Chunk count mismatch')
    return {'baseline': active['baseline_id'], 'files': len(records), 'chunks': len(seen), 'mismatches': 0}


if __name__ == '__main__':
    print(json.dumps(verify()))
