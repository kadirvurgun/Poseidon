import json
import hashlib
from pathlib import Path
import tempfile
import unittest
from xml.sax.saxutils import escape
from zipfile import ZipFile

from query_sources import query


class QuerySourceTests(unittest.TestCase):
    def overlay_fixture(self, root):
        sources = root / 'docs/sources'
        sources.mkdir(parents=True)
        controlled = root / 'analiz/new'
        controlled.mkdir(parents=True)
        document = controlled / '17.01_New.docx'
        with ZipFile(document, 'w') as archive:
            archive.writestr('word/document.xml',
                '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>'
                '<w:p><w:r><w:t>' + escape('shared source phrase new owner') + '</w:t></w:r></w:p>'
                '</w:body></w:document>')
        prototype = controlled / 'prototype.html'
        prototype.write_text('<script>shared source phrase must never execute or index</script>', encoding='utf-8')
        files = []
        for path, number, kind, count in [(document, '17.01', 'candidate-unverified', 1), (prototype, None, 'prototype-reference-only', 0)]:
            data = path.read_bytes()
            files.append({'source_id': 'new:' + path.name, 'source_path': path.relative_to(root).as_posix(),
                'document_number': number, 'source_class': kind, 'sha256': hashlib.sha256(data).hexdigest(),
                'bytes': len(data), 'paragraph_count': count})
        manifest = {'baseline_id': 'new', 'source_root': 'analiz/new', 'files': files}
        manifest_path = sources / 'new.json'
        manifest_path.write_text(json.dumps(manifest), encoding='utf-8')
        chunks = sources / 'chunks.jsonl'
        chunks.write_text(json.dumps({'source_path': 'analiz/old/17.01_Old.docx', 'locator': 'paragraph:1',
            'source_sha256': 'old-hash', 'source_class': 'candidate-unverified', 'text': 'shared source phrase old owner'}) + '\n', encoding='utf-8')
        (sources / 'active.json').write_text(json.dumps({'baseline_id': 'old', 'chunks': 'docs/sources/chunks.jsonl',
            'planning_supplement': {'baseline_id': 'new', 'source_root': 'analiz/new',
                'manifest': 'docs/sources/new.json', 'default_query_sources_includes_supplement': True}}), encoding='utf-8')
        return document, manifest_path, manifest, chunks

    def test_overlay_prefers_new_owner_retains_superseded_reference_and_never_indexes_html(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            document, _, _, chunks = self.overlay_fixture(root)
            original = chunks.read_bytes()
            found = query('shared source phrase', root=root)
            self.assertEqual(len(found), 1)
            self.assertEqual(found[0]['source_path'], 'analiz/new/17.01_New.docx')
            self.assertEqual(found[0]['locator'], 'paragraph:1')
            self.assertEqual(found[0]['sha256'], hashlib.sha256(document.read_bytes()).hexdigest())
            both = query('shared source phrase', root=root, include_references=True)
            self.assertEqual(len(both), 2)
            self.assertEqual({r['source_class'] for r in both}, {'candidate-unverified', 'superseded-candidate-reference'})
            self.assertEqual(chunks.read_bytes(), original)

    def test_changed_original_fails_even_with_a_previously_built_cache(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            document, _, _, _ = self.overlay_fixture(root)
            query('shared source phrase', root=root)
            document.write_bytes(document.read_bytes() + b'changed')
            with self.assertRaisesRegex(ValueError, 'provenance mismatch'):
                query('shared source phrase', root=root)

    def test_failed_overlay_rebuild_preserves_last_cache_and_no_partial_rows_escape(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _, manifest_path, manifest, _ = self.overlay_fixture(root)
            query('shared source phrase', root=root)
            cache = root / '.runtime/source-index/old.sqlite3'
            original = cache.read_bytes()
            manifest['files'][0]['paragraph_count'] = 2
            manifest_path.write_text(json.dumps(manifest), encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'paragraph lineage mismatch'):
                query('shared source phrase', root=root)
            self.assertEqual(cache.read_bytes(), original)
            self.assertEqual(list(cache.parent.glob('*.tmp')), [])

    def test_overlay_rejects_identity_alias_and_source_escape(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            _, manifest_path, manifest, _ = self.overlay_fixture(root)
            manifest['files'][0]['document_number'] = '50.01'
            manifest_path.write_text(json.dumps(manifest), encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'identity mismatch'):
                query('shared source phrase', root=root)
            manifest['files'][0]['source_path'] = '../outside.docx'
            manifest_path.write_text(json.dumps(manifest), encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'inside the repository'):
                query('shared source phrase', root=root)

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

    def test_lookup_reads_every_unique_configured_chunk_shard(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); sources = root / 'docs/sources'; sources.mkdir(parents=True)
            paths = ['docs/sources/chunks-0001.jsonl', 'docs/sources/chunks-0002.jsonl']
            (sources / 'active.json').write_text(json.dumps({'baseline_id': 'sharded', 'chunks': paths}))
            for index, relative in enumerate(paths, 1):
                (root / relative).write_text(json.dumps({'source_path': f'analiz/{index}.txt', 'locator': 'lines:1-1',
                    'source_sha256': str(index), 'source_class': 'candidate-unverified', 'text': f'controlled shard result {index}'}) + '\n')
            found = query('controlled shard result', root=root, limit=4)
            self.assertEqual({row['sha256'] for row in found}, {'1', '2'})
            (sources / 'active.json').write_text(json.dumps({'baseline_id': 'sharded', 'chunks': [paths[0], paths[0]]}))
            with self.assertRaisesRegex(ValueError, 'unique'):
                query('controlled', root=root)

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
