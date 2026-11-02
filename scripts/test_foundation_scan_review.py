import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import foundation_scan_review
import secret_scan


class CurrentReviewProvenanceTests(unittest.TestCase):
    def test_filename_or_changed_source_cannot_inherit_the_inspected_fixture_classification(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            subprocess.run(['git', 'init', '-q', str(root)], check=True, capture_output=True)
            relative = 'tests/Poseidon.Persistence.Tests/PasswordHashingTests.cs'
            source = root / relative
            source.parent.mkdir(parents=True)
            marker = 'synthetic-' + 'private-probe'
            raw = ('const string password = "' + marker + '";').encode()
            source.write_bytes(raw)
            target = next(secret_scan.findings(raw.decode(), relative, raw=raw))
            ledger = root / 'docs/security/files-source-classifications.pending.json'
            ledger.parent.mkdir(parents=True)
            ledger.write_text(json.dumps({'entries': []}))
            with patch.object(secret_scan, 'scan', return_value=(1, [target])), patch.object(foundation_scan_review.subprocess, 'check_output', return_value='0' * 40):
                result = foundation_scan_review.report(root)
            self.assertEqual('unresolved', result['entries'][0]['classification'])
            self.assertTrue(result['entries'][0]['rotation_remediation_gate'])
            self.assertEqual([], result['receipts'])
            self.assertEqual([], result['exceptions'])
            self.assertFalse(result['is_exception_file'])
            self.assertNotIn(marker, json.dumps(result))
            evidence = result['entries'][0]['context_evidence']
            self.assertEqual(target['immutable_source_revision'], evidence['source']['immutable_source_revision'])
            self.assertEqual(secret_scan.object_hash(evidence), result['entries'][0]['context_evidence_sha256'])

    def test_changed_input_during_evidence_collection_fails_instead_of_rebinding_identity(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / 'fixture.py'
            raw = b'password = "synthetic-probe"'
            source.write_bytes(raw + b'\n# changed')
            target = next(secret_scan.findings(raw.decode(), 'fixture.py', raw=raw))
            ledger = root / 'docs/security/files-source-classifications.pending.json'
            ledger.parent.mkdir(parents=True)
            ledger.write_text(json.dumps({'entries': []}))
            with patch.object(secret_scan, 'scan', return_value=(1, [target])):
                with self.assertRaisesRegex(ValueError, 'changed-during-review'):
                    foundation_scan_review.report(root)

"""Real locator/identity regressions; evidence never authorizes scanner waivers."""
import io
import json
from pathlib import Path
import tempfile
import unittest
import warnings
import zipfile

from classify_file_findings import source_context
from foundation_scan_review import bound_context
import secret_scan


class BoundFoundationContextTests(unittest.TestCase):
    def evidence(self, root, locator):
        text, raw, source = source_context(root, locator)
        target = next(secret_scan.findings(text, locator, raw=raw, source=source))
        return target, bound_context(root, target)

    def test_markdown_evidence_withholds_literals_and_neighbouring_words(self):
        marker = 'gh' + 'p_' + 'x' * 36
        neighbour = 'private_neighbour_should_never_be_emitted'
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'reference.md').write_text(neighbour + '\n' + marker, encoding='utf-8')
            target, evidence = self.evidence(root, 'reference.md')
            rendered = json.dumps(evidence)
            self.assertNotIn(marker, rendered)
            self.assertNotIn(neighbour, rendered)
            self.assertEqual(target['immutable_source_revision'], evidence['source']['immutable_source_revision'])

    def test_nested_archive_utf16_locator_binds_original_outer_bytes(self):
        marker = 'gh' + 'p_' + 'y' * 36
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            nested = io.BytesIO()
            with zipfile.ZipFile(nested, 'w') as archive:
                archive.writestr('fixture.txt', marker.encode('utf-16'))
            with zipfile.ZipFile(root / 'outer.zip', 'w') as archive:
                archive.writestr('inner.zip', nested.getvalue())
            target, evidence = self.evidence(root, 'outer.zip!inner.zip!fixture.txt#utf16')
            self.assertEqual(secret_scan.source_revision((root / 'outer.zip').read_bytes()), target['immutable_source_revision'])
            self.assertEqual(target['context_sha256'], evidence['source']['context_sha256'])
            self.assertNotIn(marker, json.dumps(evidence))
            self.assertNotIn('approved', evidence)

    def test_changed_bytes_and_changed_occurrence_are_rejected(self):
        marker = 'gh' + 'p_' + 'z' * 36
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            path = root / 'fixture.md'
            path.write_text(marker, encoding='utf-8')
            target, _ = self.evidence(root, 'fixture.md')
            with self.assertRaises(ValueError):
                bound_context(root, {**target, 'occurrence': target['occurrence'] + 1})
            path.write_text(marker + ' changed', encoding='utf-8')
            with self.assertRaises(ValueError):
                bound_context(root, target)

    def test_duplicate_archive_member_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            with warnings.catch_warnings():
                warnings.simplefilter('ignore', UserWarning)
                with zipfile.ZipFile(root / 'duplicate.zip', 'w') as archive:
                    archive.writestr('same.txt', 'first')
                    archive.writestr('same.txt', 'second')
            with self.assertRaises(ValueError):
                source_context(root, 'duplicate.zip!same.txt')


if __name__ == '__main__':
    unittest.main()
