import json
from pathlib import Path
import tempfile
import unittest

from query_sources import query


class QuerySourceTests(unittest.TestCase):
    def test_lookup_is_bounded_traceable_excludes_reference_and_refreshes_changed_cache(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sources = root / 'docs/sources'
            sources.mkdir(parents=True)
            (sources / 'active.json').write_text(json.dumps({'baseline_id': 'test', 'chunks': 'docs/sources/chunks.jsonl'}))
            chunk_file = sources / 'chunks.jsonl'
            records = [{'source_path': 'analiz/rule.txt', 'locator': 'lines:1-2', 'source_sha256': 'trace', 'source_class': kind, 'text': 'POL-DEFAULT-01 draft'} for kind in ['candidate-unverified', 'reference-only', 'embedded-source-reference']]
            chunk_file.write_text('\n'.join(json.dumps(c) for c in records), encoding='utf-8')
            found = query('POL-DEFAULT-01', root=root, limit=1)
            self.assertEqual(len(found), 1)
            self.assertEqual(found[0]['locator'], 'lines:1-2')
            self.assertEqual(found[0]['sha256'], 'trace')
            self.assertEqual(found[0]['source_class'], 'candidate-unverified')
            self.assertEqual(len(query('draft', root=root, include_references=True)), 3)
            chunk_file.write_text('', encoding='utf-8')
            self.assertEqual(query('draft', root=root), [])

    def test_invalid_queries_and_escape_paths_are_rejected(self):
        for text, limit in [('', 4), ('a', 0), ('a', 21), ('a' * 501, 1)]:
            with self.assertRaises(ValueError):
                query(text, limit=limit)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'docs/sources').mkdir(parents=True)
            (root / 'docs/sources/active.json').write_text(json.dumps({'baseline_id': 'test', 'chunks': '../outside'}))
            with self.assertRaises(ValueError):
                query('draft', root=root)
