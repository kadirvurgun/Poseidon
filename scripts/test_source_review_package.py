import json
from pathlib import Path
import tempfile
import unittest

import secret_scan
from source_review_package import build_package


class SourceReviewPackageTests(unittest.TestCase):
    def fixture(self, root, name):
        text = 'password = "' + 'synthetic-' + 'private-marker"\n'
        (root / name).write_text(text, encoding='utf-8')
        target = next(secret_scan.findings(text, name))
        decision = {'classification': 'synthetic-fixture',
                    'rationale': 'Author proposal for an isolated local syntax fixture; independent review is still required.',
                    'provenance': [{'line': 1, 'role': 'isolated-parser-input'}]}
        return target, decision

    def test_shared_body_preserves_every_distinct_source_identity_without_values(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            fixtures = [self.fixture(root, name) for name in ('a.py', 'b.py')]
            targets = [t for t, _ in fixtures]
            package = build_package(root, targets, {secret_scan.object_hash(t): d for t, d in fixtures}, 'test-author')
            self.assertEqual(2, len(package['entries']))
            self.assertEqual(1, len(package['syntax_bodies']))
            self.assertEqual({'a.py', 'b.py'}, {e['proposal']['target']['path'] for e in package['entries']})
            self.assertNotIn('synthetic-private-marker', json.dumps(package))
            self.assertEqual([], package['receipts'])
            self.assertEqual([], package['exceptions'])
            self.assertFalse(package['is_exception_file'])
            for entry in package['entries']:
                self.assertEqual(secret_scan.object_hash(entry['evidence']), entry['proposal']['evidence_sha256'])
                self.assertIn(entry['evidence']['syntax_body_sha256'], package['syntax_bodies'])

    def test_missing_and_duplicate_cases_fail_closed(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); target, decision = self.fixture(root, 'a.py')
            for targets, decisions in (([], {}), ([target], {}), ([target, target], {secret_scan.object_hash(target): decision})):
                with self.assertRaisesRegex(ValueError, 'incomplete-or-duplicate'):
                    build_package(root, targets, decisions, 'test-author')

    def test_changed_source_cannot_inherit_a_proposal(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); target, decision = self.fixture(root, 'a.py')
            (root / 'a.py').write_text('changed', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'stale-source-binding'):
                build_package(root, [target], {secret_scan.object_hash(target): decision}, 'test-author')

    def test_triage_cannot_publish_a_matching_value_in_prose(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); target, decision = self.fixture(root, 'a.py')
            decision['rationale'] += ' ' + 'password' + ' = "' + 'another-' + 'private-marker"'
            with self.assertRaisesRegex(ValueError, 'unsafe-review-content'):
                build_package(root, [target], {secret_scan.object_hash(target): decision}, 'test-author')
