"""Behavioral checks for provenance, source fidelity and immutable baselines."""

import json
from pathlib import Path
import tempfile
import unittest
from zipfile import ZipFile

from import_documents import digest, import_set


class ImportDocumentsTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / 'input'
        self.source.mkdir()
        (self.source / 'README_FIRST.txt').write_text('Kontrollü kaynak', encoding='utf-8')
        self.repo = self.root / 'repo'

    def test_original_hash_provenance_and_repeatability(self):
        document = self.source / '01.01_Test.docx'
        with ZipFile(document, 'w') as archive:
            archive.writestr('word/document.xml',
                '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">'
                '<w:body><w:p><w:r><w:t>Gemi </w:t></w:r><w:r><w:t>hazır</w:t></w:r></w:p>'
                '</w:body></w:document>')
        import_set(self.source, self.repo)
        output = self.repo / 'docs/sources/chunks.jsonl'
        first = output.read_bytes()
        chunks = [json.loads(line) for line in first.decode().splitlines()]
        chunk = next(c for c in chunks if c['source_path'].endswith('.docx'))
        self.assertEqual(chunk['text'], 'Gemi hazır')
        self.assertEqual(chunk['locator'], 'paragraph:1')
        self.assertEqual(chunk['source_sha256'], digest(document.read_bytes()))
        self.assertEqual((self.repo / chunk['source_path']).read_bytes(), document.read_bytes())
        import_set(self.source, self.repo)
        self.assertEqual(first, output.read_bytes())

    def test_changed_baseline_refused_without_overwriting_outputs(self):
        import_set(self.source, self.repo)
        manifest = self.repo / 'docs/sources/manifest.json'
        before = manifest.read_bytes()
        (self.source / 'README_FIRST.txt').write_text('Changed', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'Baseline changed'):
            import_set(self.source, self.repo)
        self.assertEqual(before, manifest.read_bytes())
        self.assertEqual((self.repo / 'sources/poseidon-v46.3/README_FIRST.txt').read_text(encoding='utf-8'), 'Kontrollü kaynak')

    def test_research_is_not_promoted_to_requirement_source(self):
        reference = self.source / '90_SOURCE_RESEARCH'
        reference.mkdir()
        (reference / 'note.txt').write_text('Not a requirement', encoding='utf-8')
        historical = self.source / '07_TEST_PILOT_RELEASE'
        historical.mkdir()
        (historical / '48.01_Old_Results.txt').write_text('Historical test pass', encoding='utf-8')
        import_set(self.source, self.repo)
        manifest = json.loads((self.repo / 'docs/sources/manifest.json').read_text(encoding='utf-8'))
        record = next(r for r in manifest['files'] if 'note.txt' in r['source_path'])
        self.assertEqual(record['source_class'], 'reference-only')
        history = next(r for r in manifest['files'] if 'Old_Results' in r['source_path'])
        self.assertEqual(history['source_class'], 'historical-validation-reference')

    def test_spreadsheet_shared_inline_and_cached_values_keep_cell_locator(self):
        with ZipFile(self.source / '11.01_Test.xlsx', 'w') as archive:
            archive.writestr('xl/sharedStrings.xml',
                '<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
                '<si><t>REQ-01</t></si></sst>')
            archive.writestr('xl/workbook.xml',
                '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
                'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
                '<sheets><sheet name="Trace" r:id="rId1"/></sheets></workbook>')
            archive.writestr('xl/_rels/workbook.xml.rels',
                '<Relationships><Relationship Id="rId1" Target="worksheets/sheet1.xml"/></Relationships>')
            archive.writestr('xl/worksheets/sheet1.xml',
                '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
                '<sheetData><row r="7"><c r="A7" t="s"><v>0</v></c>'
                '<c r="B7" t="inlineStr"><is><t>Gemi</t></is></c>'
                '<c r="C7"><f>1+1</f><v>2</v></c></row></sheetData></worksheet>')
        import_set(self.source, self.repo)
        chunks = [json.loads(line) for line in (self.repo / 'docs/sources/chunks.jsonl').read_text(encoding='utf-8').splitlines()]
        row = next(c for c in chunks if c['source_path'].endswith('.xlsx'))
        self.assertEqual(row['locator'], 'sheet:Trace/row:7')
        self.assertEqual(row['text'], 'A7: REQ-01 | B7: Gemi | C7: 2')

    def test_updated_set_indexes_repo_originals_without_overwriting_prior_baseline(self):
        import_set(self.source, self.repo)
        before = (self.repo / 'docs/sources/manifest.json').read_bytes()
        updated = self.repo / 'analiz/latest'
        updated.mkdir(parents=True)
        (updated / 'README_FIRST.txt').write_text('Updated source', encoding='utf-8')
        result = import_set(updated, self.repo, baseline_id='v46.3-20261030',
                            output_relative='docs/sources/2026-10-30', copy_originals=False)
        manifest_path = self.repo / 'docs/sources/2026-10-30/manifest.json'
        manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
        record = manifest['files'][0]
        self.assertEqual(record['source_path'], 'analiz/latest/README_FIRST.txt')
        self.assertTrue(record['source_id'].startswith('v46.3-20261030:'))
        self.assertEqual((self.repo / record['source_path']).read_text(encoding='utf-8'), 'Updated source')
        self.assertEqual(before, (self.repo / 'docs/sources/manifest.json').read_bytes())
        self.assertFalse((self.repo / 'sources/v46.3-20261030').exists())
        self.assertEqual(result['files'], 1)

    def test_in_place_index_rejects_changed_provenance_without_overwriting_index(self):
        updated = self.repo / 'analiz/latest'
        updated.mkdir(parents=True)
        original = updated / 'README_FIRST.txt'
        original.write_text('One', encoding='utf-8')
        kwargs = dict(baseline_id='new', output_relative='docs/sources/new', copy_originals=False)
        import_set(updated, self.repo, **kwargs)
        manifest = self.repo / 'docs/sources/new/manifest.json'
        before = manifest.read_bytes()
        original.write_text('Two', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'Baseline changed'):
            import_set(updated, self.repo, **kwargs)
        self.assertEqual(before, manifest.read_bytes())

    def test_index_paths_cannot_escape_repo_or_write_inside_originals(self):
        for output in ['../outside', '.', 'sources/poseidon-v46.3/index']:
            with self.assertRaises(ValueError):
                import_set(self.source, self.repo, output_relative=output)
        with self.assertRaises(ValueError):
            import_set(self.source, self.repo, baseline_id='../outside')
        with self.assertRaisesRegex(ValueError, 'In-place sources'):
            import_set(self.source, self.repo, copy_originals=False)

    def test_embedded_runtime_code_is_reference_not_current_implementation_evidence(self):
        runtime = self.source / '06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source'
        runtime.mkdir(parents=True)
        (runtime / 'README.md').write_text('Historical tests passed', encoding='utf-8')
        import_set(self.source, self.repo)
        manifest = json.loads((self.repo / 'docs/sources/manifest.json').read_text(encoding='utf-8'))
        record = next(r for r in manifest['files'] if '/26.01_' in r['source_path'])
        self.assertEqual(record['source_class'], 'embedded-source-reference')


if __name__ == '__main__':
    unittest.main()
