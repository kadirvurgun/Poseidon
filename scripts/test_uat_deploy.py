"""UAT deployment tooling (infra/uat): feedback trailers, membership profiles, navigation roles."""
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
UAT = ROOT / 'infra/uat'


def load(name: str):
    spec = importlib.util.spec_from_file_location('uat_' + name, UAT / f'{name}.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


notify = load('notify_feedback')
manage = load('manage')


def capabilities() -> dict:
    out = subprocess.run([sys.executable, str(UAT / 'extract_capabilities.py')], check=True, capture_output=True, text=True)
    return json.loads(out.stdout)


class FeedbackTrailerTests(unittest.TestCase):
    def test_reads_only_explicit_uat_fixes_trailers(self):
        text = 'fix(x): thing\n\nUAT-Fixes: #12, #15\nRefs #99\nFixes #7\n'
        self.assertEqual(notify.referenced(text), {12, 15})

    def test_trailer_is_case_insensitive_and_multi_line(self):
        self.assertEqual(notify.referenced('uat-fixes: #3\nbody\nUAT-FIXES:#4'), {3, 4})

    def test_closing_keywords_never_count(self):
        self.assertEqual(notify.referenced('Closes kadirvurgun/Poseidon-UAT#5\nResolves #6'), set())

    def test_first_deploy_and_rollback_announce_nothing(self):
        self.assertEqual(notify.collect('', 'a' * 40, 'o/r'), set())
        with mock.patch.object(notify.subprocess, 'run', return_value=mock.Mock(returncode=1)):
            self.assertEqual(notify.collect('b' * 40, 'a' * 40, 'o/r'), set())

    def test_invalid_sha_is_rejected_before_any_call(self):
        with mock.patch.object(sys, 'argv', ['x', '--to', 'main', '--tag', 't', '--repo', 'o/r']):
            self.assertEqual(notify.main(), 2)


class CapabilityTests(unittest.TestCase):
    def test_extracts_closed_lists_from_source(self):
        caps = capabilities()
        self.assertIn('directory.read', caps['permissions'])
        self.assertIn('membership.manage', caps['permissions'])
        self.assertIn('today', caps['navigationKeys'])
        self.assertEqual(len(set(caps['permissions'])), len(caps['permissions']))

    def test_profiles_never_include_human_authority_permissions(self):
        profiles = manage.default_profiles(capabilities())
        authority = ['pilot.gate.decide', 'uat.attest', 'human.decide', 'evidence.verify', 'crew.safe_manning.decide',
                     'import.reconciliation.sign', 'departure.decide', 'record.purge.approve', 'config.approve',
                     'certificate.verify.record', 'assurance.import.accept', 'applicability.override.author',
                     'tug.owner.eligible', 'certificate.renewal.owner', 'config.reject']
        for name in ('admin', 'po', 'viewer'):
            for permission in authority:
                self.assertNotIn(permission, profiles[name], (name, permission))
        for permission in authority:
            self.assertIn(permission, profiles['excludedAuthorityPermissions'])

    def test_every_profile_can_sign_in_and_only_admin_manages_members(self):
        profiles = manage.default_profiles(capabilities())
        for name in ('admin', 'po', 'viewer'):
            self.assertIn('directory.read', profiles[name])
        self.assertIn('membership.manage', profiles['admin'])
        self.assertNotIn('membership.manage', profiles['po'])
        self.assertTrue(all(p.endswith('.read') or p in ('workspace.search', 'mode.tug', 'mode.cross', 'notification.acknowledge')
                            for p in profiles['viewer']))

    def test_navigation_roles_respect_api_budgets_and_cover_every_key(self):
        caps = capabilities()
        roles, catalog, nav = manage.navigation(caps)
        self.assertLessEqual(len(roles), 16)
        self.assertEqual(len(set(roles)), len(roles))
        covered = [k for m in nav['Mappings'] for k in m['Keys']]
        self.assertEqual(sorted(covered), sorted(caps['navigationKeys']))
        self.assertTrue(all(1 <= len(m['Keys']) <= 16 for m in nav['Mappings']))
        self.assertEqual([r['Reference'] for r in catalog['Roles']], roles)
        self.assertEqual(roles, manage.navigation(caps)[0], 'role references must be stable across renders')


class RenderTests(unittest.TestCase):
    def test_render_binds_exact_issuer_and_secret_references_only(self):
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp)
            (home / '.env').write_text('UAT_DOMAIN=uat.example.com\n')
            (home / 'capabilities.json').write_text(json.dumps(capabilities()))
            (home / 'state').mkdir()
            (home / 'state/users.json').write_text(json.dumps([
                {'username': 'po1', 'profile': 'po', 'subject': 's-1', 'actorId': '00000000-0000-0000-0000-0000000000a1'},
                {'username': 'v1', 'profile': 'viewer', 'subject': 's-2', 'actorId': '00000000-0000-0000-0000-0000000000a2'}]))
            with mock.patch.multiple(manage, HOME=home, STATE=home / 'state', CONFIG=home / 'config'):
                manage.render()
            config = json.loads((home / 'config/appsettings.Staging.json').read_text())
        oidc = config['Authentication']['Oidc']
        self.assertEqual(oidc['Authority'], 'https://uat.example.com/auth/realms/poseidon')
        self.assertEqual(oidc['Issuer'], oidc['Authority'])
        self.assertNotIn('ClientSecret', oidc)
        self.assertEqual(oidc['ClientSecretReference'], 'POSEIDON_OIDC_CLIENT_SECRET')
        self.assertEqual(config['RateLimiting'], {'Store': 'Postgres', 'PartitionKeyReference': 'POSEIDON_RATE_LIMIT_KEY'})
        po, viewer = oidc['Memberships']
        self.assertTrue(all(m['Issuer'] == oidc['Issuer'] for m in (po, viewer)))
        self.assertTrue(po['TenantWideManage'])
        self.assertFalse(viewer['TenantWideManage'])
        self.assertTrue(1 <= len(po['RoleReferences']) <= 16)
        flat = json.dumps(config).lower()
        self.assertNotIn('"clientsecret"', flat)
        self.assertNotIn('password', flat)
        self.assertNotIn('"partitionkey"', flat)


if __name__ == '__main__':
    unittest.main()
