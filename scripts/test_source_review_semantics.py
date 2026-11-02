import copy
import json
from pathlib import Path
import tempfile
import unittest

import secret_scan
import source_review_semantics as semantic
from source_review_package import build_package


class SourceSemanticEvidenceTests(unittest.TestCase):
    def source(self):
        marker = 'synthetic-' + 'private-marker'
        text = ('import secrets\n\ndef fake_token_response():\n'
                '    # A local test double returns an isolated response.\n'
                '    password = "' + marker + '"\n'
                '    return fake_json_response(password)\n')
        return text, marker

    def pending(self, root, names=('fixture.py',)):
        text, _ = self.source()
        targets, decisions = [], {}
        for name in names:
            (root / name).write_text(text, encoding='utf-8')
            target = next(secret_scan.findings(text, name))
            targets.append(target)
            decisions[secret_scan.object_hash(target)] = {
                'classification': 'synthetic-fixture',
                'rationale': 'An author proposal for an isolated test-double source; independent source review is required.',
                'provenance': [{'line': 3, 'role': 'local-test-double'}]}
        return build_package(root, targets, decisions, 'fixture-author')

    def test_semantics_retain_function_consumer_and_comment_without_matched_value(self):
        text, marker = self.source()
        body = semantic.semantic_context(text)
        rendered = json.dumps(body)
        self.assertIn('fake_token_response', rendered)
        self.assertIn('fake_json_response', rendered)
        self.assertIn('isolated response', rendered)
        self.assertNotIn(marker, rendered)
        self.assertIn(semantic.OMITTED, rendered)
        self.assertTrue(all(s['complete_source_included'] for s in body['python_scopes']))
        self.assertEqual('Constant', body['python_assignment_values'][0]['value_shape']['kind'])

    def test_generated_rhs_retains_generator_shape_without_static_literals(self):
        body = semantic.semantic_context('import secrets\npassword = secrets.token_urlsafe(32)\n')
        shape = body['python_assignment_values'][0]['value_shape']
        self.assertEqual('Call', shape['kind'])
        self.assertIn('token_urlsafe', json.dumps(shape))

    def test_masking_precedes_cropping_at_both_sides_of_a_credential(self):
        marker = 'gh' + 'p_' + 'q' * 36
        text = 'prefix ' + marker + ' suffix'
        for start, end in ((10, len(text)), (0, 20), (12, 15)):
            result = semantic.redacted_window(text, start, end)
            self.assertNotIn('qqq', result)
            self.assertIn(semantic.OMITTED, result)

    def test_overlapping_rules_and_multiple_occurrences_are_masked(self):
        marker = 'gh' + 'p_' + 'r' * 36
        text = 'password = "' + marker + '"; ' + marker
        body = semantic.semantic_context(text)
        self.assertGreaterEqual(len(body['occurrences']), 3)
        self.assertNotIn(marker, json.dumps(body))
        self.assertFalse(any(p.search(body['windows'][0]['text']) for p in secret_scan.RULES.values()))

    def test_html_encoded_credential_is_masked_after_unescaping(self):
        marker = 'gh' + 'p_' + 's' * 36
        text = 'example &#103;h' + marker[2:]
        result = semantic.redacted_window(text, 0, len(text))
        self.assertNotIn(marker, result)
        self.assertIn(semantic.OMITTED, result)

    def test_email_userinfo_and_full_private_key_body_are_not_emitted(self):
        key_body = 'opaque-private-' + 'body-data'
        email = 'person' + '@example.test'
        url = 'https://' + 'person:private@service.test/path'
        text = email + '\n' + url + '\n-----BEGIN PRIVATE KEY-----\n' + key_body + '\n-----END PRIVATE KEY-----'
        result = semantic.redacted_window(text, 0, len(text))
        for marker in (email, url, key_body, 'person:private'):
            self.assertNotIn(marker, result)

    def test_package_shares_context_but_preserves_each_exact_locator_and_binding(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            package = semantic.build_semantic_package(root, self.pending(root, ('a.py', 'b.py')))
            self.assertEqual(2, len(package['entries']))
            self.assertEqual(1, len(package['semantic_bodies']))
            self.assertEqual({'a.py', 'b.py'}, {e['proposal']['target']['path'] for e in package['entries']})
            self.assertFalse(package['is_exception_file'])
            self.assertEqual([], package['exceptions'])
            self.assertEqual([], package['receipts'])
            for entry in package['entries']:
                self.assertEqual(entry['proposal']['evidence_sha256'], secret_scan.object_hash(entry['evidence']))
                self.assertIn(entry['evidence']['semantic_body_sha256'], package['semantic_bodies'])

    def test_source_change_or_duplicate_identity_cannot_inherit_review_binding(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); pending = self.pending(root)
            duplicate = copy.deepcopy(pending)
            duplicate['entries'].append(copy.deepcopy(duplicate['entries'][0]))
            with self.assertRaisesRegex(ValueError, 'duplicate-source-identity'):
                semantic.build_semantic_package(root, duplicate)
            (root / 'fixture.py').write_text('changed', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'stale-source-binding'):
                semantic.build_semantic_package(root, pending)

    def test_tampered_proposal_or_evidence_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); original = self.pending(root)
            for field in ('classification', 'evidence_sha256', 'rationale'):
                pending = copy.deepcopy(original)
                pending['entries'][0]['proposal'][field] = 'tampered'
                with self.assertRaisesRegex(ValueError, 'invalid-author-proposal'):
                    semantic.build_semantic_package(root, pending)

    def test_large_scope_is_explicitly_incomplete_and_bounds_are_enforced(self):
        marker = 'synthetic-' + 'private-marker'
        text = 'def example():\n    password = "' + marker + '"\n' + ('    example_consumer(None)\n' * 1000)
        body = semantic.semantic_context(text)
        self.assertEqual(1, len(body['windows']))
        self.assertFalse(body['python_scopes'][0]['complete_source_included'])
        with self.assertRaisesRegex(ValueError, 'invalid-source-window'):
            semantic.redacted_window(text, -1, 1)
        with self.assertRaisesRegex(ValueError, 'semantic-case-budget'):
            semantic.semantic_context('no matching credentials')


if __name__ == '__main__':
    unittest.main()
