"""Synthetic isolated pin fixtures only. Never produces repository/CI approvals."""
import contextlib
import io
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import reviewed_secret_scan
import secret_scan


class ReviewedSecretScanTests(unittest.TestCase):
    def fixture(self, root):
        bundle = root / 'frontend/dist'; bundle.mkdir(parents=True)
        (bundle / 'index.html').write_text('<!doctype html><script src="/app.js"></script>')
        marker = 'gh' + 'p_' + 'z' * 36
        source = bundle / 'app.js'; source.write_text(marker, encoding='utf-8')
        return source, marker

    def synthetic_document(self, root, source):
        # Current real scanner identity/receipt validation; no runtime or global pin.
        target = secret_scan.scan_file(source, root)[0]
        proposal = {'target': target, 'classification': 'synthetic-fixture',
            'rationale': 'Synthetic isolated unit fixture; not a real credential or repository approval.',
            'evidence_sha256': 'a' * 64, 'author_reference': 'synthetic-proposal-author'}
        proposal_hash = secret_scan.object_hash(proposal)
        receipt = {'schema_version': secret_scan.IDENTITY_VERSION, 'status': 'independently-reviewed',
            'proposal_sha256': proposal_hash, 'evidence_sha256': proposal['evidence_sha256'],
            'author_reference': proposal['author_reference'], 'reviewer_reference': 'synthetic-independent-reviewer',
            'target_sha256': secret_scan.object_hash(target)}
        pin = secret_scan.object_hash(receipt)
        document = {'schema_version': secret_scan.IDENTITY_VERSION, 'proposals': [proposal], 'receipts': [receipt],
            'exceptions': [{'proposal_sha256': proposal_hash, 'review_sha256': pin}]}
        path = root / reviewed_secret_scan.EXCEPTION_DOCUMENT; path.parent.mkdir(parents=True)
        path.write_text(json.dumps(document), encoding='utf-8')
        return path, document, pin

    def run_scan(self, root, trust='', mode='client'):
        output = io.StringIO()
        with patch.dict(os.environ, {reviewed_secret_scan.TRUST_VARIABLE: trust}), contextlib.redirect_stdout(output):
            result = reviewed_secret_scan.main(['--root', str(root), '--mode', mode])
        return result, output.getvalue()

    def test_no_document_no_pins_still_performs_the_real_client_scan(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); _, marker = self.fixture(root)
            result, output = self.run_scan(root)
            self.assertEqual(1, result); self.assertNotIn(marker, output)
            self.assertIn('github-token', output); self.assertIn('covered_files', output)

    def test_real_repository_scan_remains_mandatory_without_review_documents(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); marker = 'gh' + 'p_' + 'y' * 36
            (root / 'fixture.txt').write_text(marker)
            subprocess.run(['git', 'init', '-q', str(root)], check=True, capture_output=True)
            subprocess.run(['git', '-C', str(root), 'add', 'fixture.txt'], check=True, capture_output=True)
            result, output = self.run_scan(root, mode='repository')
            self.assertEqual(1, result); self.assertNotIn(marker, output); self.assertIn('github-token', output)

    def test_self_described_receipt_does_not_supply_trust(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); source, marker = self.fixture(root); self.synthetic_document(root, source)
            result, output = self.run_scan(root)
            self.assertEqual(2, result); self.assertNotIn(marker, output); self.assertNotIn('Traceback', output)

    def test_externally_pinned_exact_synthetic_review_can_match_only_unchanged_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); source, marker = self.fixture(root); _, _, pin = self.synthetic_document(root, source)
            self.assertEqual(0, self.run_scan(root, json.dumps([pin]))[0])
            source.write_text(source.read_text() + '\n// changed source outside the match')
            result, output = self.run_scan(root, json.dumps([pin]))
            self.assertEqual(1, result); self.assertNotIn(marker, output)

    def test_forged_pin_pending_receipt_and_changed_proposal_cannot_suppress(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); source, _ = self.fixture(root); path, document, pin = self.synthetic_document(root, source)
            self.assertEqual(2, self.run_scan(root, json.dumps(['b' * 64]))[0])
            document['receipts'][0]['status'] = 'pending-independent-review'; path.write_text(json.dumps(document))
            self.assertEqual(2, self.run_scan(root, json.dumps([pin]))[0])
            document['receipts'][0]['status'] = 'independently-reviewed'; document['proposals'][0]['classification'] = 'public-example'
            path.write_text(json.dumps(document)); self.assertEqual(2, self.run_scan(root, json.dumps([pin]))[0])

    def test_missing_review_document_or_invalid_trust_fails_without_echoing(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); _, marker = self.fixture(root)
            for value in (json.dumps(['a' * 64]), '{}', '[]', 'null', '[1]', '["bad"]', json.dumps(['a' * 64, 'a' * 64]), marker, ' ' * 70001):
                result, output = self.run_scan(root, value)
                self.assertEqual(2, result); self.assertNotIn(marker, output); self.assertNotIn('Traceback', output)

    def test_absent_or_empty_actual_client_output_remains_a_required_coverage_failure(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); self.assertEqual(2, self.run_scan(root)[0])
            bundle = root / 'frontend/dist'; bundle.mkdir(parents=True)
            (bundle / 'index.html').write_text('<!doctype html>')
            (bundle / 'app.js').write_text('')
            self.assertEqual(2, self.run_scan(root)[0])


if __name__ == '__main__':
    unittest.main()
