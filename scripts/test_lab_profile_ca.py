"""Pinned-bundle NSS loading fails closed before any library is loaded (no system NSS fallback)."""
import contextlib
import io
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import lab_profile_ca as lab

POSIX_ALL = lab.POSIX_DEPENDENCIES + lab.POSIX_NSS + lab.POSIX_SOFTOKEN


class PinnedBundleTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(prefix='poseidon-nss-bundle-')
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve()
        self.bundle = self.root / 'firefox-1555' / 'firefox'
        self.bundle.mkdir(parents=True)

    def populate(self, names):
        for name in names:
            (self.bundle / name).write_bytes(b'fictional')

    def test_each_missing_posix_library_fails_closed_before_any_load(self):
        for missing in POSIX_ALL:
            with self.subTest(missing=missing):
                for entry in self.bundle.iterdir():
                    entry.unlink()
                self.populate(name for name in POSIX_ALL if name != missing)
                with patch.object(lab.c, 'CDLL') as loader, \
                        self.assertRaisesRegex(RuntimeError, '^nss_bundle_incomplete$'):
                    lab.load_nss(self.bundle, platform='posix')
                loader.assert_not_called()

    def test_missing_windows_nss_fails_closed_before_any_load(self):
        with patch.object(lab.c, 'CDLL') as loader, \
                self.assertRaisesRegex(RuntimeError, '^nss_bundle_incomplete$'):
            lab.load_nss(self.bundle, platform='nt')
        loader.assert_not_called()

    def test_directory_in_place_of_library_fails_closed(self):
        self.populate(name for name in POSIX_ALL if name != 'libnssutil3.so')
        (self.bundle / 'libnssutil3.so').mkdir()
        with patch.object(lab.c, 'CDLL') as loader, \
                self.assertRaisesRegex(RuntimeError, '^nss_bundle_incomplete$'):
            lab.load_nss(self.bundle, platform='posix')
        loader.assert_not_called()

    @unittest.skipIf(os.name == 'nt', 'POSIX symlink creation')
    def test_library_aliased_outside_bundle_fails_closed(self):
        outside = self.root / 'system-libnss3.so'
        outside.write_bytes(b'fictional')
        self.populate(name for name in POSIX_ALL if name != 'libnss3.so')
        (self.bundle / 'libnss3.so').symlink_to(outside)
        with patch.object(lab.c, 'CDLL') as loader, \
                self.assertRaisesRegex(RuntimeError, '^nss_bundle_incomplete$'):
            lab.load_nss(self.bundle, platform='posix')
        loader.assert_not_called()

    def test_complete_posix_bundle_loads_only_bundle_paths_in_order(self):
        self.populate(POSIX_ALL)
        with patch.object(lab.c, 'CDLL', side_effect=lambda path, mode=0: path) as loader:
            libraries = lab.load_nss(self.bundle, platform='posix')
        loaded = [call.args[0] for call in loader.call_args_list]
        expected = lab.POSIX_DEPENDENCIES + lab.POSIX_NSS
        self.assertEqual([str(self.bundle / name) for name in expected], loaded)
        self.assertTrue(all(call.kwargs['mode'] == lab.c.RTLD_GLOBAL for call in loader.call_args_list))
        self.assertEqual(tuple(str(self.bundle / name) for name in lab.POSIX_NSS), libraries)

    def test_complete_windows_bundle_loads_only_nss3(self):
        self.populate(lab.WINDOWS_NSS)
        with patch.object(lab.c, 'CDLL', side_effect=lambda path: path) as loader:
            libraries = lab.load_nss(self.bundle, platform='nt')
        self.assertEqual((str(self.bundle / 'nss3.dll'),), libraries)
        loader.assert_called_once()


class CliDiagnosticTests(unittest.TestCase):
    def run_main(self, error):
        stderr = io.StringIO()
        with patch.object(lab, 'install', side_effect=error), contextlib.redirect_stderr(stderr):
            code = lab.main(['profile', 'firefox', 'owner'])
        return code, stderr.getvalue()

    def test_missing_dependency_reports_fixed_code(self):
        self.assertEqual((1, 'lab_profile_ca: nss_bundle_incomplete\n'),
                         self.run_main(RuntimeError('nss_bundle_incomplete')))

    def test_exception_text_never_escapes(self):
        for error in (OSError('/home/secret/path: cannot open'), RuntimeError('free text with /path'),
                      RuntimeError('a', 'b'), ValueError('nss_bundle_incomplete')):
            with self.subTest(error=type(error).__name__):
                self.assertEqual((1, 'lab_profile_ca: failed\n'), self.run_main(error))

    def test_success_is_silent(self):
        stderr = io.StringIO()
        with patch.object(lab, 'install', return_value=None), contextlib.redirect_stderr(stderr):
            self.assertEqual(0, lab.main([]))
        self.assertEqual('', stderr.getvalue())


if __name__ == '__main__':
    unittest.main()
