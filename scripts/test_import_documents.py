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
        import_set(self.source, self.repo)
        manifest = json.loads((self.repo / 'docs/sources/manifest.json').read_text(encoding='utf-8'))
        record = next(r for r in manifest['files'] if 'note.txt' in r['source_path'])
        self.assertEqual(record['source_class'], 'reference-only')

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


if __name__ == '__main__':
    unittest.main()
