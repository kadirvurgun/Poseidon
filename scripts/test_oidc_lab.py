"""Fixture contract tests; real browser/provider execution is a separate mandatory gate."""
import json
from pathlib import Path
import tempfile
import os
import unittest
from unittest.mock import patch

import oidc_lab as lab


class LabTests(unittest.TestCase):
    def test_register_enrollment_is_explicit_server_owned_idempotent_and_preserves_other_members(self):
        realm, _, _, config = lab.make_configuration()
        with tempfile.TemporaryDirectory() as directory, patch.object(lab, 'PRIVATE', Path(directory)):
            (Path(directory) / 'realm').mkdir()
            (Path(directory) / 'realm/poseidon-lab-realm.json').write_text(json.dumps(realm))
            original = json.loads(json.dumps(config))
            lab.register_membership(config)
            lab.register_membership(config)
            members = config['Authentication']['Oidc']['Memberships']
            self.assertEqual(original['Authentication']['Oidc']['Memberships'][1], members[1])
            self.assertEqual(original['Authentication']['Oidc']['Memberships'][0]['Subject'], members[0]['Subject'])
            self.assertEqual(1, members[0]['Permissions'].count('regulatory.draft.write'))
            self.assertIn('regulatory.draft.read', members[0]['Permissions'])
            members[0]['Subject'] = 'untrusted'
            with self.assertRaisesRegex(RuntimeError, 'lab_manager_scope_invalid'):
                lab.register_membership(config)

    def test_lab_environment_excludes_ambient_memberships_database_and_listeners(self):
        ambient = {'Authentication__Oidc__Memberships__9__Subject': 'ambient',
                   'ConnectionStrings__Poseidon': 'ambient', 'Kestrel__Endpoints__Other__Url': 'http://0.0.0.0:9999',
                   'ASPNETCORE_URLS': 'http://0.0.0.0:9998', 'POSEIDON_DATABASE': 'ambient'}
        with patch.dict(os.environ, ambient), patch.object(lab, 'root_tool', return_value=str(Path('synthetic-tools/dotnet.exe').resolve())):
            env = lab.environment()
        self.assertFalse(any(key in env for key in ambient))
        self.assertEqual('Development', env['ASPNETCORE_ENVIRONMENT'])
        self.assertEqual(str(lab.PRIVATE / 'appsettings.json'), env['POSEIDON_LAB_CONFIG'])

    def test_host_prefixes_and_configuration_delimiters_cannot_extend_lab_configuration(self):
        for prefix in ('', 'DOTNET_', 'ASPNETCORE_', 'dotnet_', 'aspnetcore_'):
            for delimiter in ('__', ':'):
                for key in ('Kestrel:Endpoints:Other:Url', 'Authentication:Oidc:Memberships:9:Subject',
                            'ConnectionStrings:Other', 'DevelopmentLab:WebRoot', 'URLS', 'HTTP_PORTS', 'HTTPS_PORTS'):
                    ambient_key = prefix + key.replace(':', delimiter)
                    with self.subTest(prefix=prefix, delimiter=delimiter, key=key), \
                         patch.dict(os.environ, {ambient_key: 'ambient', 'LAB_TEST_UNRELATED': 'preserved'}), \
                         patch.object(lab, 'root_tool', return_value=str(Path('synthetic-tools/dotnet.exe').resolve())):
                        env = lab.environment()
                        self.assertNotIn(ambient_key, env)
                        self.assertEqual('preserved', env['LAB_TEST_UNRELATED'])

    def test_generated_credentials_and_subjects_are_random_and_server_scope_is_explicit(self):
        realm, credentials, password, config = lab.make_configuration()
        other, other_credentials, other_password, _ = lab.make_configuration()
        self.assertNotEqual(credentials, other_credentials)
        self.assertNotEqual(password, other_password)
        self.assertNotEqual(realm['users'][0]['id'], other['users'][0]['id'])
        client = realm['clients'][0]
        self.assertFalse(client['directAccessGrantsEnabled'])
        self.assertFalse(client['publicClient'])
        self.assertEqual([lab.APP + '/signin-oidc'], client['redirectUris'])
        self.assertEqual('S256', client['attributes']['pkce.code.challenge.method'])
        memberships = config['Authentication']['Oidc']['Memberships']
        self.assertEqual([u['id'] for u in realm['users']], [m['Subject'] for m in memberships])
        self.assertTrue(memberships[0]['TenantWideManage'])
        self.assertFalse(memberships[1]['TenantWideRead'])
        self.assertEqual(['directory.read'], memberships[1]['Permissions'])
        self.assertEqual(1, len(memberships[1]['VesselGrants']))
        self.assertNotIn('realmRoles', json.dumps(realm))
        self.assertTrue(all(u['firstName'] == 'Synthetic' and u['email'].endswith('@example.invalid') and not u['emailVerified'] for u in realm['users']))
        self.assertGreaterEqual(len(password), 48)

    def test_restart_preserves_private_files_and_partial_bootstrap_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(lab, 'PRIVATE', Path(directory)):
            path = Path(directory)
            (path / 'operator-data').write_text('preserve')
            with self.assertRaisesRegex(RuntimeError, 'partial_configuration_preserved'):
                lab.init()
            (path / 'ready').write_text('DEV-OIDC-LAB-01')
            with patch.object(lab, 'make_configuration', side_effect=AssertionError('must not regenerate')):
                lab.init()
            self.assertEqual('preserve', (path / 'operator-data').read_text())

    def test_stop_uses_only_owned_project_without_volume_deletion(self):
        with patch.object(lab, 'owned_process') as owned, patch.object(lab, 'compose') as compose:
            lab.stop()
            owned.assert_called_once_with('stop')
            compose.assert_called_once_with('down')


if __name__ == '__main__':
    unittest.main()
