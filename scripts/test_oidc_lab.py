"""Fixture contract tests; real browser/provider execution is a separate mandatory gate."""
import json
from pathlib import Path
import tempfile
import os
import unittest
from unittest.mock import patch

import oidc_lab as lab


class LabTests(unittest.TestCase):
    def test_lab_environment_excludes_ambient_memberships_database_and_listeners(self):
        ambient = {'Authentication__Oidc__Memberships__9__Subject': 'ambient',
                   'ConnectionStrings__Poseidon': 'ambient', 'Kestrel__Endpoints__Other__Url': 'http://0.0.0.0:9999',
                   'ASPNETCORE_URLS': 'http://0.0.0.0:9998', 'POSEIDON_DATABASE': 'ambient'}
        with patch.dict(os.environ, ambient), patch.object(lab, 'root_tool', return_value=str(Path('synthetic-tools/dotnet.exe').resolve())):
            env = lab.environment()
        self.assertFalse(any(key in env for key in ambient))
        self.assertEqual('Development', env['ASPNETCORE_ENVIRONMENT'])
        self.assertEqual(str(lab.PRIVATE / 'appsettings.json'), env['POSEIDON_LAB_CONFIG'])

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
