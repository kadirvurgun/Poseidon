"""Source contract tests: synthetic isolated fixture configuration, not acceptance."""
from copy import deepcopy
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import tempfile
from unittest.mock import patch
import unittest
import uuid

import bundle_fixture_plan as plan
import oidc_lab as lab


class BundleFixturePlanTests(unittest.TestCase):
    def setup_fixture(self):
        realm, _, _, config = lab.make_configuration()
        realm['users'].append({'id': str(uuid.uuid4()), 'username': 'lab-foreign', 'enabled': True})
        config['Authentication']['Oidc']['Memberships'].append({'Issuer': lab.AUTHORITY,
            'Subject': realm['users'][-1]['id'], 'ActorId': str(uuid.uuid4()), 'TenantId': plan.FOREIGN_TENANT,
            'Permissions': ['directory.read', 'directory.write'], 'TenantWideManage': False, 'TenantWideRead': False,
            'VesselGrants': [{'VesselId': plan.FOREIGN_SHIP, 'CanEdit': True}]})
        return realm, config

    def configure(self, realm, config, run=None):
        return plan.configure(config, realm, lab.AUTHORITY, lab.PROJECT, run or str(uuid.uuid4()),
            datetime(2026, 11, 9, 0, 0, tzinfo=timezone.utc))

    def test_copy_preserves_credentials_provider_and_restricted_actor(self):
        realm, config = self.setup_fixture(); original = deepcopy(config)
        after, metadata = self.configure(realm, config)
        self.assertEqual(original, config)
        self.assertEqual(original['ConnectionStrings'], after['ConnectionStrings'])
        self.assertEqual(original['Authentication']['Oidc']['ClientSecret'], after['Authentication']['Oidc']['ClientSecret'])
        self.assertEqual(original['Authentication']['Oidc']['Memberships'][1], after['Authentication']['Oidc']['Memberships'][1])
        self.assertNotIn('credentials', metadata); self.assertNotIn('csrfToken', metadata)
        self.assertEqual('CONFIGURATION_ONLY_REQUIRES_REAL_LOGIN_AND_HTTP', metadata['state'])
        writer = after['Authentication']['Oidc']['Memberships'][0]
        self.assertTrue(set(plan.WRITER_PERMISSIONS).issubset(writer['Permissions']))
        self.assertFalse(any('verify' in permission or 'finalize' in permission for permission in writer['Permissions']))

    def test_exact_record_authorities_match_existing_browser_hash_scheme_both_lanes(self):
        realm, config = self.setup_fixture(); run = str(uuid.uuid4())
        after, _ = self.configure(realm, config, run)
        reviewer = after['Authentication']['Oidc']['Memberships'][2]
        appointments = reviewer['DailyReviewAuthorities']; self.assertEqual(6, len(appointments))
        for kind, (duty, _) in plan.FAMILIES.items():
            for lane in ('desktop', 'narrow'):
                h = hashlib.sha256(f'poseidon.daily.signed-fixture.v1:{run}:{lane}:{kind}:review'.encode()).hexdigest()
                identifier = str(uuid.UUID(h[:32]))
                matches = [a for a in appointments if a['RecordId'] == identifier]
                self.assertEqual(1, len(matches)); self.assertEqual(duty, matches[0]['Duty'])
                self.assertEqual(plan.DAILY_SOURCE, matches[0]['SourceVersion'])
                self.assertTrue(matches[0]['AppointmentSource'].startswith('FICTIONAL'))
        self.assertNotIn('DailyReviewAuthorities', after['Authentication']['Oidc']['Memberships'][0])

    def test_offline_policy_only_three_explicit_writer_fields_not_reviewer_or_foreign(self):
        realm, config = self.setup_fixture(); after, meta = self.configure(realm, config)
        policies = after['DailyOffline']['Policies']; self.assertEqual(3, len(policies))
        for policy in policies:
            self.assertEqual([plan.FAMILIES[policy['Kind']][1]], [field['Key'] for field in policy['Fields']])
            self.assertEqual([{'TenantId':plan.TENANT, 'ActorId':meta['writerActorId'], 'VesselId':plan.SHIP, 'Kind':policy['Kind']}], policy['Scopes'])
            self.assertEqual(3600, policy['LeaseSeconds']); self.assertEqual(28800, policy['RetentionSeconds'])
            self.assertEqual('SelectedExistingOrNewDraft', policy['ResourcePolicy'])
        self.assertNotIn('CertificateExtraction', after)
        self.assertEqual('UNCONFIGURED_REQUIRES_EXPLICIT_PROTOCOL_PEER', meta['extraction'])

    def test_missing_foreign_real_provider_user_fails_without_partial_permissions(self):
        realm, config = self.setup_fixture(); realm['users'].pop(); original = deepcopy(config)
        with self.assertRaisesRegex(RuntimeError, 'provider_identity_required'):
            self.configure(realm, config)
        self.assertEqual(original, config)

    def test_existing_operator_policy_and_appointments_are_preserved(self):
        for location in ('offline', 'review'):
            realm, config = self.setup_fixture()
            if location == 'offline': config['DailyOffline'] = {'Policies':[{'Version':'operator-owned'}]}
            else: config['Authentication']['Oidc']['Memberships'][2]['DailyReviewAuthorities'] = [{'Id':'operator-owned'}]
            original = deepcopy(config)
            with self.assertRaisesRegex(RuntimeError, 'existing_policy_preserved'):
                self.configure(realm, config)
            self.assertEqual(original, config)

    def test_wrong_provider_duplicate_actor_wide_restricted_scope_fail_closed(self):
        for change in ('provider', 'actor', 'restricted', 'foreign', 'role'):
            realm, config = self.setup_fixture(); members=config['Authentication']['Oidc']['Memberships']
            if change == 'provider': config['Authentication']['Oidc']['Authority'] = 'https://example.invalid'
            if change == 'actor': members[-1]['ActorId'] = members[0]['ActorId']
            if change == 'restricted': members[1]['TenantWideManage'] = True
            if change == 'foreign': members[-1]['TenantWideRead'] = True
            if change == 'role': members[0]['RoleReferences'] = ['not-an-identifier']
            original=deepcopy(config)
            with self.assertRaises(RuntimeError): self.configure(realm, config)
            self.assertEqual(original, config)

    def test_existing_ready_source_profile_is_not_rotated(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(lab, 'PRIVATE', Path(directory)):
            (Path(directory)/'ready').write_text('operator-owned')
            (Path(directory)/'appsettings.json').write_text('preserve')
            with self.assertRaisesRegex(RuntimeError, 'fresh_disposable_bundle_profile_required'):
                lab.init_source_bundle()
            self.assertEqual('preserve', (Path(directory)/'appsettings.json').read_text())

    def test_fresh_source_profile_plan_has_matching_four_provider_subjects_without_provider_calls(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(lab, 'PRIVATE', Path(directory)), patch.object(lab, 'private_directory'), patch.object(lab, 'run') as child:
            lab.init_source_bundle()
            root=Path(directory); metadata=json.loads((root/'source-bundle-plan.json').read_text())
            realm=json.loads((root/'realm/poseidon-lab-realm.json').read_text())
            config=json.loads((root/'appsettings.json').read_text())
            members=config['Authentication']['Oidc']['Memberships']
            self.assertEqual(4,len(realm['users'])); self.assertEqual({u['id'] for u in realm['users']},{m['Subject'] for m in members})
            self.assertEqual(metadata['foreignActorId'],members[-1]['ActorId'])
            self.assertEqual(plan.FOREIGN_TENANT,members[-1]['TenantId'])
            self.assertEqual(1,child.call_count)
            self.assertIn('oidc-lab-certificates.ps1',child.call_args.args[0][3])
            self.assertNotIn('DailyReviewAuthorities',members[1])
            mappings=config['WorkspaceNavigation']['Mappings']
            self.assertTrue(any('operational-downtime' in row['Keys'] and len(row['Keys']) >=12 for row in mappings))
            self.assertEqual(config,lab.navigation_membership(config,realm,source_bundle=True))

    def test_fixture_environment_requires_all_seven_private_bounded_files(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(lab, 'PRIVATE', Path(directory)):
            root=Path(directory)
            self.assertEqual({},lab.source_fixture_environment())
            values={}
            for name in ('DAILY','CERTIFICATE','CERTIFICATE_EXTRACTION','SHIP_OPERATIONS','DEFECT','WORK_ORDER','DOWNTIME'):
                file=root/(name.lower()+'.json');file.write_text('{}')
                values['POSEIDON_BROWSER_'+name+'_FIXTURE']=str(file.resolve())
            env=root/'source-bundle-environment.json';env.write_text(json.dumps(values))
            self.assertEqual(values,lab.source_fixture_environment())
            values.pop('POSEIDON_BROWSER_DOWNTIME_FIXTURE');env.write_text(json.dumps(values))
            with self.assertRaisesRegex(RuntimeError,'incomplete'):lab.source_fixture_environment()

    def test_fixture_environment_rejects_path_escape_extra_env_and_oversized_files(self):
        for change in ('escape','extra','oversize'):
            with tempfile.TemporaryDirectory() as directory, patch.object(lab,'PRIVATE',Path(directory)):
                root=Path(directory);file=root/'fixture.json';file.write_text('{}')
                values={'POSEIDON_BROWSER_'+name+'_FIXTURE':str(file.resolve()) for name in ('DAILY','CERTIFICATE','CERTIFICATE_EXTRACTION','SHIP_OPERATIONS','DEFECT','WORK_ORDER','DOWNTIME')}
                if change=='escape':values['POSEIDON_BROWSER_DAILY_FIXTURE']=str((root.parent/'untrusted.json').resolve())
                if change=='extra':values['NODE_TLS_REJECT_UNAUTHORIZED']='0'
                if change=='oversize':file.write_text('a'*8193)
                (root/'source-bundle-environment.json').write_text(json.dumps(values))
                with self.assertRaises(RuntimeError):lab.source_fixture_environment()

    def test_source_policy_ambient_overrides_are_removed_and_owned_peer_secret_is_explicit(self):
        import os
        with tempfile.TemporaryDirectory() as directory, patch.object(lab,'PRIVATE',Path(directory)), patch.object(lab,'root_tool',return_value=str(Path('test-dotnet.exe').resolve())):
            root=Path(directory)
            ambient={'CertificateExtraction__Endpoint':'https://external.invalid','DOTNET_DailyOffline__Policies__0__Kind':'Engine'}
            with patch.dict(os.environ,ambient):
                env=lab.environment()
            self.assertFalse(any(key in env for key in ambient))
            (root/'extraction-peer-secret').write_text('synthetic_fixture_secret')
            self.assertNotIn('POSEIDON_BUNDLE_EXTRACTION_SECRET',lab.environment())
            (root/'source-bundle-plan.json').write_text('{}')
            self.assertEqual('synthetic_fixture_secret',lab.environment()['POSEIDON_BUNDLE_EXTRACTION_SECRET'])
            (root/'extraction-peer-secret').write_text('bad\nsecret')
            with self.assertRaisesRegex(RuntimeError,'synthetic_peer_secret_invalid'):lab.environment()

    def test_noncanonical_run_and_naive_clock_fail_before_any_result(self):
        realm, config = self.setup_fixture()
        for run in ('not-a-uuid', '00000000-0000-0000-0000-000000000000', str(uuid.uuid4()).upper()):
            with self.assertRaises(RuntimeError): self.configure(realm, config, run)
        with self.assertRaisesRegex(RuntimeError, 'utc_required'):
            plan.configure(config, realm, lab.AUTHORITY, lab.PROJECT, str(uuid.uuid4()), datetime(2026,11,9))


if __name__ == '__main__': unittest.main()
