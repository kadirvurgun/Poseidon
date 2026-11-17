import json
from pathlib import Path
import tempfile
import unittest

from import_documents import import_set
from verify_sources import verify


class VerifySourceTests(unittest.TestCase):
    def test_byte_changes_and_tampered_or_duplicate_chunks_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'analiz/latest'
            source.mkdir(parents=True)
            original = source / 'README_FIRST.txt'
            original.write_text('Controlled policy', encoding='utf-8')
            import_set(source, root, baseline_id='test', copy_originals=False)
            (root / 'docs/sources/active.json').write_text(json.dumps({'baseline_id': 'test', 'source_root': 'analiz/latest', 'manifest': 'docs/sources/manifest.json', 'chunks': 'docs/sources/chunks.jsonl'}))
            self.assertEqual(verify(root)['mismatches'], 0)
            original.write_text('Changed', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'Source provenance'):
                verify(root)
            original.write_text('Controlled policy', encoding='utf-8')
            chunk_file = root / 'docs/sources/chunks.jsonl'
            data = chunk_file.read_text(encoding='utf-8')
            chunk_file.write_text(data + data, encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'duplicate'):
                verify(root)
            altered = json.loads(data)
            altered['text'] = 'Tampered'
            chunk_file.write_text(json.dumps(altered), encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'lineage'):
                verify(root)

    def test_sharded_chunk_lineage_is_verified_as_one_complete_set(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); source = root / 'analiz/latest'; source.mkdir(parents=True)
            (source / 'README_FIRST.txt').write_text('Controlled policy ' * 500, encoding='utf-8')
            result = import_set(source, root, baseline_id='sharded', copy_originals=False, chunk_shard_bytes=1024)
            (root / 'docs/sources/active.json').write_text(json.dumps({'baseline_id': 'sharded', 'source_root': 'analiz/latest',
                'manifest': 'docs/sources/manifest.json', 'chunks': [f'docs/sources/{name}' for name in result['chunk_files']]}))
            self.assertEqual(verify(root)['mismatches'], 0)
            (root / 'docs/sources' / result['chunk_files'][-1]).write_text('', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'Chunk count'):
                verify(root)
