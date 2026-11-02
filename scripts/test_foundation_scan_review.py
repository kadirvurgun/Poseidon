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


if __name__ == '__main__':
    unittest.main()
