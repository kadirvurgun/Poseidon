"""Owned temporary-root regressions; authoring is not native execution proof."""
from copy import deepcopy
import ctypes as c
from ctypes import wintypes as w
import hashlib
import os
from pathlib import Path
import stat
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

import bundle_object_root as fixture
import bundle_oidc_lab as bundle
from lab_profile_acl import WindowsAcl as NativeWindowsAcl


class BundleObjectRootTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='poseidon-owned-object-test-')
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name).resolve()
        self.repository = self.base / 'worktree'
        self.repository.mkdir()
        self.project = 'poseidon-lab-' + hashlib.sha256(str(self.repository).encode()).hexdigest()[:12]
        self.path = self.base / (self.project + '-source-bundle-objects')
        self.lab = SimpleNamespace(ROOT=self.repository, PROJECT=self.project, private_directory=Mock())
        self.current = 'S-1-5-21-1-2-3-1001'  # Synthetic parser fixture, not an OS identity.
        self.snapshot = {'owner': self.current, 'protected': True, 'rules': [
            {'sid': self.current, 'type': 0, 'flags': 3, 'mask': fixture.FULL_CONTROL}]}
        self.native = Mock(current_user=self.current)
        self.native.snapshot.side_effect = lambda _: deepcopy(self.snapshot)
        self.addCleanup(patch.stopall)
        patch.object(fixture.tempfile, 'gettempdir', return_value=str(self.base)).start()
        patch.object(fixture, '_windows', return_value=True).start()
        patch.object(fixture, 'WindowsAcl', return_value=self.native).start()

    def existing(self):
        self.path.mkdir(mode=0o700)
        return self.path

    def test_existing_correct_root_keeps_open_object_bytes_and_never_restricts(self):
        self.existing()
        object_file = self.path / 'owned.object'
        object_file.write_bytes(b'fictional-object-content')
        before = object_file.read_bytes()
        with object_file.open('rb') as held:
            for _ in range(2):
                self.assertTrue(fixture.ensure(self.lab) == self.path, 'owned_path_changed')
            self.assertEqual(before, held.read())
        self.assertEqual(before, object_file.read_bytes())
        self.assertEqual(0, self.lab.private_directory.call_count)
        self.assertEqual(0, self.native.protect.call_count)

    def test_fresh_root_is_created_exclusively_restricted_once_and_read_back(self):
        self.assertFalse(self.path.exists())
        self.assertTrue(fixture.ensure(self.lab) == self.path, 'owned_path_changed')
        self.assertTrue(self.lab.private_directory.call_count == 1 and self.lab.private_directory.call_args.args == (self.path,), 'fresh_restriction_contract')
        self.assertEqual(2, self.native.snapshot.call_count)
        self.assertFalse(any(self.path.iterdir()), 'fresh_root_not_empty')
        fixture.ensure(self.lab)
        self.assertTrue(self.lab.private_directory.call_count == 1 and self.lab.private_directory.call_args.args == (self.path,), 'fresh_restriction_contract')
        self.assertEqual(0, self.native.protect.call_count)

    def test_wrong_owner_protection_and_missing_or_extra_grants_fail_without_repair(self):
        self.existing()
        cases = [
            ('WRONG_OWNER', {**self.snapshot, 'owner': 'S-1-5-18'}),
            ('UNPROTECTED', {**self.snapshot, 'protected': False}),
            ('MISSING_RULE', {**self.snapshot, 'rules': []}),
            ('EXTRA_RULE', {**self.snapshot, 'rules': self.snapshot['rules'] * 2}),
            ('UNKNOWN_KEY', {**self.snapshot, 'unknown': True}),
        ]
        for label, snapshot in cases:
            with self.subTest(case=label):
                self.native.snapshot.side_effect = lambda _, value=snapshot: deepcopy(value)
                with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_acl$'):
                    fixture.ensure(self.lab)
        self.assertEqual(0, self.lab.private_directory.call_count)
        self.assertEqual(0, self.native.protect.call_count)

    def test_broad_deny_inherited_narrow_or_noninteger_aces_are_not_repaired(self):
        self.existing()
        for field, value in [('sid', 'S-1-1-0'), ('type', 1), ('flags', 19), ('flags', 0),
                             ('mask', 0x120089), ('type', False), ('flags', 3.0)]:
            changed = deepcopy(self.snapshot)
            changed['rules'][0][field] = value
            self.native.snapshot.side_effect = lambda _, snapshot=changed: deepcopy(snapshot)
            with self.subTest(field=field), self.assertRaisesRegex(RuntimeError, '^source_bundle_object_acl$'):
                fixture.ensure(self.lab)
        self.assertEqual(0, self.lab.private_directory.call_count)

    def test_acl_change_between_readbacks_fails_without_any_acl_write(self):
        self.existing()
        self.native.snapshot.side_effect = [deepcopy(self.snapshot), {**self.snapshot, 'protected': False}]
        with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_acl$'):
            fixture.ensure(self.lab)
        self.assertEqual(0, self.lab.private_directory.call_count)
        self.assertEqual(0, self.native.protect.call_count)

    def test_identity_replacement_during_readback_preserves_old_objects_and_stops(self):
        self.existing()
        object_file = self.path / 'owned.object'
        object_file.write_bytes(b'fictional-retained-object')
        retained = self.base / 'retained-original'
        calls = 0
        def snapshot(_):
            nonlocal calls
            calls += 1
            if calls == 1:
                self.path.rename(retained)
                self.path.mkdir(mode=0o700)
            return deepcopy(self.snapshot)
        self.native.snapshot.side_effect = snapshot
        with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_identity$'):
            fixture.ensure(self.lab)
        self.assertEqual(b'fictional-retained-object', (retained / 'owned.object').read_bytes())
        self.assertEqual(0, self.lab.private_directory.call_count)

    def test_racing_creation_is_not_adopted_or_restricted(self):
        boundary = fixture._boundary
        def racing(path, repository, *, exists):
            result = boundary(path, repository, exists=exists)
            if result is None:
                path.mkdir(mode=0o700)
            return result
        with patch.object(fixture, '_boundary', side_effect=racing):
            with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_creation_raced$'):
                fixture.ensure(self.lab)
        self.assertTrue(self.path.is_dir())
        self.assertEqual(0, self.lab.private_directory.call_count)

    def test_fresh_failed_restriction_never_retries_or_removes_evidence(self):
        self.lab.private_directory.side_effect = RuntimeError('synthetic_restriction_failed')
        with self.assertRaisesRegex(RuntimeError, '^synthetic_restriction_failed$'):
            fixture.ensure(self.lab)
        self.assertTrue(self.lab.private_directory.call_count == 1 and self.lab.private_directory.call_args.args == (self.path,), 'fresh_restriction_contract')
        self.assertTrue(self.path.is_dir())

    def test_fresh_failed_readback_keeps_root_and_does_not_restrict_again(self):
        self.native.snapshot.side_effect = lambda _: {**self.snapshot, 'protected': False}
        with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_acl$'):
            fixture.ensure(self.lab)
        self.assertTrue(self.lab.private_directory.call_count == 1 and self.lab.private_directory.call_args.args == (self.path,), 'fresh_restriction_contract')
        self.assertTrue(self.path.is_dir())

    def test_fresh_unexpected_child_is_preserved_and_not_adopted(self):
        self.lab.private_directory.side_effect = lambda path: (path / 'unexpected').write_bytes(b'fictional')
        with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_creation_raced$'):
            fixture.ensure(self.lab)
        self.assertTrue(self.lab.private_directory.call_count == 1 and self.lab.private_directory.call_args.args == (self.path,), 'fresh_restriction_contract')
        self.assertEqual(b'fictional', (self.path / 'unexpected').read_bytes())

    def test_caller_project_override_cannot_select_another_path(self):
        self.lab.PROJECT = '../foreign'
        with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_boundary$'):
            fixture.ensure(self.lab)
        self.assertEqual(0, self.lab.private_directory.call_count)
        self.assertFalse(self.path.exists())

    def test_repository_and_git_ancestor_storage_are_refused_before_creation(self):
        with patch.object(fixture.tempfile, 'gettempdir', return_value=str(self.repository)):
            with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_boundary$'):
                fixture.ensure(self.lab)
        (self.base / '.git').write_text('fictional-marker', encoding='utf-8')
        with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_boundary$'):
            fixture.ensure(self.lab)
        self.assertEqual(0, self.lab.private_directory.call_count)
        self.assertFalse(self.path.exists())

    def test_existing_file_and_reparse_metadata_are_refused_without_write(self):
        self.path.write_bytes(b'fictional-file-not-directory')
        with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_boundary$'):
            fixture.ensure(self.lab)
        self.assertEqual(b'fictional-file-not-directory', self.path.read_bytes())
        self.path.unlink()
        self.existing()
        with patch.object(Path, 'is_junction', lambda p: p == self.path):
            with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_boundary$'):
                fixture.ensure(self.lab)
        self.assertEqual(0, self.lab.private_directory.call_count)

    def test_real_temp_parent_alias_is_refused_without_touching_target(self):
        alias = self.base / 'alias'
        if os.name == 'nt':
            p = subprocess.run(['cmd.exe', '/c', 'mklink', '/J', str(alias), str(self.base)], capture_output=True)
            if p.returncode:
                self.fail('owned_test_junction_unavailable')
        else:
            alias.symlink_to(self.base, target_is_directory=True)
        try:
            with patch.object(fixture.tempfile, 'gettempdir', return_value=str(alias)):
                with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_boundary$'):
                    fixture.ensure(self.lab)
        finally:
            if os.name == 'nt':
                alias.rmdir()  # Exact owned junction only, no recursive target operation.
            else:
                alias.unlink()
        self.assertEqual(0, self.lab.private_directory.call_count)
        self.assertFalse(self.path.exists())

    @unittest.skipIf(os.name == 'nt', 'POSIX owner/mode applicable on POSIX')
    def test_posix_current_owner_700_existing_root_never_chmods_open_object(self):
        self.existing()
        object_file = self.path / 'owned.object'; object_file.write_bytes(b'fictional-retained')
        with patch.object(fixture, '_windows', return_value=False), patch.object(Path, 'chmod', side_effect=AssertionError('existing_chmod_forbidden')):
            with object_file.open('rb') as held:
                self.assertTrue(fixture.ensure(self.lab) == self.path, 'owned_path_changed')
                self.assertEqual(b'fictional-retained', held.read())
        self.assertEqual(0, self.lab.private_directory.call_count)

    @unittest.skipIf(os.name == 'nt', 'POSIX owner/mode applicable on POSIX')
    def test_posix_widened_mode_is_rejected_and_left_unchanged(self):
        self.existing(); self.path.chmod(0o750)
        with patch.object(fixture, '_windows', return_value=False):
            with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_private$'):
                fixture.ensure(self.lab)
        self.assertEqual(0o750, stat.S_IMODE(self.path.stat().st_mode))
        self.assertEqual(0, self.lab.private_directory.call_count)

    @unittest.skipIf(os.name == 'nt', 'POSIX owner/mode applicable on POSIX')
    def test_posix_foreign_effective_uid_is_rejected_without_chown_or_repair(self):
        self.existing(); metadata = fixture._metadata
        def other_owner(path):
            value = metadata(path)
            if path != self.path:
                return value
            return SimpleNamespace(st_uid=os.geteuid() + 1, st_mode=value.st_mode,
                                   st_dev=value.st_dev, st_ino=value.st_ino)
        with patch.object(fixture, '_windows', return_value=False), patch.object(fixture, '_metadata', side_effect=other_owner):
            with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_private$'):
                fixture.ensure(self.lab)
        self.assertEqual(0, self.lab.private_directory.call_count)

    @unittest.skipUnless(os.name == 'nt', 'Windows native ACL applies on Windows')
    def test_windows_native_fresh_once_then_existing_exclusive_child_never_restricts(self):
        def restrict(path):
            command = ['pwsh', '-NoProfile', '-File', str(Path(__file__).parent / 'oidc-lab-private.ps1'),
                       '-Action', 'restrict', '-Directory', str(path)]
            result = subprocess.run(command, capture_output=True)
            if result.returncode:
                self.fail('owned_fresh_native_restriction_failed')
        self.lab.private_directory.side_effect = restrict
        with patch.object(fixture, 'WindowsAcl', NativeWindowsAcl):
            fixture.ensure(self.lab)
            self.assertTrue(self.lab.private_directory.call_count == 1 and self.lab.private_directory.call_args.args == (self.path,), 'fresh_restriction_contract')
            backend = NativeWindowsAcl(); before = backend.snapshot(self.path)
            object_file = self.path / 'held.object'; object_file.write_bytes(b'fictional-held-object')
            kernel = c.WinDLL('kernel32', use_last_error=True)
            create = kernel.CreateFileW
            create.restype = w.HANDLE
            create.argtypes = [w.LPCWSTR, w.DWORD, w.DWORD, w.LPVOID, w.DWORD, w.DWORD, w.HANDLE]
            close = kernel.CloseHandle; close.restype = w.BOOL; close.argtypes = [w.HANDLE]
            handle = create(str(object_file), 0xC0000000, 0, None, 3, 0, None)
            self.assertTrue(handle != c.c_void_p(-1).value, 'owned_exclusive_child_open_failed')
            try:
                self.lab.private_directory.side_effect = AssertionError('existing_restrict_forbidden')
                fixture.ensure(self.lab); fixture.ensure(self.lab)
                self.assertTrue(before == backend.snapshot(self.path), 'existing_acl_changed')
                self.assertTrue(self.lab.private_directory.call_count == 1 and self.lab.private_directory.call_args.args == (self.path,), 'fresh_restriction_contract')
            finally:
                close(handle)
            self.assertEqual(b'fictional-held-object', object_file.read_bytes())

    @unittest.skipUnless(os.name == 'nt', 'Windows native ACL applies on Windows')
    def test_windows_native_broader_existing_acl_is_not_repaired(self):
        self.existing()
        with patch.object(fixture, 'WindowsAcl', NativeWindowsAcl):
            backend = NativeWindowsAcl()
            # Arrange a deliberately invalid OBJECT-root ACL only on this owned
            # temporary fixture: Firefox profile's three-actor policy is broader.
            backend.protect(self.path)
            before = backend.snapshot(self.path)
            self.assertEqual(3, len(before['rules']))
            with patch.object(fixture, 'WindowsAcl', return_value=backend), patch.object(backend, 'protect', side_effect=AssertionError('existing_acl_write_forbidden')):
                with self.assertRaisesRegex(RuntimeError, '^source_bundle_object_acl$'):
                    fixture.ensure(self.lab)
            self.assertTrue(before == backend.snapshot(self.path), 'unsafe_existing_acl_was_changed')
        self.assertEqual(0, self.lab.private_directory.call_count)


