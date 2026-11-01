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
        self.assertEqual({'path', 'rule', 'line', 'fingerprint'}, set(a))

    def test_archive_limits_and_malformed_xml_are_errors(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); file = root / 'bad.docx'
            with zipfile.ZipFile(file, 'w') as archive:
                archive.writestr('word/document.xml', '<!DOCTYPE x><p/>')
            with self.assertRaises(ValueError):
                secret_scan.scan_file(file, root)

    def test_exact_reviewed_exception_does_not_suppress_a_changed_literal(self):
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); file = root / 'fixture.txt'; exceptions = root / 'exceptions.json'
            first = 'gh' + 'p_' + 'a' * 36; second = 'gh' + 'p_' + 'b' * 36
            file.write_text(first)
            found = secret_scan.scan_file(file, root)
            exceptions.write_text(json.dumps([{**found[0], 'rationale': 'Reviewed exact synthetic test fixture, no operational credential'}]))
            with patch('secret_scan.subprocess.run', return_value=SimpleNamespace(stdout=b'fixture.txt\0')):
                self.assertEqual([], secret_scan.scan(root, 'repository', exceptions=exceptions)[1])
                file.write_text(second)
                self.assertEqual(1, len(secret_scan.scan(root, 'repository', exceptions=exceptions)[1]))
