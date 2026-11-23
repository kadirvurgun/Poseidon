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
        environment = patch.dict(os.environ)
        environment.start()
        self.addCleanup(environment.stop)
        for name in lab.REFUSED_ENVIRONMENT:
            os.environ.pop(name, None)

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

    def test_each_missing_windows_library_fails_closed_before_any_load(self):
        for missing in lab.WINDOWS_REQUIRED:
            with self.subTest(missing=missing):
                for entry in self.bundle.iterdir():
                    entry.unlink()
                self.populate(name for name in lab.WINDOWS_REQUIRED if name != missing)
                with patch.object(lab.c, 'CDLL') as loader,                         self.assertRaisesRegex(RuntimeError, '^nss_bundle_incomplete$'):
                    lab.load_nss(self.bundle, platform='nt')
                loader.assert_not_called()

    def test_preload_or_audit_refused_before_any_load(self):
        self.populate(POSIX_ALL)
        for name in lab.REFUSED_ENVIRONMENT:
            with self.subTest(variable=name), patch.dict(os.environ, {name: '/tmp/fictional-inject.so'}),                     patch.object(lab.c, 'CDLL') as loader,                     self.assertRaisesRegex(RuntimeError, '^nss_preload_refused$'):
                lab.load_nss(self.bundle, platform='posix')
            loader.assert_not_called()

    def test_empty_preload_is_not_an_injection(self):
        self.populate(POSIX_ALL)
        with patch.dict(os.environ, {'LD_PRELOAD': ''}),                 patch.object(lab.c, 'CDLL', side_effect=lambda path, mode=0: path):
            self.assertEqual(len(lab.POSIX_NSS), len(lab.load_nss(self.bundle, platform='posix')))

    def test_complete_windows_bundle_loads_only_nss3(self):
        self.populate(lab.WINDOWS_REQUIRED)
        with patch.object(lab.c, 'CDLL', side_effect=lambda path: path) as loader:
            libraries = lab.load_nss(self.bundle, platform='nt')
        self.assertEqual((str(self.bundle / 'nss3.dll'),), libraries)
        loader.assert_called_once()


def maps_line(path, inode=4242):
    return f'7f0000000000-7f0000001000 r-xp 00000000 08:01 {inode}                     {path}'


class MappedLibraryTests(unittest.TestCase):
    """/proc/self/maps verification after NSS_InitReadWrite, using a fake maps text."""

    def setUp(self):
        temp = tempfile.TemporaryDirectory(prefix='poseidon-nss-maps-')
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name).resolve()
        self.bundle = self.root / 'firefox-1555' / 'firefox'
        self.system = self.root / 'usr' / 'lib'
        self.bundle.mkdir(parents=True)
        self.system.mkdir(parents=True)

    def maps(self, *paths):
        unrelated = [maps_line('/usr/bin/python3.12'), maps_line(self.system / 'libc.so.6'),
                     '7ffd00000000-7ffd00021000 rw-p 00000000 00:00 0                          [stack]',
                     '7f0000002000-7f0000003000 rw-p 00000000 00:00 0 ']
        return chr(10).join(unrelated + [maps_line(path) for path in paths]) + chr(10)

    def bundle_set(self):
        return [self.bundle / name for name in POSIX_ALL]

    def test_all_bundle_libraries_pass(self):
        lab.verify_mapped(self.bundle, self.maps(*self.bundle_set()))

    def test_glibc_name_service_modules_are_not_mozilla_nss(self):
        lab.verify_mapped(self.bundle, self.maps(*self.bundle_set(), self.system / 'libnss_files.so.2',
                                                 self.system / 'libnss_dns.so.2'))

    def test_each_foreign_family_library_fails_closed(self):
        for name in ('libsoftokn3.so', 'libfreeblpriv3.so', 'libfreebl3.so', 'libnssutil3.so', 'libnspr4.so',
                     'libplc4.so', 'libplds4.so', 'libsmime3.so', 'libmozsqlite3.so', 'libnssckbi.so',
                     'libnss3.so.0d'):
            with self.subTest(name=name), self.assertRaisesRegex(RuntimeError, '^nss_foreign_library$'):
                lab.verify_mapped(self.bundle, self.maps(*self.bundle_set(), self.system / name))

    def test_deleted_mapping_fails_closed(self):
        with self.assertRaisesRegex(RuntimeError, '^nss_foreign_library$'):
            lab.verify_mapped(self.bundle, self.maps(*self.bundle_set()[:-1],
                                                     f'{self.bundle / "libfreeblpriv3.so"} (deleted)'))

    def test_subdirectory_of_bundle_is_foreign(self):
        nested = self.bundle / 'gmp'
        nested.mkdir()
        with self.assertRaisesRegex(RuntimeError, '^nss_foreign_library$'):
            lab.verify_mapped(self.bundle, self.maps(*self.bundle_set(), nested / 'libsoftokn3.so'))

    def test_softoken_not_mapped_fails_closed(self):
        for absent in lab.MAPPED_REQUIRED:
            with self.subTest(absent=absent), self.assertRaisesRegex(RuntimeError, '^nss_foreign_library$'):
                lab.verify_mapped(self.bundle, self.maps(*(path for path in self.bundle_set()
                                                           if path.name != absent)))

    def test_empty_maps_fails_closed(self):
        with self.assertRaisesRegex(RuntimeError, '^nss_foreign_library$'):
            lab.verify_mapped(self.bundle, '')

    @unittest.skipIf(os.name == 'nt', 'POSIX symlink creation')
    def test_symlinked_path_resolving_outside_bundle_fails_closed(self):
        target = self.system / 'libsoftokn3.so'
        target.write_bytes(b'fictional')
        alias = self.bundle / 'alias-libsoftokn3.so'
        alias.symlink_to(target)
        with self.assertRaisesRegex(RuntimeError, '^nss_foreign_library$'):
            lab.verify_mapped(self.bundle, self.maps(*self.bundle_set(), alias))

    def test_unreadable_maps_fails_closed(self):
        with patch.object(lab.Path, 'read_text', side_effect=OSError('no procfs')),                 self.assertRaisesRegex(RuntimeError, '^nss_foreign_library$'):
            lab.read_maps()


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