class BundleObjectRootDelegationTests(unittest.TestCase):
    """Retain the four existing source-bundle regressions under the corrected contract."""
    def inputs(self, base):
        repository = base / 'repository'; repository.mkdir()
        project = 'poseidon-lab-' + hashlib.sha256(str(repository).encode()).hexdigest()[:12]
        sid = 'S-1-5-21-1-2-3-1001'
        snapshot = {'owner': sid, 'protected': True, 'rules': [
            {'sid': sid, 'type': 0, 'flags': 3, 'mask': fixture.FULL_CONTROL}]}
        native = Mock(current_user=sid); native.snapshot.side_effect = lambda _: deepcopy(snapshot)
        return repository, project, native

    def test_dedicated_project_path_outside_repo_is_restricted_before_return(self):
        with tempfile.TemporaryDirectory(prefix='poseidon-object-delegate-') as directory:
            temporary = Path(directory).resolve(); repository, project, native = self.inputs(temporary)
            events = []
            def restricted(path):
                events.append('RESTRICTED')
            def snapshot(_):
                self.assertEqual(['RESTRICTED'], events)
                return {'owner': native.current_user, 'protected': True, 'rules': [
                    {'sid': native.current_user, 'type': 0, 'flags': 3, 'mask': fixture.FULL_CONTROL}]}
            native.snapshot.side_effect = snapshot
            with patch.object(fixture.tempfile, 'gettempdir', return_value=str(temporary)), patch.object(bundle.lab, 'ROOT', repository), patch.object(bundle.lab, 'PROJECT', project), patch.object(bundle.lab, 'private_directory', side_effect=restricted) as restrict, patch.object(fixture, '_windows', return_value=True), patch.object(fixture, 'WindowsAcl', return_value=native):
                result = bundle.source_object_root()
                expected = temporary / (project + '-source-bundle-objects')
                self.assertTrue(result == expected, 'dedicated_path_changed')
                self.assertFalse(result.resolve().is_relative_to(repository.resolve()))
                self.assertTrue(restrict.call_count == 1 and restrict.call_args.args == (result,), 'fresh_restriction_contract')

    def test_repo_temp_override_is_rejected_without_creating_or_restricting_storage(self):
        with tempfile.TemporaryDirectory(prefix='poseidon-object-delegate-') as directory:
            temporary = Path(directory).resolve(); repository, project, _ = self.inputs(temporary)
            with patch.object(fixture.tempfile, 'gettempdir', return_value=str(repository)), patch.object(bundle.lab, 'ROOT', repository), patch.object(bundle.lab, 'PROJECT', project), patch.object(bundle.lab, 'private_directory') as restrict:
                with self.assertRaisesRegex(RuntimeError, 'object_boundary'):
                    bundle.source_object_root()
                self.assertEqual(0, restrict.call_count)
                self.assertFalse((repository / (project + '-source-bundle-objects')).exists())

    def test_another_repository_ancestor_is_also_rejected(self):
        with tempfile.TemporaryDirectory(prefix='poseidon-object-delegate-') as directory:
            temporary = Path(directory).resolve(); repository, project, _ = self.inputs(temporary)
            (temporary / '.git').write_text('synthetic git marker', encoding='utf-8')
            with patch.object(fixture.tempfile, 'gettempdir', return_value=str(temporary)), patch.object(bundle.lab, 'ROOT', repository), patch.object(bundle.lab, 'PROJECT', project), patch.object(bundle.lab, 'private_directory') as restrict:
                with self.assertRaisesRegex(RuntimeError, 'object_boundary'):
                    bundle.source_object_root()
                self.assertEqual(0, restrict.call_count)
                self.assertFalse((temporary / (project + '-source-bundle-objects')).exists())

    def test_existing_objects_are_retained_and_acl_is_validated_without_reapplication(self):
        with tempfile.TemporaryDirectory(prefix='poseidon-object-delegate-') as directory:
            temporary = Path(directory).resolve(); repository, project, native = self.inputs(temporary)
            objects = temporary / (project + '-source-bundle-objects'); objects.mkdir(mode=0o700)
            original = objects / 'retained.object'; original.write_bytes(b'synthetic retained transport')
            with patch.object(fixture.tempfile, 'gettempdir', return_value=str(temporary)), patch.object(bundle.lab, 'ROOT', repository), patch.object(bundle.lab, 'PROJECT', project), patch.object(bundle.lab, 'private_directory') as restrict, patch.object(fixture, '_windows', return_value=True), patch.object(fixture, 'WindowsAcl', return_value=native):
                self.assertTrue(objects == bundle.source_object_root(), 'retained_object_root_changed')
                self.assertEqual(0, restrict.call_count)
                self.assertEqual(b'synthetic retained transport', original.read_bytes())
                self.assertEqual(2, native.snapshot.call_count)
                self.assertEqual(0, native.protect.call_count)


if __name__ == '__main__':
    unittest.main()
