import contextlib
import io
import json
import tempfile
import unittest
import zipfile
import os
import subprocess
from pathlib import Path
from unittest.mock import patch
import secret_scan


class SecretScanTests(unittest.TestCase):
    def test_context_hash_is_newline_stable_and_any_surrounding_change_invalidates_review(self):
        marker = 'gh' + 'p_' + 'n' * 36
        a = list(secret_scan.findings('before\r\n' + marker + '\r\nafter', 'fixture.txt'))[0]
        b = list(secret_scan.findings('before\n' + marker + '\nafter', 'fixture.txt'))[0]
        c = list(secret_scan.findings('changed\n' + marker + '\nafter', 'fixture.txt'))[0]
        self.assertEqual(a, b)
        self.assertEqual(a['fingerprint'], c['fingerprint'])
        self.assertNotEqual(a['context_sha256'], c['context_sha256'])

    def test_pending_self_review_and_legacy_unbound_exception_records_fail_closed(self):
        marker = 'gh' + 'p_' + 'n' * 36
        found = next(secret_scan.findings(marker, 'fixture.txt'))
        legacy = {k: v for k, v in found.items() if k != 'context_sha256'}
        legacy['rationale'] = 'A claimed fixture without source-context and independent review cannot suppress findings.'
        with self.assertRaises(ValueError):
            secret_scan.approved_exceptions([legacy])
        review = {'status': 'pending-independent-review', 'reviewer_reference': 'same-author', 'author_reference': 'same-author', 'target_context_sha256': found['context_sha256'], 'target_fingerprint': found['fingerprint'], 'evidence_reference': 'synthetic-unit-test'}
        proposal = {**found, 'classification': 'synthetic-fixture', 'rationale': legacy['rationale'], 'review': review}
        for state in ('pending-independent-review', 'independently-reviewed'):
            review['status'] = state
            with self.assertRaises(ValueError):
                secret_scan.approved_exceptions([proposal])

    def test_linked_input_parent_is_rejected_even_when_target_is_inside_root(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); original = root / 'original'; original.mkdir()
            (original / 'fixture.txt').write_text('synthetic noncredential input')
            link = root / 'shortcut'
            if os.name == 'nt':
                result = subprocess.run(['cmd.exe', '/c', 'mklink', '/J', str(link), str(original)], capture_output=True)
                self.assertEqual(0, result.returncode)
            else:
                link.symlink_to(original, target_is_directory=True)
            try:
                with self.assertRaises(ValueError):
                    secret_scan.scan_file(link / 'fixture.txt', root)
            finally:
                if os.name == 'nt':
                    link.rmdir()
                else:
                    link.unlink()

    def test_nested_archive_is_scanned_and_shares_expansion_budget(self):
        inner = io.BytesIO()
        marker = 'gh' + 'p_' + 'z' * 36
        with zipfile.ZipFile(inner, 'w') as archive:
            archive.writestr('fixture.txt', marker)
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); path = root / 'outer.zip'
            with zipfile.ZipFile(path, 'w') as archive:
                archive.writestr('inner.zip', inner.getvalue())
            findings = secret_scan.scan_file(path, root)
            self.assertTrue(any(x['path'].endswith('!inner.zip!fixture.txt') for x in findings))
            self.assertNotIn(marker, json.dumps(findings))
            with patch('secret_scan.MAX_EXPANDED', len(inner.getvalue())):
                with self.assertRaises(ValueError):
                    secret_scan.scan_file(path, root)
            with patch('secret_scan.MAX_DEPTH', 0):
                with self.assertRaises(ValueError):
                    secret_scan.scan_file(path, root)

    def test_detects_redacts_and_scans_real_client_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); (root / 'frontend/dist').mkdir(parents=True)
            (root / 'frontend/dist/index.html').write_text('<html></html>')
            secret = 'gh' + 'p_' + 's' * 36
            (root / 'frontend/dist/main.js').write_text('let value = "' + secret + '";')
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                result = secret_scan.main(['--root', str(root), '--mode', 'client'])
            self.assertEqual(1, result); self.assertNotIn(secret, out.getvalue())
            self.assertIn('github-token', out.getvalue()); self.assertNotIn('let value', out.getvalue())

    def test_missing_empty_malformed_and_unreadable_inputs_fail_closed(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for mode in ('client', 'repository'):
                with contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(2, secret_scan.main(['--root', str(root), '--mode', mode]))
            with patch('secret_scan.read_file', side_effect=PermissionError()):
                with self.assertRaises(PermissionError):
                    secret_scan.scan_file(root / 'missing', root)

    def test_no_blanket_fixture_document_or_build_exemptions(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); file = root / 'fixture.docx'
            value = 'client' + '_secret="' + 'synthetic' * 3 + '"'
            with zipfile.ZipFile(file, 'w') as archive:
                archive.writestr('word/document.xml', '<p>' + value + '</p>')
            found = secret_scan.scan_file(file, root)
            self.assertTrue(any(f['rule'] == 'credential-literal' for f in found))
            self.assertTrue(all('synthetic' not in str(f) for f in found))

    def test_safe_negative_and_exact_exception_cannot_hide_changed_value(self):
        self.assertEqual([], list(secret_scan.findings('configuration uses an environment reference', 'safe.txt')))
        a = next(secret_scan.findings('gh' + 'p_' + 'x' * 36, 'fixture.txt'))
        b = next(secret_scan.findings('gh' + 'p_' + 'y' * 36, 'fixture.txt'))
        self.assertNotEqual(a['fingerprint'], b['fingerprint'])
        self.assertEqual(set(secret_scan.TARGET_KEYS), set(a))

    def test_archive_limits_and_malformed_xml_are_errors(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); file = root / 'bad.docx'
            with zipfile.ZipFile(file, 'w') as archive:
                archive.writestr('word/document.xml', '<!DOCTYPE x><p/>')
            with self.assertRaises(ValueError):
                secret_scan.scan_file(file, root)

    @staticmethod
    def reviewed_document(target):
        # Entirely synthetic in-memory receipt and pin; never a repository approval.
        proposal = {'target': target, 'classification': 'synthetic-fixture',
                    'rationale': 'Generated isolated test marker with no product consumer; synthetic test of the receipt contract only.',
                    'evidence_sha256': 'a' * 64, 'author_reference': 'unit-test-author'}
        proposal_hash = secret_scan.object_hash(proposal)
        receipt = {'schema_version': secret_scan.IDENTITY_VERSION, 'status': 'independently-reviewed',
                   'proposal_sha256': proposal_hash, 'evidence_sha256': proposal['evidence_sha256'],
                   'author_reference': 'unit-test-author', 'reviewer_reference': 'unit-test-reviewer',
                   'target_sha256': secret_scan.object_hash(target)}
        pin = secret_scan.object_hash(receipt)
        return {'schema_version': secret_scan.IDENTITY_VERSION, 'proposals': [proposal], 'receipts': [receipt],
                'exceptions': [{'proposal_sha256': proposal_hash, 'review_sha256': pin}]}, pin

    def test_exact_reviewed_exception_does_not_suppress_a_changed_literal(self):
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); file = root / 'fixture.txt'; exceptions = root / 'exceptions.json'
            first = 'gh' + 'p_' + 'a' * 36; second = 'gh' + 'p_' + 'b' * 36
            file.write_bytes(first.encode())
            found = secret_scan.scan_file(file, root)
            document, pin = self.reviewed_document(found[0])
            exceptions.write_text(json.dumps(document))
            with patch('secret_scan.subprocess.run', return_value=SimpleNamespace(stdout=b'fixture.txt\0')):
                with self.assertRaises(ValueError):
                    secret_scan.scan(root, 'repository', exceptions=exceptions)
                self.assertEqual([], secret_scan.scan(root, 'repository', exceptions=exceptions, trusted_review_sha256=[pin])[1])
                file.write_bytes(second.encode())
                self.assertEqual(1, len(secret_scan.scan(root, 'repository', exceptions=exceptions, trusted_review_sha256=[pin])[1]))

    def test_invalid_byte_sequences_cannot_share_context_or_source_approval(self):
        marker = ('gh' + 'p_' + 'v' * 36).encode()
        a = b'\xff ' + marker; b = b'\xfe ' + marker
        self.assertEqual(a.decode('utf-8', errors='replace'), b.decode('utf-8', errors='replace'))
        first = next(secret_scan.findings(a.decode('utf-8', errors='replace'), 'a', raw=a))
        second = next(secret_scan.findings(b.decode('utf-8', errors='replace'), 'a', raw=b))
        self.assertNotEqual(first['context_sha256'], second['context_sha256'])
        self.assertNotEqual(first['immutable_source_revision'], second['immutable_source_revision'])

    def test_every_target_dimension_prevents_inherited_approval(self):
        from copy import deepcopy
        target = next(secret_scan.findings('gh' + 'p_' + 'n' * 36, 'archive.zip!member.txt'))
        document, pin = self.reviewed_document(target)
        accepted = secret_scan.approved_exceptions(document, [pin])
        changes = {'path': 'archive.zip!other.txt', 'line': 2, 'occurrence': 1,
                   'rule': 'aws-key', 'fingerprint': 'b' * 64, 'context_sha256': 'c' * 64,
                   'immutable_source_revision': 'git-blob-sha256:' + 'd' * 64,
                   'identity_version': 'obsolete-version'}
        for key, value in changes.items():
            with self.subTest(dimension=key):
                changed = {**target, key: value}
                self.assertFalse(any(all(changed[k] == x[k] for k in secret_scan.TARGET_KEYS) for x in accepted))
                tampered = deepcopy(document); tampered['proposals'][0]['target'] = changed
                with self.assertRaises((ValueError, KeyError)):
                    secret_scan.approved_exceptions(tampered, [pin])
        changed_path = {**target, 'path': 'other.zip!member.txt'}
        self.assertNotIn(changed_path, accepted)

    def test_receipt_proposal_and_evidence_are_exact_and_coordinator_pinned(self):
        from copy import deepcopy
        target = next(secret_scan.findings('gh' + 'p_' + 'n' * 36, 'fixture'))
        document, pin = self.reviewed_document(target)
        for field, value in [('status', 'rejected'), ('proposal_sha256', '0' * 64),
                             ('evidence_sha256', '0' * 64), ('target_sha256', '0' * 64),
                             ('reviewer_reference', 'unit-test-author'), ('author_reference', 'other-author')]:
            changed = deepcopy(document); changed['receipts'][0][field] = value
            changed_pin = secret_scan.object_hash(changed['receipts'][0])
            changed['exceptions'][0]['review_sha256'] = changed_pin
            with self.subTest(field=field):
                with self.assertRaises((ValueError, KeyError)):
                    secret_scan.approved_exceptions(changed, [pin])
                with self.assertRaises((ValueError, KeyError)):
                    secret_scan.approved_exceptions(changed, [changed_pin])
        changed = deepcopy(document); changed['proposals'][0]['rationale'] += ' changed'
        with self.assertRaises((ValueError, KeyError)):
            secret_scan.approved_exceptions(changed, [pin])

    def test_source_revision_is_git_blob_not_current_commit_and_newlines_are_portable(self):
        text = 'before\n' + 'gh' + 'p_' + 'n' * 36 + '\nafter'
        for encoding in ('utf-8', 'utf-16-le', 'utf-16-be'):
            bom = {'utf-8': b'', 'utf-16-le': b'\xff\xfe', 'utf-16-be': b'\xfe\xff'}[encoding]
            unix = bom + text.encode(encoding)
            windows = bom + text.replace('\n', '\r\n').encode(encoding)
            self.assertEqual(secret_scan.source_revision(unix), secret_scan.source_revision(windows))
            self.assertEqual(secret_scan.canonical_bytes(unix), secret_scan.canonical_bytes(windows))
        # Unrelated checkout commits are absent from the identity preimage.
        with patch('secret_scan.subprocess.run', side_effect=AssertionError('no Git HEAD lookup')):
            self.assertEqual(secret_scan.source_revision(text.encode()), secret_scan.source_revision(text.encode()))

    def test_utf16_nested_members_and_same_line_xml_duplicates_are_all_retained(self):
        marker = 'gh' + 'p_' + 'm' * 36
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); path = root / 'outer.zip'
            inner = io.BytesIO()
            with zipfile.ZipFile(inner, 'w') as archive:
                archive.writestr('utf16.txt', marker.encode('utf-16'))
                archive.writestr('duplicate.xml', '<p><a>' + marker + '</a><b>' + marker + '</b></p>')
            with zipfile.ZipFile(path, 'w') as archive:
                archive.writestr('inner.zip', inner.getvalue())
            found = secret_scan.scan_file(path, root)
            self.assertTrue(any(f['path'].endswith('utf16.txt#utf16') for f in found))
            xml = [f for f in found if f['path'].endswith('duplicate.xml')]
            self.assertEqual(2, len(xml)); self.assertEqual(2, len({f['occurrence'] for f in xml}))
            self.assertNotIn(marker, json.dumps(found))
            before = xml[0]['immutable_source_revision']
            with zipfile.ZipFile(path, 'a') as archive:
                archive.writestr('unrelated.txt', 'context changed')
            after = [f for f in secret_scan.scan_file(path, root) if f['path'].endswith('duplicate.xml')][0]
            self.assertNotEqual(before, after['immutable_source_revision'])

    def test_duplicate_archive_names_are_ambiguous_and_fail_closed(self):
        import warnings
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); path = root / 'duplicate.zip'
            with warnings.catch_warnings():
                warnings.simplefilter('ignore', UserWarning)
                with zipfile.ZipFile(path, 'w') as archive:
                    archive.writestr('a', 'first'); archive.writestr('a', 'second')
            with self.assertRaises(ValueError):
                secret_scan.scan_file(path, root)
