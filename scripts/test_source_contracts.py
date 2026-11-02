import hashlib
from pathlib import Path
import tempfile
import unittest
from zipfile import ZipFile
from source_contracts import SourceRecords

class SourceContractTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.path = self.root / "analiz/POSEIDON GUNCEL 30.10.2026/fixture.docx"
        self.path.parent.mkdir(parents=True)
        with ZipFile(self.path, "w") as archive:
            archive.writestr("word/document.xml", '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>FR-real-synthetic</w:t></w:r></w:p></w:body></w:document>')
        self.sources = {"fixture": {"path": self.path.relative_to(self.root).as_posix(), "sha256": hashlib.sha256(self.path.read_bytes()).hexdigest()}}

    def test_actual_paragraph_resolves_but_invented_value_does_not(self):
        records = SourceRecords(self.sources, self.root)
        ref = {"source": "fixture", "locator": "paragraph:1", "value": "FR-real-synthetic"}
        self.assertEqual("FR-real-synthetic", records.resolve(ref))
        with self.assertRaises(ValueError): records.resolve({**ref, "value": "FR-invented"})
        with self.assertRaises(ValueError): records.resolve({**ref, "locator": "paragraph:999"})

    def test_changed_bytes_or_path_escape_fail_before_source_use(self):
        self.path.write_bytes(b"changed")
        with self.assertRaises(ValueError): SourceRecords(self.sources, self.root).records("fixture")
        self.sources["fixture"]["path"] = "../outside.docx"
        with self.assertRaises(ValueError): SourceRecords(self.sources, self.root).records("fixture")

    def test_invalid_original_archive_has_a_bounded_diagnostic(self):
        self.path.write_bytes(b"not an archive")
        self.sources["fixture"]["sha256"] = hashlib.sha256(self.path.read_bytes()).hexdigest()
        with self.assertRaisesRegex(ValueError, "invalid_source_archive"):
            SourceRecords(self.sources, self.root).records("fixture")

    def test_locator_range_cannot_become_unbounded_corpus_read(self):
        with self.assertRaises(ValueError): SourceRecords(self.sources, self.root).resolve({"source": "fixture", "locator": "paragraph:1..99999"})

    def test_declarations_are_rejected_even_with_matching_original_hash(self):
        with ZipFile(self.path, "w") as archive:
            archive.writestr("word/document.xml", '<!DOCTYPE x><x/>')
        self.sources["fixture"]["sha256"] = hashlib.sha256(self.path.read_bytes()).hexdigest()
        with self.assertRaises(ValueError): SourceRecords(self.sources, self.root).records("fixture")

if __name__ == "__main__": unittest.main()
