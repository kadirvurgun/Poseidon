"""No runtime, private input, provider or generated credential is read here."""
from copy import deepcopy
import unittest
import bundle_crew_fixture as fixture


class BundleCrewFixtureTests(unittest.TestCase):
    def inputs(self):
        metadata = {'schemaVersion': 'poseidon.bundle-fixture-plan.v1',
            'runId': '70000000-0000-0000-0000-000000000001',
            'tenantId': '10000000-0000-0000-0000-000000000001', 'writerActorId': '80000000-0000-0000-0000-000000000001'}
        members = [{'ActorId': metadata['writerActorId'], 'TenantId': metadata['tenantId'], 'ActorKind': 'Human', 'Permissions': ['directory.read']},
            {'ActorId': 'restricted', 'Permissions': ['directory.read'], 'TenantId': metadata['tenantId']}]
        return {'Authentication': {'Oidc': {'Issuer': 'https://localhost:8443/realms/poseidon-lab', 'Memberships': members}}}, metadata

    def test_all_private_fields_have_explicit_capabilities_and_only_the_fictional_writer_is_enrolled(self):
        config, metadata = self.inputs(); before = deepcopy(config)
        result = fixture.configure(config, metadata)
        self.assertEqual(before, config)
        self.assertEqual(before['Authentication']['Oidc']['Memberships'][1], result['Authentication']['Oidc']['Memberships'][1])
        fields = result['CrewProfilePolicy']['Fields']
        self.assertEqual(set(fixture.FIELDS), {r['Field'] for r in fields})
        self.assertEqual(16, len(fields))
        medical = next(r for r in fields if r['Field'] == 'MedicalExpiry')
        self.assertEqual('crew.profile.medical.write', medical['WriteCapability'])
        self.assertEqual({'synthetic-bundle-v1': fixture.REFERENCE}, result['CrewProfilePolicy']['KeyReferences'])
        self.assertNotIn('HumanAuthorities', result['Authentication']['Oidc']['Memberships'][0])
        self.assertEqual(result, fixture.configure(result, metadata))

    def test_operator_policy_conflict_fails_without_partially_enrolling_permissions(self):
        for key in ('CrewIdentityPolicy', 'CrewProfilePolicy'):
            config, metadata = self.inputs();config[key] = {'Version': 'operator-owned'};before = deepcopy(config)
            with self.assertRaisesRegex(RuntimeError, 'existing_policy_preserved'):
                fixture.configure(config, metadata)
            self.assertEqual(before, config)

    def test_foreign_or_unrecognized_run_cannot_install_any_policy(self):
        for change in ('tenant', 'issuer', 'run', 'actor'):
            config, metadata = self.inputs()
            if change == 'tenant': metadata['tenantId'] = 'foreign'
            if change == 'issuer': config['Authentication']['Oidc']['Issuer'] = 'https://example.invalid'
            if change == 'run': metadata['runId'] = 'not-a-uuid'
            if change == 'actor': config['Authentication']['Oidc']['Memberships'][0]['ActorKind'] = 'Service'
            before = deepcopy(config)
            with self.assertRaises(RuntimeError): fixture.configure(config, metadata)
            self.assertEqual(before, config)

    def test_missing_or_noncanonical_key_is_never_replaced_with_an_ephemeral_fallback(self):
        for raw in (None, '', 'A' * 42 + '==', 'A' * 43 + 'A', 'not-base64', 'A' * 43 + '=\n'):
            with self.assertRaisesRegex(RuntimeError, 'key_invalid'): fixture.validate_key(raw)


if __name__ == '__main__': unittest.main()
