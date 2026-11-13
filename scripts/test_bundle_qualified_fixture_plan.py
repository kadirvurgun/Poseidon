"""Authored pure fixture-planning tests. No host/provider/seed/file-scanner calls."""
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import secrets
import unittest
import uuid
import json
import os
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

import bundle_qualified_fixture_plan as q
import bundle_oidc_lab as driver


def uid():
    return str(uuid.uuid4())


class QualifiedFixturePlanTests(unittest.TestCase):
    def setUp(self):
        self.now = datetime(2026, 11, 13, tzinfo=timezone.utc)
        self.metadata = {'schemaVersion': 'poseidon.bundle-fixture-plan.v1', 'runId': uid(), 'tenantId': q.base.TENANT,
            'vesselId': q.base.SHIP, 'writerActorId': uid(), 'reviewerActorId': uid(), 'writerRoleId': uid(), 'reviewerRoleId': uid()}
        company = uid()
        self.vessels = {'schemaVersion': 'poseidon.qualified-owning.observed-vessels.v1', **{key: self.metadata[key] for key in ('runId', 'tenantId', 'writerActorId')},
            'vessels': {kind: {'id': uid(), 'companyId': company, 'kind': kind, 'code': q.vessel_code(self.metadata['runId'], kind), 'version': 1,
                'profileRevision': 1, 'profileVersion': 1, 'profileContentSha256': 'A'*64} for kind in ('Ship', 'Tug')}}
        self.plan = q.create(self.metadata, self.vessels, self.now)
        self.config = {'Authentication': {'Oidc': {'ClientSecret': secrets.token_urlsafe(32), 'Memberships': [
            {'ActorId': self.metadata[name+'ActorId'], 'TenantId': q.base.TENANT, 'Subject': uid(), 'Issuer': 'https://localhost:8443/realms/poseidon-lab',
             'ActorKind': 'Human', 'TenantWideRead': True, 'RoleReferences': [self.metadata[name+'RoleId']], 'Permissions': ['directory.read'],
             'VesselGrants': [], 'RegulatoryAuthorities': [], 'HumanAuthorities': []} for name in ('writer', 'reviewer')] + [
            {'ActorId': uid(), 'TenantId': q.base.FOREIGN_TENANT, 'Permissions': ['directory.read'], 'Subject': uid()}]}},
            'UnrelatedOperatorValue': {'nonce': secrets.token_urlsafe(32)}}
        self.sources = {'schemaVersion': 'poseidon.qualified-owning.observed-sources.v1', 'runId': self.plan['runId'], 'rules': {
            mode: {'id': uid(), 'revision': 1, 'sourceId': uid(), 'sourceRevision': 1, 'requirementId': uid(), 'requirementRevision': 1} for mode in q.MODES}}
        controls = {mode: uid() for mode in q.MODES}
        evaluations = {mode: uid() for mode in q.MODES}
        operation = uid()
        self.witnesses = {'schemaVersion': 'poseidon.qualified-owning.observed-witnesses.v1', 'runId': self.plan['runId'], 'witnesses': {
            name: {'vesselId': self.plan['tugId'] if mode == 'Tug' else self.plan['shipId'], 'id': uid(), 'revision': 2, 'controlId': controls[mode],
                'controlRevision': 1, 'operationId': operation if name in ('tugService', 'tugOperation') else uid(), 'baselineEvaluationId': evaluations[mode]}
            for name, (mode, _) in q.WITNESSES.items()}}

    def configure(self, config=None, sources=None, witnesses=None, plan=None, metadata=None, now=None):
        return q.configure(config or self.config, metadata or self.metadata, plan or self.plan, sources, witnesses, now or self.now, vessels=self.vessels)

    def test_explicit_plan_is_run_bound_and_configuration_preserves_original_identities_and_unrelated_values(self):
        before = deepcopy(self.config)
        actual = self.configure()
        self.assertEqual(before, self.config)
        self.assertEqual(actual['UnrelatedOperatorValue'], before['UnrelatedOperatorValue'])
        self.assertEqual(actual['Authentication']['Oidc']['ClientSecret'], before['Authentication']['Oidc']['ClientSecret'])
        for old, new in zip(before['Authentication']['Oidc']['Memberships'], actual['Authentication']['Oidc']['Memberships']):
            for key in ('Subject', 'ActorId', 'TenantId'):
                self.assertEqual(old[key], new[key])
        self.assertEqual(actual['Authentication']['Oidc']['Memberships'][2], before['Authentication']['Oidc']['Memberships'][2])
        self.assertEqual(actual['TugPlanning']['Scopes'][0]['EligibleTugs'], [self.plan['tugId']])
        self.assertEqual(actual['CrewPositionPolicy']['Positions'][0]['PositionId'], self.plan['positionId'])
        self.assertEqual(self.configure(config=actual), actual)

    def test_observed_exact_source_and_evidence_appointments_are_additive_idempotent_and_distinct(self):
        actual = self.configure(sources=self.sources, witnesses=self.witnesses)
        members = actual['Authentication']['Oidc']['Memberships']
        self.assertEqual([g['Actions'] for g in members[0]['RegulatoryAuthorities']], [['qualify']] * 3)
        self.assertEqual([g['Actions'] for g in members[1]['RegulatoryAuthorities']], [['draft', 'review', 'own']] * 3)
        self.assertEqual({g['Capability'] for g in members[0]['HumanAuthorities']}, {'human.review.submit'})
        self.assertEqual({g['Capability'] for g in members[1]['HumanAuthorities']}, {'human.verify'})
        self.assertEqual(len(members[0]['HumanAuthorities']), 6)
        self.assertEqual(len({g['Id'] for m in members[:2] for field in ('HumanAuthorities', 'RegulatoryAuthorities') for g in m[field]}), 18)
        self.assertNotIn('human.decide', members[1]['Permissions'])
        self.assertEqual(self.configure(config=actual, sources=self.sources, witnesses=self.witnesses), actual)

    def test_foreign_run_tenant_actor_scope_or_expired_plan_never_authorizes(self):
        for key in ('runId', 'tenantId', 'writerActorId', 'reviewerActorId', 'writerRoleId'):
            changed = deepcopy(self.metadata); changed[key] = uid()
            with self.subTest(key=key), self.assertRaises(RuntimeError):
                self.configure(metadata=changed)
        with self.assertRaises(RuntimeError):
            self.configure(now=self.now + timedelta(hours=9))
        for key, value in (('shipId', q.TUG), ('writerActorId', self.plan['reviewerActorId']), ('positionId', uid()),
                           ('writerRoleId', self.plan['reviewerRoleId']), ('expiresAt', self.plan['notBefore']), ('extra', True)):
            changed = deepcopy(self.plan); changed[key] = value
            with self.subTest(key=key), self.assertRaises(RuntimeError):
                self.configure(plan=changed)
        with self.assertRaises(RuntimeError):
            q.create({**self.metadata, 'runId': self.metadata['runId']+'\n'}, self.vessels, self.now)

    def test_malformed_observed_source_pins_and_collisions_fail_without_replacing_any_grant(self):
        for key, value in (('revision', True), ('sourceRevision', 1.5), ('requirementRevision', 0), ('id', uid()+'\n'), ('extra', uid())):
            changed = deepcopy(self.sources); changed['rules']['Ship'][key] = value
            with self.subTest(key=key), self.assertRaises(RuntimeError):
                self.configure(sources=changed)
        changed = deepcopy(self.sources); changed['rules']['Tug']['sourceId'] = changed['rules']['Ship']['sourceId']
        with self.assertRaises(RuntimeError):
            self.configure(sources=changed)
        actual = self.configure(sources=self.sources)
        changed = deepcopy(actual); changed['Authentication']['Oidc']['Memberships'][0]['RegulatoryAuthorities'][0]['RoleId'] = uid()
        with self.assertRaises(RuntimeError):
            self.configure(config=changed, sources=self.sources)

    def test_operator_policy_and_conflicting_appointments_are_preserved_by_rejection(self):
        for key in ('TugPlanning', 'CrewPositionPolicy'):
            changed = deepcopy(self.config); changed[key] = {'Version': 'OPERATOR-CONTROLLED'}
            with self.subTest(key=key), self.assertRaises(RuntimeError):
                self.configure(config=changed)
            self.assertEqual(changed[key], {'Version': 'OPERATOR-CONTROLLED'})
        actual = self.configure(sources=self.sources, witnesses=self.witnesses)
        actual['Authentication']['Oidc']['Memberships'][1]['HumanAuthorities'][0]['OperationId'] = uid()
        with self.assertRaises(RuntimeError):
            self.configure(config=actual, sources=self.sources, witnesses=self.witnesses)
        restricted = deepcopy(self.config)
        restricted['Authentication']['Oidc']['Memberships'][1]['TenantWideRead'] = False
        with self.assertRaises(RuntimeError):
            self.configure(config=restricted)
        self.assertFalse(restricted['Authentication']['Oidc']['Memberships'][1]['TenantWideRead'])

    def test_existing_policy_and_appointment_numeric_types_or_nested_shape_cannot_be_adopted_or_repaired(self):
        actual = self.configure(sources=self.sources, witnesses=self.witnesses)
        variants = []
        for value in (1.0, True):
            policy = deepcopy(actual); policy['TugPlanning']['Contexts'][0]['SourceRevision'] = value
            variants.append(policy)
            appointment = deepcopy(actual)
            appointment['Authentication']['Oidc']['Memberships'][0]['RegulatoryAuthorities'][0]['SourceRevision'] = value
            variants.append(appointment)
        extra = deepcopy(actual); extra['TugPlanning']['Contexts'][0]['extra'] = None; variants.append(extra)
        extra = deepcopy(actual); extra['CrewPositionPolicy']['Positions'][0]['extra'] = None; variants.append(extra)
        extra = deepcopy(actual); extra['Authentication']['Oidc']['Memberships'][0]['RegulatoryAuthorities'][0]['Contract']['extra'] = None
        variants.append(extra)
        reordered_type = deepcopy(actual)
        reordered_type['Authentication']['Oidc']['Memberships'][0]['RegulatoryAuthorities'][0]['Actions'] = ('qualify',)
        variants.append(reordered_type)
        for index, changed in enumerate(variants):
            # JSON retains bool-vs-int/float spellings where Python == does not.
            before = json.dumps(changed, sort_keys=True)
            with self.subTest(variant=index), self.assertRaises(RuntimeError):
                self.configure(config=changed, sources=self.sources, witnesses=self.witnesses)
            self.assertEqual(before, json.dumps(changed, sort_keys=True))
        self.assertIsInstance(reordered_type['Authentication']['Oidc']['Memberships'][0]['RegulatoryAuthorities'][0]['Actions'], tuple)

    def test_stale_wrong_vessel_or_reused_witness_tuple_never_gets_an_appointment(self):
        for key, value in (('vesselId', q.base.FOREIGN_SHIP), ('revision', 0), ('controlRevision', True), ('baselineEvaluationId', uid())):
            changed = deepcopy(self.witnesses); changed['witnesses']['tugOperation'][key] = value
            with self.subTest(key=key), self.assertRaises(RuntimeError):
                self.configure(sources=self.sources, witnesses=changed)
        changed = deepcopy(self.witnesses); changed['witnesses']['shipMsmd']['id'] = changed['witnesses']['shipClass']['id']
        with self.assertRaises(RuntimeError):
            self.configure(sources=self.sources, witnesses=changed)
        with self.assertRaises(RuntimeError):
            self.configure(witnesses=self.witnesses)

    def test_additive_descriptor_rejects_wrong_run_stale_source_reused_tuples_unknown_keys_or_unknown_outcome_shape(self):
        evaluations = {mode: next(row['baselineEvaluationId'] for name, row in self.witnesses['witnesses'].items() if q.WITNESSES[name][0] == mode) for mode in q.MODES}
        value = {'schemaVersion': 'poseidon.qualified-owning.signed-fixture.v1',
            **{key: self.plan[key] for key in ('runId', 'tenantId', 'writerActorId', 'reviewerActorId', 'shipId', 'tugId', 'expiresAt')},
            'vessels': self.vessels['vessels'],
            'rules': {mode: {**row, 'qualificationRevision': 3, 'qualificationHash': 'A'*64, 'baselineEvaluationId': evaluations[mode]} for mode, row in self.sources['rules'].items()},
            'witnesses': {name: {**row, 'hash': 'B'*64, 'fileId': uid(), 'fileRevision': 1, 'fileChecksum': 'C'*64,
                'verificationId': uid(), 'verificationRevision': 2} for name, row in self.witnesses['witnesses'].items()}}
        self.assertEqual(q.validate_descriptor(self.plan, value, self.now), value)
        for change in ('run', 'extra', 'missing', 'unknown', 'revision', 'baseline', 'duplicate'):
            altered = deepcopy(value)
            if change == 'run': altered['runId'] = uid()
            elif change == 'extra': altered['ready'] = True
            elif change == 'missing': del altered['witnesses']['shipMsmd']
            elif change == 'unknown': altered['witnesses']['shipCargo']['verificationRevision'] = None
            elif change == 'revision': altered['rules']['Ship']['sourceRevision'] = 1.0
            elif change == 'baseline': altered['witnesses']['shipCargo']['baselineEvaluationId'] = uid()
            else: altered['witnesses']['shipMsmd']['fileId'] = altered['witnesses']['shipCargo']['fileId']
            with self.subTest(change=change), self.assertRaises(RuntimeError):
                q.validate_descriptor(self.plan, altered, self.now)
        with self.assertRaises(RuntimeError):
            q.validate_descriptor(self.plan, value, self.now+timedelta(hours=9))

    def test_driver_requires_the_existing_exact_eight_and_exact_owned_host_before_any_publication_or_command(self):
        lanes = {'schemaVersion': 'poseidon.bundle.signed-lane-fixtures.v1', 'runId': self.plan['runId'], 'lanes': {'desktop': {}, 'narrow': {}}}
        for count, owned in ((7, True), (8, False)):
            with self.subTest(count=count, owned=owned), patch.object(driver.private_io, 'selected', return_value=(self.config, Path('unused.json'))), \
                 patch.object(driver.private_io, 'read_json', side_effect=[self.metadata, lanes]), \
                 patch.object(driver, 'source_fixture_environment', return_value={str(i): 'synthetic' for i in range(count)}), \
                 patch.object(driver.lab, 'owned_process', return_value=owned), \
                 patch.object(driver.private_io, 'publish_json') as publish, patch.object(driver.private_io, 'publish_config') as config, \
                 patch.object(driver.lab, 'run') as run, patch.object(driver, 'start') as start:
                with self.assertRaises(RuntimeError):
                    driver.prepare_qualified_fixtures()
                publish.assert_not_called(); config.assert_not_called(); run.assert_not_called(); start.assert_not_called()

    def test_only_actual_observed_dedicated_vessels_can_bind_the_plan_and_repeated_input_preserves_ids(self):
        self.assertEqual(q.create(self.metadata, self.vessels, self.now), self.plan)
        self.assertNotIn(self.plan['shipId'], (q.base.SHIP, q.TUG, q.base.FOREIGN_SHIP))
        self.assertTrue(q.vessel_code(self.plan['runId'], 'Ship') > 'DEMO-SHIP')
        self.assertTrue(q.vessel_code(self.plan['runId'], 'Tug') > 'DEMO-TUG')
        for change in ('tenant', 'actor', 'seed', 'type', 'code', 'version', 'profile', 'company', 'reused', 'extra'):
            altered = deepcopy(self.vessels)
            if change == 'tenant': altered['tenantId'] = q.base.FOREIGN_TENANT
            elif change == 'actor': altered['writerActorId'] = uid()
            elif change == 'seed': altered['vessels']['Ship']['id'] = q.base.SHIP
            elif change == 'type': altered['vessels']['Ship']['kind'] = 'Tug'
            elif change == 'code': altered['vessels']['Ship']['code'] = 'OPERATOR'
            elif change == 'version': altered['vessels']['Ship']['version'] = 1.0
            elif change == 'profile': altered['vessels']['Ship']['profileRevision'] = 0
            elif change == 'company': altered['vessels']['Tug']['companyId'] = uid()
            elif change == 'reused': altered['vessels']['Tug']['id'] = altered['vessels']['Ship']['id']
            else: altered['ready'] = True
            with self.subTest(change=change), self.assertRaises(RuntimeError):
                q.create(self.metadata, altered, self.now)

    def test_additive_environment_cannot_be_smuggled_into_the_old_exact_eight_decoder_or_adopted_from_ambient_path(self):
        with TemporaryDirectory(prefix='poseidon-qualified-driver-') as directory:
            root = Path(directory)
            values = {'POSEIDON_BROWSER_'+name+'_FIXTURE': str(root/'synthetic.json') for name in driver.FIXTURE_NAMES}
            values['POSEIDON_BROWSER_QUALIFIED_OWNING_FIXTURE'] = str(root/'outside.json')
            (root/'source-bundle-environment.json').write_text(json.dumps(values), encoding='utf-8')
            with patch.object(driver.lab, 'PRIVATE', root):
                with self.assertRaisesRegex(RuntimeError, 'source_fixture_environment_incomplete'):
                    driver.source_fixture_environment()
                with patch.dict(os.environ, {'POSEIDON_BROWSER_QUALIFIED_OWNING_FIXTURE': str(root/'outside.json')}):
                    with self.assertRaisesRegex(RuntimeError, 'qualified_fixture_preparation_required'):
                        driver.qualified_fixture_environment()


if __name__ == '__main__':
    unittest.main()
