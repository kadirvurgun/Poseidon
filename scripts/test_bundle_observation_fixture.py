"""Authored source-fixture regressions; no runtime/provider/secret inputs. NOT RUN."""
from copy import deepcopy
import unittest

import bundle_observation_fixture as fixture


class BundleObservationFixtureTests(unittest.TestCase):
    def inputs(self):
        metadata = {'schemaVersion': 'poseidon.bundle-fixture-plan.v1',
            'runId': '70000000-0000-0000-0000-000000000001', 'tenantId': fixture.TENANT, 'vesselId': fixture.SHIP,
            'writerActorId': '80000000-0000-0000-0000-000000000001', 'writerRoleId': '90000000-0000-0000-0000-000000000001',
            'state': 'CONFIGURATION_ONLY_REQUIRES_REAL_LOGIN_AND_HTTP'}
        writer = {'ActorId': metadata['writerActorId'], 'TenantId': fixture.TENANT, 'Issuer': fixture.AUTHORITY,
            'Subject': '60000000-0000-0000-0000-000000000001', 'ActorKind': 'Human', 'RoleReferences': [metadata['writerRoleId']],
            'TenantWideRead': True, 'TenantWideManage': True, 'Permissions': sorted(fixture.BASE_PERMISSIONS)}
        config = {'ConnectionStrings': {'Poseidon': 'synthetic-owned-fixture'}, 'Authentication': {'Oidc': {
            'Issuer': fixture.AUTHORITY, 'Authority': fixture.AUTHORITY, 'ClientId': 'poseidon-local',
            'ClientSecret': 'synthetic-only', 'Memberships': [writer,
                {'ActorId': '80000000-0000-0000-0000-000000000002', 'Permissions': ['directory.read'], 'TenantId': fixture.TENANT},
                {'ActorId': '80000000-0000-0000-0000-000000000003', 'Permissions': ['directory.read'], 'TenantId': '10000000-0000-0000-0000-000000000002'}]}},
            'DailyOffline': {'Policies': [{'Version': 'preserved-synthetic-policy'}]}}
        return config, metadata

    def test_exact_synthetic_source_and_only_the_explicit_writer_grant_are_added_to_a_copy(self):
        config, metadata = self.inputs(); before = deepcopy(config)
        after = fixture.configure(config, metadata)
        self.assertEqual(before, config)
        expected = {'Sources': [{'TenantId': fixture.TENANT, 'VesselId': fixture.SHIP, 'AssetId': fixture.SHIP,
            'Source': 'SYNTHETIC-OBS-' + metadata['runId'], 'Version': 'SYNTHETIC-OBS-v1-' + metadata['runId'],
            'Kind': 'Meter', 'Unit': 'hours', 'FreshnessSeconds': 3600, 'MaxScale': 4}]}
        self.assertEqual(expected, after['MaintenanceObservations'])
        members = after['Authentication']['Oidc']['Memberships']
        self.assertEqual(before['Authentication']['Oidc']['Memberships'][1:], members[1:])
        self.assertEqual({fixture.PERMISSION}, set(members[0]['Permissions']) - set(before['Authentication']['Oidc']['Memberships'][0]['Permissions']))
        self.assertEqual(before['DailyOffline'], after['DailyOffline'])
        self.assertEqual(before['ConnectionStrings'], after['ConnectionStrings'])
        self.assertEqual(before['Authentication']['Oidc']['ClientSecret'], after['Authentication']['Oidc']['ClientSecret'])
        self.assertNotIn('HumanAuthorities', members[0]); self.assertNotIn('Observations', after)
        self.assertNotIn('Verified', repr(after['MaintenanceObservations']))

    def test_exact_same_run_is_idempotent_and_an_explicit_empty_source_array_is_supported(self):
        config, metadata = self.inputs(); config['MaintenanceObservations'] = {'Sources': []}
        once = fixture.configure(config, metadata)
        self.assertEqual(once, fixture.configure(once, metadata))
        self.assertEqual(1, once['Authentication']['Oidc']['Memberships'][0]['Permissions'].count(fixture.PERMISSION))

    def test_existing_operator_or_other_run_policy_is_rejected_without_any_partial_grant(self):
        for value in ({'Sources': [{'Source': 'operator-owned'}]}, {'Unexpected': []}, [], None, {'Sources': None}):
            config, metadata = self.inputs(); config['MaintenanceObservations'] = value; before = deepcopy(config)
            with self.assertRaisesRegex(RuntimeError, 'existing_policy_preserved'): fixture.configure(config, metadata)
            self.assertEqual(before, config)
        config, metadata = self.inputs(); configured = fixture.configure(config, metadata); before = deepcopy(configured)
        metadata['runId'] = '70000000-0000-0000-0000-000000000002'
        with self.assertRaisesRegex(RuntimeError, 'existing_policy_preserved'): fixture.configure(configured, metadata)
        self.assertEqual(before, configured)

    def test_malformed_nested_provider_or_role_data_has_only_closed_error_codes(self):
        for change in ('auth', 'provider', 'members', 'roles'):
            config, metadata = self.inputs()
            if change == 'auth': config['Authentication'] = None
            if change == 'provider': config['Authentication']['Oidc'] = []
            if change == 'members': config['Authentication']['Oidc']['Memberships'] = [None]
            if change == 'roles': config['Authentication']['Oidc']['Memberships'][0]['RoleReferences'] = [[]]
            before = deepcopy(config)
            with self.assertRaisesRegex(RuntimeError, '^observation_fixture_'): fixture.configure(config, metadata)
            self.assertEqual(before, config)

    def test_wrong_provider_actor_scope_role_or_missing_base_access_cannot_enroll(self):
        for change in ('issuer', 'authority', 'client', 'tenant', 'vessel', 'actor', 'duplicate', 'kind', 'wide', 'role', 'subject', 'permission'):
            config, metadata = self.inputs(); oidc = config['Authentication']['Oidc']; writer = oidc['Memberships'][0]
            if change in ('issuer', 'authority'): oidc[change.title()] = 'https://example.invalid'
            if change == 'client': oidc['ClientId'] = 'other-client'
            if change == 'tenant': metadata['tenantId'] = '10000000-0000-0000-0000-000000000002'
            if change == 'vessel': metadata['vesselId'] = '30000000-0000-0000-0000-000000000003'
            if change == 'actor': writer['ActorId'] = '80000000-0000-0000-0000-000000000009'
            if change == 'duplicate': oidc['Memberships'].append(deepcopy(writer))
            if change == 'kind': writer['ActorKind'] = 'Service'
            if change == 'wide': writer['TenantWideManage'] = False
            if change == 'role': writer['RoleReferences'] = []
            if change == 'subject': writer['Subject'] = 'not-provider-subject'
            if change == 'permission': writer['Permissions'].remove('maintenance.write')
            before = deepcopy(config)
            with self.assertRaises(RuntimeError): fixture.configure(config, metadata)
            self.assertEqual(before, config)

    def test_malformed_run_plan_or_noncanonical_identity_never_gets_a_generated_fallback(self):
        for key in ('runId', 'writerActorId', 'writerRoleId'):
            for value in ('', None, '00000000-0000-0000-0000-000000000000', 'not-a-uuid', 'abcdefab-0000-0000-0000-000000000001'.upper()):
                config, metadata = self.inputs(); metadata[key] = value; before = deepcopy(config)
                with self.assertRaises(RuntimeError): fixture.configure(config, metadata)
                self.assertEqual(before, config)
        config, metadata = self.inputs(); metadata['state'] = 'ACCEPTED'
        with self.assertRaisesRegex(RuntimeError, 'scope_invalid'): fixture.configure(config, metadata)


if __name__ == '__main__':
    unittest.main()
