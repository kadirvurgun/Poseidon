"""Authored source protocol tests; no provider, HTTP, storage or acceptance proof."""
from copy import deepcopy
from datetime import datetime, timezone
import secrets
import unittest
from unittest.mock import patch
import uuid

import bundle_crew_boundary_fixture as boundary
import bundle_fixture_plan as plan
import oidc_lab as lab


class CrewBoundaryFixtureTests(unittest.TestCase):
    def fixture(self):
        realm, credentials, _, config = lab.make_configuration()
        subject, actor = str(uuid.uuid4()), str(uuid.uuid4())
        password = secrets.token_urlsafe(32)
        credentials['lab-foreign'] = {'username': 'lab-foreign', 'password': password}
        realm['users'].append({'id': subject, 'username': 'lab-foreign', 'enabled': True,
            'credentials': [{'type': 'password', 'value': password, 'temporary': False}]})
        config['Authentication']['Oidc']['Memberships'].append({'Issuer': lab.AUTHORITY, 'Subject': subject,
            'ActorId': actor, 'TenantId': plan.FOREIGN_TENANT, 'Permissions': ['directory.read'],
            'TenantWideRead': False, 'TenantWideManage': False,
            'VesselGrants': [{'VesselId': plan.FOREIGN_SHIP, 'CanEdit': False}]})
        config, metadata = plan.configure(config, realm, lab.AUTHORITY, lab.PROJECT, str(uuid.uuid4()),
            datetime(2026, 11, 13, tzinfo=timezone.utc))
        return config, realm, credentials, metadata

    def test_returns_copies_preserving_all_original_identities_credentials_grants_and_policies(self):
        config, realm, credentials, metadata = self.fixture()
        before = deepcopy((config, realm, credentials, metadata))
        copied, imported, private = boundary.configure(config, realm, credentials, metadata)
        self.assertEqual(before, (config, realm, credentials, metadata))
        self.assertEqual(config['Authentication']['Oidc']['Memberships'], copied['Authentication']['Oidc']['Memberships'][:4])
        self.assertEqual(realm['users'], imported['users'][:4])
        self.assertEqual(credentials, {key: value for key, value in private.items() if key != boundary.ACCOUNT})
        original = deepcopy(copied); original['Authentication']['Oidc']['Memberships'].pop()
        self.assertEqual(config, original)
        copied['Authentication']['Oidc']['Memberships'][0]['Permissions'].append('changed-copy-only')
        imported['users'][0]['enabled'] = False
        private['lab-manager']['username'] = 'changed-copy-only'
        self.assertEqual(before, (config, realm, credentials, metadata))

    def test_exact_six_capabilities_one_original_ship_and_audit_role_without_other_authority(self):
        config, realm, credentials, metadata = self.fixture()
        copied, imported, private = boundary.configure(config, realm, credentials, metadata)
        member = copied['Authentication']['Oidc']['Memberships'][-1]
        self.assertEqual(list(boundary.PERMISSIONS), member['Permissions'])
        self.assertEqual({'Issuer','Subject','ActorId','TenantId','Permissions','TenantWideRead','TenantWideManage',
                          'VesselGrants','RoleReferences','ActorKind'}, set(member))
        self.assertIs(member['TenantWideRead'], False); self.assertIs(member['TenantWideManage'], False)
        self.assertEqual([{'VesselId': metadata['vesselId'], 'CanEdit': True}], member['VesselGrants'])
        self.assertEqual(1, len(member['RoleReferences']))
        self.assertEqual(metadata['tenantId'], member['TenantId'])
        self.assertEqual(lab.AUTHORITY, member['Issuer'])
        self.assertEqual({u['id'] for u in imported['users']}, {m['Subject'] for m in copied['Authentication']['Oidc']['Memberships']})
        self.assertEqual(5, len(private)); self.assertNotIn('HumanAuthorities', member)
        user = imported['users'][-1]
        self.assertEqual(member['Subject'], user['id'])
        self.assertEqual(private[boundary.ACCOUNT]['password'], user['credentials'][0]['value'])

    def test_same_in_memory_run_is_idempotent_without_rotating_private_credential(self):
        config, realm, credentials, metadata = self.fixture()
        copied, imported, private = boundary.configure(config, realm, credentials, metadata)
        before = deepcopy((copied, imported, private))
        with patch.object(boundary.secrets, 'token_urlsafe', side_effect=AssertionError('must not rotate')):
            again = boundary.configure(copied, imported, private, metadata)
        self.assertEqual(before, again); self.assertEqual(before, (copied, imported, private))

    def test_different_run_cannot_adopt_an_existing_persona(self):
        config, realm, credentials, metadata = self.fixture()
        copied, imported, private = boundary.configure(config, realm, credentials, metadata)
        other = {**metadata, 'runId': str(uuid.uuid4())}
        with self.assertRaisesRegex(RuntimeError, '^crew_boundary_fixture_invalid$'):
            boundary.configure(copied, imported, private, other)

    def test_own_persona_rejects_conflicting_membership_shape_scope_grants_or_rights_without_mutation(self):
        config, realm, credentials, metadata = self.fixture()
        baseline = boundary.configure(config, realm, credentials, metadata)
        mutations = [lambda m: m['Permissions'].append('crew.profile.contact.read'),
            lambda m: m.__setitem__('TenantWideRead', True), lambda m: m.__setitem__('TenantWideManage', True),
            lambda m: m.__setitem__('TenantWideRead', 0),
            lambda m: m['VesselGrants'].append({'VesselId': metadata['foreignVesselId'], 'CanEdit': True}),
            lambda m: m['VesselGrants'][0].__setitem__('CanEdit', False),
            lambda m: m['VesselGrants'][0].__setitem__('CanEdit', 1),
            lambda m: m.__setitem__('HumanAuthorities', []),
            lambda m: m.__setitem__('RoleReferences', [str(uuid.uuid4())]),
            lambda m: m.__setitem__('TenantId', metadata['foreignTenantId'])]
        for change in mutations:
            with self.subTest(mutation=mutations.index(change)):
                copied, imported, private = deepcopy(baseline)
                change(copied['Authentication']['Oidc']['Memberships'][-1])
                before = deepcopy((copied, imported, private))
                with self.assertRaisesRegex(RuntimeError, '^crew_boundary_fixture_invalid$'):
                    boundary.configure(copied, imported, private, metadata)
                self.assertEqual(before, (copied, imported, private))

    def test_own_user_or_private_credential_cannot_conflict_or_gain_unreviewed_fields(self):
        config, realm, credentials, metadata = self.fixture()
        baseline = boundary.configure(config, realm, credentials, metadata)
        for change in ('disabled', 'enabled-int', 'email-int', 'temporary-int', 'role', 'private-field', 'password', 'missing-private', 'missing-user'):
            with self.subTest(change=change):
                copied, imported, private = deepcopy(baseline)
                if change == 'disabled': imported['users'][-1]['enabled'] = False
                if change == 'enabled-int': imported['users'][-1]['enabled'] = 1
                if change == 'email-int': imported['users'][-1]['emailVerified'] = 0
                if change == 'temporary-int': imported['users'][-1]['credentials'][0]['temporary'] = 0
                if change == 'role': imported['users'][-1]['realmRoles'] = ['unreviewed']
                if change == 'private-field': private[boundary.ACCOUNT]['token'] = 'unexpected'
                if change == 'password': private[boundary.ACCOUNT]['password'] = secrets.token_urlsafe(32)
                if change == 'missing-private': del private[boundary.ACCOUNT]
                if change == 'missing-user': imported['users'].pop()
                before = deepcopy((copied, imported, private))
                with self.assertRaisesRegex(RuntimeError, '^crew_boundary_fixture_invalid$'):
                    boundary.configure(copied, imported, private, metadata)
                self.assertEqual(before, (copied, imported, private))

    def test_rejects_collision_with_every_existing_subject_actor_or_role(self):
        for kind in ('subject', 'actor', 'audit-role'):
            with self.subTest(kind=kind):
                config, realm, credentials, metadata = self.fixture()
                member = config['Authentication']['Oidc']['Memberships'][-1]
                collision = boundary.identity(metadata['runId'], kind)
                if kind == 'subject':
                    member['Subject'] = collision; realm['users'][-1]['id'] = collision
                if kind == 'actor':
                    member['ActorId'] = collision; metadata['foreignActorId'] = collision
                if kind == 'audit-role': member['RoleReferences'] = [collision]
                with self.assertRaisesRegex(RuntimeError, '^crew_boundary_fixture_invalid$'):
                    boundary.configure(config, realm, credentials, metadata)

    def test_rejects_malformed_run_scope_original_actor_and_provider_pairing(self):
        for kind in ('run', 'nil', 'tenant', 'original-actor', 'issuer', 'duplicate-subject', 'unknown-account'):
            with self.subTest(kind=kind):
                config, realm, credentials, metadata = self.fixture()
                if kind == 'run': metadata['runId'] = True
                if kind == 'nil': metadata['vesselId'] = '00000000-0000-0000-0000-000000000000'
                if kind == 'tenant': metadata['foreignTenantId'] = metadata['tenantId']
                if kind == 'original-actor': metadata['writerActorId'] = str(uuid.uuid4())
                if kind == 'issuer': config['Authentication']['Oidc']['Issuer'] += '/different'
                if kind == 'duplicate-subject': config['Authentication']['Oidc']['Memberships'][1]['Subject'] = realm['users'][0]['id']
                if kind == 'unknown-account': credentials['unknown'] = {'username': 'unknown', 'password': secrets.token_urlsafe(32)}
                with self.assertRaises(RuntimeError): boundary.configure(config, realm, credentials, metadata)
