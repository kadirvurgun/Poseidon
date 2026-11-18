"""Authored pure/driver protocol regressions; no provider/HTTP/browser execution."""
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import uuid

import bundle_foundation_policy_fixture as fixture
import bundle_fixture_plan as base
import bundle_crew_fixture as crew
import bundle_oidc_lab as driver
import oidc_lab as lab


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode('utf-8')).hexdigest()


class FoundationPolicyFixtureTests(unittest.TestCase):
    def setUp(self):
        self.now = datetime(2026, 11, 13, tzinfo=timezone.utc)
        realm, _, _, config = lab.make_configuration()  # Ephemeral test-only values; never printed.
        subject = str(uuid.uuid4())
        realm['users'].append({'id': subject, 'username': 'lab-foreign', 'enabled': True})
        config['Authentication']['Oidc']['Memberships'].append({'Issuer': lab.AUTHORITY, 'Subject': subject,
            'ActorId': str(uuid.uuid4()), 'TenantId': base.FOREIGN_TENANT, 'Permissions': ['directory.read'],
            'TenantWideRead': False, 'TenantWideManage': False, 'VesselGrants': [{'VesselId': base.FOREIGN_SHIP, 'CanEdit': False}]})
        config = lab.navigation_membership(config, realm)
        config = lab.catalog_membership(config, realm, lab.AUTHORITY, lab.PROJECT, self.now)
        config, self.metadata = base.configure(config, realm, lab.AUTHORITY, lab.PROJECT, str(uuid.uuid4()), self.now)
        config = crew.configure(config, self.metadata)
        writer = next(m for m in config['Authentication']['Oidc']['Memberships'] if m['ActorId'] == self.metadata['writerActorId'])
        for permission in ('file.read', 'file.write'):
            if permission not in writer['Permissions']: writer['Permissions'].append(permission)
        config['WorkspaceNavigation']['Mappings'][0]['Keys'] = list(dict.fromkeys([*config['WorkspaceNavigation']['Mappings'][0]['Keys'], *driver.NAVIGATION_KEYS]))
        config['TugPlanning'] = {'operatorOwned': 'untouched'}
        config['CrewPositionPolicy'] = {'operatorOwned': 'untouched'}
        self.config = config
        self.plan = fixture.create_plan(self.metadata, lab.PROJECT, self.now)

    def configure(self, config=None, plan=None, metadata=None, now=None):
        return fixture.configure(self.config if config is None else config, self.metadata if metadata is None else metadata,
            self.plan if plan is None else plan, lab.PROJECT, self.now if now is None else now)

    def rejected_unchanged(self, config=None, plan=None, metadata=None):
        config = self.config if config is None else config
        before = digest(config)
        with self.assertRaises(RuntimeError): self.configure(config=config, plan=plan, metadata=metadata)
        self.assertEqual(before, digest(config))

    def test_exact_known_navigation_repartition_preserves_roles_anchors_modes_union_order_and_other_sections(self):
        before = digest(self.config)
        actual = self.configure()
        self.assertEqual(before, digest(self.config))
        rows = actual['WorkspaceNavigation']['Mappings']; prior = self.config['WorkspaceNavigation']['Mappings']
        self.assertEqual([15, 7], [len(row['Keys']) for row in rows])
        self.assertEqual(set().union(*(set(row['Keys']) for row in prior)), set().union(*(set(row['Keys']) for row in rows)))
        self.assertEqual(21, len(set().union(*(set(row['Keys']) for row in rows))))
        for index, anchors in enumerate((fixture.DAILY_ANCHORS, fixture.REVIEW_ANCHORS)):
            self.assertTrue(set(anchors).issubset(rows[index]['Keys']))
            self.assertEqual(prior[index]['Reference'], rows[index]['Reference'])
            self.assertEqual(prior[index]['Modes'], rows[index]['Modes'])
        self.assertTrue(fixture.exact(actual['WorkspaceContext'], self.config['WorkspaceContext']))
        self.assertTrue(fixture.exact(actual['TugPlanning'], self.config['TugPlanning']))
        self.assertTrue(fixture.exact(actual['CrewPositionPolicy'], self.config['CrewPositionPolicy']))

    def test_only_two_writer_document_permissions_and_exact_minimal_offline_and_file_policies(self):
        actual = self.configure()
        for old, new in zip(self.config['Authentication']['Oidc']['Memberships'], actual['Authentication']['Oidc']['Memberships']):
            if old['ActorId'] == self.plan['writerActorId']:
                self.assertEqual(list(fixture.DOCUMENT_PERMISSIONS), [p for p in new['Permissions'] if p not in old['Permissions']])
                retained = deepcopy(new); retained['Permissions'] = old['Permissions']
                self.assertTrue(fixture.exact(retained, old))
            else: self.assertTrue(fixture.exact(old, new))
        offline = actual['OfflineApplication']
        self.assertEqual(['watch_group'], offline['Fields']); self.assertEqual(['SYNTHETIC-A', 'SYNTHETIC-B'], offline['WatchGroups'])
        self.assertEqual([{'TenantId': base.TENANT, 'ActorId': self.plan['writerActorId'], 'VesselId': base.SHIP}], offline['Scopes'])
        self.assertEqual('crew.draft.watch-group.v1', offline['Operation']); self.assertEqual(fixture.OFFLINE_SOURCE, offline['SourceVersion'])
        self.assertEqual(['Photo', 'Passport'], actual['CrewFilePolicy']['Kinds'])
        self.assertTrue(actual['CrewFilePolicy']['Purpose'].startswith('SYNTHETIC-UNAPPROVED-'))
        self.assertLessEqual(len(actual['CrewFilePolicy']['Purpose']), 80)
        for field in ('ClientSecret', 'Authority', 'Issuer', 'ClientId'):
            self.assertTrue(actual['Authentication']['Oidc'][field] == self.config['Authentication']['Oidc'][field])
        self.assertTrue(actual['ConnectionStrings'] == self.config['ConnectionStrings'])

    def test_same_run_repeat_keeps_window_and_shape_exact_without_restoring_revoked_permissions(self):
        actual = self.configure()
        again = self.configure(config=actual, now=self.now+timedelta(hours=1))
        self.assertTrue(fixture.exact(actual, again))
        fixture.require_applied(actual, self.metadata, self.plan, lab.PROJECT, self.now)
        with self.assertRaises(RuntimeError): fixture.require_applied(self.config, self.metadata, self.plan, lab.PROJECT, self.now)
        old = deepcopy(actual); next(m for m in old['Authentication']['Oidc']['Memberships'] if m['ActorId'] == self.plan['writerActorId'])['Permissions'].remove('crew.documents.write')
        self.rejected_unchanged(config=old)
        with self.assertRaises(RuntimeError): self.configure(config=actual, now=self.now+timedelta(hours=9))
        changed = {**self.plan, 'expiresAt': (self.now+timedelta(hours=7)).isoformat()}
        self.rejected_unchanged(config=actual, plan=changed)

    def test_operator_navigation_unknown_refs_duplicates_partial_states_and_label_drift_are_not_repaired(self):
        for change in ('unknown-key', 'duplicate-ref', 'extra-role', 'label', 'mode', 'partial', 'version'):
            altered = deepcopy(self.config)
            rows = altered['WorkspaceNavigation']['Mappings']
            if change == 'unknown-key': rows[0]['Keys'][0] = 'operator-unknown'
            elif change == 'duplicate-ref': rows.append(deepcopy(rows[0]))
            elif change == 'extra-role': altered['Authentication']['Oidc']['Memberships'][0]['RoleReferences'].append(str(uuid.uuid4()))
            elif change == 'label': altered['WorkspaceContext']['RoleCatalog']['Roles'][0]['En'] = 'OPERATOR'
            elif change == 'mode': rows[0]['Modes'] = ['Fleet']
            elif change == 'partial': rows[0]['Keys'] = fixture.DAILY
            else: altered['WorkspaceNavigation']['Version'] = 'OPERATOR'
            with self.subTest(change=change): self.rejected_unchanged(config=altered)

    def test_other_run_foreign_owner_null_float_bool_extra_fields_and_missing_prerequisites_fail_without_partial_output(self):
        for key, value in (('runId', str(uuid.uuid4())), ('writerActorId', str(uuid.uuid4())), ('vesselId', base.FOREIGN_SHIP), ('sourceVersion', None)):
            altered = {**self.metadata, key: value}
            with self.subTest(key=key): self.rejected_unchanged(metadata=altered)
        for key, value in (('writerRoleId', self.plan['reviewerRoleId']), ('runId', self.plan['runId']+'\n'), ('extra', True), ('notBefore', None)):
            with self.subTest(key=key): self.rejected_unchanged(plan={**self.plan, key: value})
        for permission in fixture.PREREQUISITES:
            altered = deepcopy(self.config); next(m for m in altered['Authentication']['Oidc']['Memberships'] if m['ActorId'] == self.plan['writerActorId'])['Permissions'].remove(permission)
            with self.subTest(permission=permission): self.rejected_unchanged(config=altered)
        for key in ('OfflineApplication', 'CrewFilePolicy'):
            with self.subTest(key=key): self.rejected_unchanged(config={**self.config, key: None})
        for value in (False, True, 1.0, datetime(2026, 11, 13)):
            with self.assertRaises(RuntimeError): fixture.create_plan(self.metadata, lab.PROJECT, value)

    def test_edited_existing_policy_fields_and_numeric_coercion_are_rejected(self):
        actual = self.configure()
        changes = [('OfflineApplication', 'MaxRecords', 8.0), ('OfflineApplication', 'LeaseSeconds', True),
            ('OfflineApplication', 'Fields', ['watch_group', 'medicalExpiry']), ('OfflineApplication', 'SourceVersion', 'other'),
            ('OfflineApplication', 'Scopes', [{'TenantId': base.TENANT, 'ActorId': self.plan['reviewerActorId'], 'VesselId': base.SHIP}]),
            ('CrewFilePolicy', 'Kinds', ['Photo', 'Passport', 'CertificateDocument']), ('CrewFilePolicy', 'Purpose', 'operator'),
            ('CrewFilePolicy', 'extra', None)]
        for section, key, value in changes:
            altered = deepcopy(actual); altered[section][key] = value
            with self.subTest(section=section, key=key): self.rejected_unchanged(config=altered)

    def test_unrelated_fifth_membership_is_preserved_but_ambiguous_actors_or_nonwriter_document_access_is_rejected(self):
        altered = deepcopy(self.config)
        extra = {'ActorId': str(uuid.uuid4()), 'TenantId': base.TENANT, 'Subject': str(uuid.uuid4()), 'Issuer': lab.AUTHORITY,
            'ActorKind': 'Human', 'Permissions': ['directory.read'], 'RoleReferences': [], 'VesselGrants': [{'VesselId': base.SHIP, 'CanEdit': False}]}
        altered['Authentication']['Oidc']['Memberships'].append(extra)
        actual = self.configure(config=altered)
        self.assertTrue(fixture.exact(extra, actual['Authentication']['Oidc']['Memberships'][-1]))
        extra['Permissions'].append('crew.documents.read'); self.rejected_unchanged(config=altered)
        altered = deepcopy(self.config); altered['Authentication']['Oidc']['Memberships'].append(deepcopy(altered['Authentication']['Oidc']['Memberships'][0]))
        self.rejected_unchanged(config=altered)

    def test_driver_first_apply_is_prestart_publish_once_and_exact_repeat_never_invokes_runtime_or_churns_generation(self):
        configure = fixture.configure
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); (root/'ready').write_bytes(b'DEV-OIDC-LAB-01\n')
            (root/'source-bundle-plan.json').write_text(json.dumps(self.metadata), encoding='utf-8')
            (root/'source-bundle-plan.json').chmod(0o600)
            selected = [deepcopy(self.config)]; publications = []
            def publish(value): publications.append(value); selected[0] = deepcopy(value)
            def publish_plan(path, value):
                path.write_text(json.dumps(value), encoding='utf-8'); path.chmod(0o600)
            with patch.object(lab, 'PRIVATE', root), patch.object(lab, 'stage'), \
                 patch.object(driver.private_io, 'selected', side_effect=lambda: (selected[0], root/'selected.json')), \
                 patch.object(driver.private_io, 'publish_json', side_effect=publish_plan) as plans, \
                 patch.object(driver.private_io, 'publish_config', side_effect=publish), \
                 patch.object(driver.foundation_policy, 'create_plan', return_value=self.plan), \
                 patch.object(driver.foundation_policy, 'configure', side_effect=lambda c,m,p,project: configure(c,m,p,project,self.now)), \
                 patch.object(lab, 'run') as run, patch.object(lab, 'owned_process') as process:
                driver.prepare_foundation_policy_fixtures()
                original = (root/'foundation-policy-fixture-plan.json').read_bytes()
                driver.prepare_foundation_policy_fixtures()
                self.assertEqual(1, len(publications)); plans.assert_called_once()
                self.assertTrue(original == (root/'foundation-policy-fixture-plan.json').read_bytes())
                run.assert_not_called(); process.assert_not_called()

    def test_driver_missing_explicit_preparation_or_prior_business_attempt_cannot_start_writes(self):
        configure = fixture.configure
        with tempfile.TemporaryDirectory() as directory, patch.object(lab, 'PRIVATE', Path(directory)), patch.object(lab, 'run') as run:
            for prepare in (driver.prepare_source_fixtures, driver.prepare_qualified_fixtures):
                with self.subTest(prepare=prepare.__name__), self.assertRaises(RuntimeError): prepare()
            run.assert_not_called()
        for name in ('app.pid', 'source-bundle-provision-attempt.json', 'qualified-owning-plan.json', 'register-counts.json'):
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory); (root/'ready').write_bytes(b'DEV-OIDC-LAB-01\n')
                (root/'source-bundle-plan.json').write_text(json.dumps(self.metadata), encoding='utf-8'); (root/name).write_text('{}', encoding='utf-8')
                (root/'source-bundle-plan.json').chmod(0o600)
                with patch.object(lab, 'PRIVATE', root), patch.object(lab, 'stage'), patch.object(driver.private_io, 'selected', return_value=(self.config, root/'selected.json')), \
                     patch.object(driver.foundation_policy, 'create_plan', return_value=self.plan), \
                     patch.object(driver.foundation_policy, 'configure', side_effect=lambda c,m,p,project: configure(c,m,p,project,self.now)), \
                     patch.object(driver.private_io, 'publish_json') as plan, patch.object(driver.private_io, 'publish_config') as config:
                    with self.assertRaises(RuntimeError): driver.prepare_foundation_policy_fixtures()
                    plan.assert_not_called(); config.assert_not_called()

    def test_ambient_policy_overrides_cannot_replace_selected_fixture_policies(self):
        overrides = {'OfflineApplication__Fields__0': 'medicalExpiry', 'crewfilepolicy:Kinds:0': 'CertificateDocument',
            'DOTNET_OfflineApplication__LeaseSeconds': '86400', 'ASPNETCORE_CrewFilePolicy__Purpose': 'operator',
            'POSEIDON_UNRELATED_SYNTHETIC': 'preserved'}
        with tempfile.TemporaryDirectory() as directory, patch.object(lab, 'PRIVATE', Path(directory)), \
             patch.object(driver, 'BASE_ENVIRONMENT', return_value=dict(overrides)):
            actual = driver.environment()
        self.assertTrue(all(key not in actual for key in overrides if key != 'POSEIDON_UNRELATED_SYNTHETIC'))
        self.assertEqual('preserved', actual['POSEIDON_UNRELATED_SYNTHETIC'])


if __name__ == '__main__': unittest.main()
