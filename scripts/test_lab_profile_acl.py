"""Cross-platform ownership/readback tests; actual native Windows proof is separate."""
from copy import deepcopy
from pathlib import Path
import tempfile
import os
import subprocess
import unittest
from unittest.mock import Mock, patch
import uuid

import lab_profile_acl as lab


class ProfileAclTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='poseidon-profile-boundary-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.private = self.root / 'private'
        self.private.mkdir()
        self.owner = str(uuid.uuid4())
        self.directory = self.private / 'strict-profile-OWNED'
        self.directory.mkdir()
        self.profile = self.directory / 'profile'
        self.profile.mkdir()
        self.marker = self.directory / '.owner'
        self.marker.write_text(self.owner, encoding='utf-8')
        self.secret = self.private / 'credential-sentinel'
        self.secret.write_bytes(b'fictional-unchanged-sentinel')
        self.current = 'S-1-5-21-1-2-3-1001'
        self.snapshot = {'owner': self.current, 'protected': True, 'rules': [
            {'sid': sid, 'type': 0, 'flags': 3, 'mask': lab.FULL_CONTROL}
            for sid in (self.current, lab.SYSTEM, lab.ADMINISTRATORS)]}

    def test_only_empty_owned_pair_is_returned(self):
        self.assertEqual((self.directory, self.profile), lab.owned_profile(self.profile, self.owner, self.private))

    def test_wrong_owner_marker_refuses_native_mutation(self):
        self.marker.write_text(str(uuid.uuid4()), encoding='utf-8')
        with patch.object(lab, 'PRIVATE', self.private), patch.object(lab, 'WindowsAcl') as native:
            with self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
                lab.prepare(self.profile, self.owner)
            native.assert_not_called()

    def test_empty_noncanonical_and_malformed_owner_refused(self):
        for owner in ('', str(uuid.UUID(int=0)), self.owner.upper(), 'malformed'):
            with self.subTest(owner=owner), self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
                lab.owned_profile(self.profile, owner, self.private)

    def test_credential_root_and_sibling_root_refused(self):
        sibling = self.root / 'sibling'
        sibling.mkdir()
        for target in (self.private, self.secret, sibling):
            with self.subTest(target=target.name), self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
                lab.owned_profile(target, self.owner, self.private)

    def test_parent_path_escape_refused(self):
        escaped = self.private / '..' / 'sibling'
        escaped.mkdir()
        with self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
            lab.owned_profile(escaped, self.owner, self.private)

    def test_nonempty_profile_refuses_native_mutation(self):
        (self.profile / 'auth-data').write_bytes(b'fictional')
        with patch.object(lab, 'PRIVATE', self.private), patch.object(lab, 'WindowsAcl') as native:
            with self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
                lab.prepare(self.profile, self.owner)
            native.assert_not_called()

    def test_extra_parent_file_refused(self):
        (self.directory / 'unknown').write_bytes(b'fictional')
        with self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
            lab.owned_profile(self.profile, self.owner, self.private)

    def test_reparse_or_alias_refused(self):
        with patch.object(Path, 'is_symlink', return_value=True):
            with self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
                lab.owned_profile(self.profile, self.owner, self.private)

    def test_owner_marker_alias_refused(self):
        with patch.object(Path, 'is_symlink', lambda path: path == self.marker):
            with self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
                lab.owned_profile(self.profile, self.owner, self.private)

    def test_owner_marker_hardlink_refuses_native_writes(self):
        external = self.root / 'external-owner'
        external.write_text(self.owner, encoding='utf-8')
        self.marker.unlink()
        os.link(external, self.marker)
        with patch.object(lab, 'PRIVATE', self.private), patch.object(lab, 'WindowsAcl') as native:
            with self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
                lab.prepare(self.profile, self.owner)
            native.assert_not_called()
        self.assertEqual(self.owner, external.read_text(encoding='utf-8'))

    def test_real_parent_junction_or_symlink_refused(self):
        alias = self.private / 'strict-profile-ALIAS'
        if os.name == 'nt':
            subprocess.run(['cmd.exe', '/c', 'mklink', '/J', str(alias), str(self.directory)],
                           check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        else:
            alias.symlink_to(self.directory, target_is_directory=True)
        try:
            with self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
                lab.owned_profile(alias / 'profile', self.owner, self.private)
        finally:
            if os.name == 'nt':
                alias.rmdir()
            else:
                alias.unlink()

    def test_exact_privileged_allowlist_accepted(self):
        lab.validate_acl(self.snapshot, self.current)

    def test_broad_inherited_or_duplicate_grants_refused(self):
        for sid in ('S-1-1-0', 'S-1-5-32-545', 'S-1-15-2-1', self.current):
            candidate = deepcopy(self.snapshot)
            candidate['rules'].append({'sid': sid, 'type': 0, 'flags': 3, 'mask': lab.FULL_CONTROL})
            with self.subTest(sid=sid), self.assertRaisesRegex(RuntimeError, '^profile_acl_readback$'):
                lab.validate_acl(candidate, self.current)

    def test_wrong_owner_unprotected_missing_or_deny_ace_refused(self):
        faults = [('owner', lab.SYSTEM), ('protected', False), ('rules', self.snapshot['rules'][:2])]
        for field, value in faults:
            candidate = deepcopy(self.snapshot)
            candidate[field] = value
            with self.subTest(field=field), self.assertRaisesRegex(RuntimeError, '^profile_acl_readback$'):
                lab.validate_acl(candidate, self.current)
        for field, value in (('type', 1), ('flags', 19), ('flags', 0), ('mask', 0x120089)):
            candidate = deepcopy(self.snapshot)
            candidate['rules'][0][field] = value
            with self.subTest(field=field, value=value), self.assertRaisesRegex(RuntimeError, '^profile_acl_readback$'):
                lab.validate_acl(candidate, self.current)

    def test_only_owned_pair_changed_and_credential_bytes_preserved(self):
        backend = Mock(current_user=self.current)
        backend.snapshot.return_value = self.snapshot
        before = self.secret.read_bytes()
        with patch.object(lab, 'PRIVATE', self.private), patch.object(lab, 'WindowsAcl', return_value=backend):
            lab.prepare(self.profile, self.owner)
        self.assertEqual([self.directory, self.profile], [call.args[0] for call in backend.protect.call_args_list])
        self.assertEqual(before, self.secret.read_bytes())
        self.assertEqual(self.owner, self.marker.read_text(encoding='utf-8'))

    def test_failed_readback_stops_before_second_acl_write(self):
        backend = Mock(current_user=self.current)
        backend.snapshot.return_value = {**self.snapshot, 'protected': False}
        with patch.object(lab, 'PRIVATE', self.private), patch.object(lab, 'WindowsAcl', return_value=backend):
            with self.assertRaisesRegex(RuntimeError, '^profile_acl_readback$'):
                lab.prepare(self.profile, self.owner)
        backend.protect.assert_called_once_with(self.directory)

    def test_owner_rechecked_between_native_writes(self):
        backend = Mock(current_user=self.current)
        backend.snapshot.return_value = self.snapshot
        backend.protect.side_effect = lambda _: self.marker.write_text(str(uuid.uuid4()), encoding='utf-8')
        with patch.object(lab, 'PRIVATE', self.private), patch.object(lab, 'WindowsAcl', return_value=backend):
            with self.assertRaisesRegex(RuntimeError, '^profile_ownership$'):
                lab.prepare(self.profile, self.owner)
        backend.protect.assert_called_once_with(self.directory)


if __name__ == '__main__':
    unittest.main()
