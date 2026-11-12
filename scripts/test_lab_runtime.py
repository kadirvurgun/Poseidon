"""BUNDLE-PORT-ISOLATION-01 protocol regressions; no live provider or runtime."""
from copy import deepcopy
import argparse
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import uuid

import bundle_oidc_lab as bundle
import bundle_private as store
import lab_runtime as runtime
import oidc_lab as lab


class LabRuntimeTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name).resolve()
        self.scope = patch.object(lab, 'PRIVATE', self.root)
        self.scope.start()
        self.ipv6_scope = patch.object(runtime, 'ipv6_free', return_value=True)
        self.ipv6_scope.start()
        self.original = lab.CURRENT_RUNTIME
        self.runtime = runtime.create(str(uuid.uuid4()), 15443, 18443, 25432)
        lab.bind_runtime(self.runtime)
        self.realm, _, _, self.config = lab.make_configuration()

    def tearDown(self):
        lab.bind_runtime(self.original)
        self.scope.stop()
        self.ipv6_scope.stop()
        self.directory.cleanup()

    def publish_metadata(self):
        (self.root / runtime.FILE).write_text(json.dumps(self.runtime), encoding='utf-8')
        (self.root / 'source-bundle-plan.json').write_text(json.dumps({
            'schemaVersion': 'poseidon.bundle-fixture-plan.v1',
            'runId': self.runtime['runId'], 'ownedRuntime': self.runtime}), encoding='utf-8')

    def test_legacy_defaults_omit_selected_config_and_keep_endpoint_identity(self):
        lab.bind_runtime()
        realm, _, _, config = lab.make_configuration()
        self.assertEqual('https://localhost:5443', lab.APP)
        self.assertEqual('https://localhost:8443/realms/poseidon-lab', lab.AUTHORITY)
        self.assertIsNone(runtime.configured(config))
        self.assertNotIn('OidcPort', config['DevelopmentLab'])
        self.assertNotIn('OwnedRuntime', config['DevelopmentLab'])
        self.assertIn('Port=55432;', config['ConnectionStrings']['Poseidon'])
        self.assertEqual(['https://localhost:5443/signin-oidc'], realm['clients'][0]['redirectUris'])

    def test_selected_ports_bind_config_redirect_logout_database_and_compose_environment(self):
        selected = runtime.endpoints(self.runtime)
        self.assertEqual(self.runtime, runtime.configured(self.config))
        self.assertEqual([selected['app'] + '/signin-oidc'], self.realm['clients'][0]['redirectUris'])
        self.assertEqual([selected['app']], self.realm['clients'][0]['webOrigins'])
        self.assertEqual(selected['app'] + '/signout-callback-oidc', self.realm['clients'][0]['attributes']['post.logout.redirect.uris'])
        self.assertTrue(all(row['Issuer'] == selected['authority'] for row in self.config['Authentication']['Oidc']['Memberships']))
        env = runtime.child_environment({'POSEIDON_LAB_PROVIDER_PORT': '9999', 'POSEIDON_LAB_RUN_ID': 'other'}, self.runtime)
        self.assertEqual('18443', env['POSEIDON_LAB_PROVIDER_PORT'])
        self.assertEqual('25432', env['POSEIDON_LAB_DATABASE_PORT'])
        self.assertEqual(self.runtime['runId'], env['POSEIDON_LAB_RUN_ID'])
        self.assertEqual(runtime.runtime_identity(self.runtime), env['POSEIDON_LAB_RUNTIME_IDENTITY'])

    def test_closed_schema_rejects_uuid_suffix_ambiguous_ports_and_unknown_keys(self):
        for field in ('appPort', 'providerPort', 'databasePort'):
            for value in (True, '15443', 15443.0, 0, 1023, 65536):
                candidate = {**self.runtime, field: value}
                with self.subTest(field=field, value=value), self.assertRaisesRegex(RuntimeError, '^owned_lab_runtime_invalid$'):
                    runtime.decode(candidate)
        for candidate in ({**self.runtime, 'runId': self.runtime['runId'] + '\n'},
                          {**self.runtime, 'runId': str(uuid.UUID(int=0))},
                          {**self.runtime, 'providerPort': self.runtime['appPort']},
                          {**self.runtime, 'extra': True}):
            with self.assertRaises(RuntimeError):
                runtime.decode(candidate)
        for value in ('+15443', '015443', ' 15443', '15443\n', '1023', '65536'):
            with self.assertRaises(argparse.ArgumentTypeError):
                runtime.cli_port(value)
        self.assertEqual(15443, runtime.cli_port('15443'))

    def test_configuration_rejects_wrong_host_scheme_issuer_port_listener_and_database(self):
        changes = [
            lambda c: c['Authentication']['Oidc'].update(Authority='https://127.0.0.1:18443/realms/poseidon-lab'),
            lambda c: c['Authentication']['Oidc'].update(Issuer='https://localhost:8443/realms/poseidon-lab'),
            lambda c: c['Authentication']['Oidc'].update(Authority='http://localhost:18443/realms/poseidon-lab'),
            lambda c: c['DevelopmentLab'].update(OidcPort='18443'),
            lambda c: c['Kestrel']['Endpoints'].update(Other={'Url': 'https://localhost:19999'}),
            lambda c: c['Kestrel']['Endpoints']['LocalLab'].update(Url='https://localhost:5443'),
            lambda c: c['ConnectionStrings'].update(Poseidon=c['ConnectionStrings']['Poseidon'] + ';Host=external.invalid'),
            lambda c: c['ConnectionStrings'].update(Poseidon=c['ConnectionStrings']['Poseidon'] + ';host=external.invalid'),
            lambda c: c['ConnectionStrings'].update(Poseidon=c['ConnectionStrings']['Poseidon'] + ';Server=external.invalid'),
            lambda c: c['ConnectionStrings'].update(Poseidon=c['ConnectionStrings']['Poseidon'].replace('Port=25432', 'Port=55432')),
            lambda c: c.update(Authentication=[]),
        ]
        for change in changes:
            candidate = deepcopy(self.config)
            change(candidate)
            with self.assertRaisesRegex(RuntimeError, '^owned_lab_runtime_invalid$'):
                runtime.configured(candidate)

    def test_descriptor_requires_same_plan_and_selected_generation_and_never_falls_back(self):
        self.publish_metadata()
        self.assertEqual(self.runtime, runtime.validate_selected(self.config, self.root))
        store.publish_config(self.config)
        self.assertEqual(self.config, store.selected()[0])
        changed = deepcopy(self.config)
        changed['DevelopmentLab']['OwnedRuntime']['runId'] = str(uuid.uuid4())
        with self.assertRaisesRegex(RuntimeError, 'identity_changed'):
            store.publish_config(changed)
        (self.root / runtime.FILE).unlink()
        with self.assertRaisesRegex(RuntimeError, '^owned_lab_runtime_invalid$'):
            store.selected()
        self.publish_metadata()
        plan = json.loads((self.root / 'source-bundle-plan.json').read_text())
        plan['runId'] = str(uuid.uuid4())
        (self.root / 'source-bundle-plan.json').write_text(json.dumps(plan), encoding='utf-8')
        with self.assertRaises(RuntimeError):
            runtime.read(self.root)

    def test_raw_decimal_and_exponent_descriptor_ports_are_not_native_integers(self):
        self.publish_metadata()
        file = self.root / runtime.FILE
        raw = file.read_text(encoding='utf-8')
        for spelling in ('15443.0', '1.5443e4'):
            file.write_text(raw.replace('"appPort": 15443', '"appPort": ' + spelling), encoding='utf-8')
            with self.assertRaisesRegex(RuntimeError, '^owned_lab_runtime_invalid$'):
                runtime.read(self.root)

    def test_descriptor_loader_rejects_duplicate_escaped_key_large_file_link_and_directory(self):
        self.publish_metadata()
        file = self.root / runtime.FILE
        raw = file.read_text(encoding='utf-8')
        file.write_text(raw[:-1] + ',"app\\u0050ort":15443}', encoding='utf-8')
        with self.assertRaises(RuntimeError):
            runtime.read(self.root)
        file.write_text(' ' * 4097, encoding='utf-8')
        with self.assertRaises(RuntimeError):
            runtime.read(self.root)
        file.write_text(raw, encoding='utf-8')
        link = self.root / 'linked.json'
        link.hardlink_to(file)
        with self.assertRaises(RuntimeError):
            runtime.read(self.root)
        link.unlink()
        file.unlink()
        file.mkdir()
        with self.assertRaises(RuntimeError):
            runtime.read(self.root)

    def test_explicit_genesis_publishes_one_matching_runtime_across_plan_config_and_generation(self):
        def certificates(*_args, **_kwargs):
            for name in ('ca.pem', 'tls.pem', 'tls.key'):
                (self.root / name).write_text('SYNTHETIC path fixture; not a trusted certificate', encoding='utf-8')
        with patch.object(runtime, 'preflight'), patch.object(lab, 'selected_compose_ownership') as ownership, patch.object(lab, 'private_directory'), patch.object(lab, 'run', side_effect=certificates):
            bundle.init_source_bundle(15443, 18443, 25432)
        ownership.assert_called_once()
        descriptor = runtime.read(self.root)
        self.assertEqual((15443, 18443, 25432), tuple(descriptor[key] for key in runtime.DEFAULTS))
        genesis = json.loads((self.root / 'appsettings.json').read_text(encoding='utf-8'))
        self.assertEqual(descriptor, runtime.configured(genesis))
        self.assertEqual(genesis, store.selected()[0])
        realm = json.loads((self.root / 'realm/poseidon-lab-realm.json').read_text(encoding='utf-8'))
        self.assertEqual(['https://localhost:15443/signin-oidc'], realm['clients'][0]['redirectUris'])

    def test_explicit_genesis_rejects_stale_resources_before_private_writes(self):
        with patch.object(runtime, 'preflight'), patch.object(lab, 'selected_compose_ownership', side_effect=RuntimeError('owned_lab_volume_boundary')), patch.object(lab, 'private_directory') as directory:
            with self.assertRaisesRegex(RuntimeError, 'volume_boundary'):
                bundle.init_source_bundle(15443, 18443, 25432)
            directory.assert_not_called()
        self.assertEqual([], list(self.root.iterdir()))

    def test_partial_selection_foreign_listener_or_stale_private_fails_before_publication(self):
        with patch.object(lab, 'private_directory') as directory:
            with self.assertRaisesRegex(RuntimeError, 'all_ports_required'):
                bundle.init_source_bundle(15443, None, 25432)
            with patch.object(runtime, 'port_free', side_effect=[True, False]):
                with self.assertRaisesRegex(RuntimeError, 'port_in_use'):
                    bundle.init_source_bundle(15443, 18443, 25432)
            directory.assert_not_called()
        self.assertEqual([], list(self.root.iterdir()))
        (self.root / 'prior-owned-state').write_text('preserve', encoding='utf-8')
        with patch.object(runtime, 'preflight') as preflight, patch.object(lab, 'private_directory') as directory:
            with self.assertRaisesRegex(RuntimeError, 'fresh_disposable_bundle_profile_required'):
                bundle.init_source_bundle(15443, 18443, 25432)
            preflight.assert_not_called()
            directory.assert_not_called()

    def test_start_rejects_foreign_port_before_init_compose_or_migration(self):
        for index in range(3):
            available = [True] * 3
            available[index] = False
            with patch.object(runtime, 'port_free', side_effect=available), patch.object(lab, 'compose_owns', return_value=False), patch.object(lab, 'init') as init, patch.object(lab, 'compose') as compose, patch.object(lab, 'run') as run:
                with self.assertRaisesRegex(RuntimeError, 'port_in_use'):
                    lab.start()
                init.assert_not_called()
                compose.assert_not_called()
                run.assert_not_called()

    def test_preflight_allows_only_exact_owned_app_and_service_callbacks(self):
        with patch.object(runtime, 'port_free', return_value=False):
            seen = []
            runtime.preflight(self.runtime, lambda service, port: seen.append((service, port)) or True, app_owned=True)
            self.assertEqual([('provider', 18443), ('database', 25432)], seen)
            with self.assertRaisesRegex(RuntimeError, 'port_in_use'):
                runtime.preflight(self.runtime, lambda *_: True, app_owned=False)

    def test_ipv6_listener_cannot_be_adopted_by_an_owned_ipv4_provider_or_database(self):
        for index in range(3):
            available = [True] * 3
            available[index] = False
            with patch.object(runtime, 'ipv6_free', side_effect=available), patch.object(runtime, 'port_free', return_value=True), patch.object(lab, 'compose_owns', return_value=True) as owns:
                with self.assertRaisesRegex(RuntimeError, 'port_in_use'):
                    runtime.preflight(self.runtime, owns)
                owns.assert_not_called()

    def test_compose_ownership_requires_current_run_project_service_and_exact_loopback_binding(self):
        labels = {'com.docker.compose.project': lab.PROJECT, 'com.docker.compose.service': 'provider',
                  'poseidon.lab.run-id': self.runtime['runId'], 'poseidon.lab.runtime-identity': runtime.runtime_identity(self.runtime)}
        bindings = {'8443/tcp': [{'HostIp': '127.0.0.1', 'HostPort': '18443'}], '8080/tcp': None}
        def observed(current_labels, current_bindings, running=True):
            values = [subprocess.CompletedProcess([], 0, ('a' * 64).encode()),
                      subprocess.CompletedProcess([], 0, ('|'.join(json.dumps(x) for x in (current_labels, current_bindings, running, {'8443/tcp': current_bindings.get('8443/tcp')}))).encode())]
            with patch.object(lab.subprocess, 'run', side_effect=values) as docker:
                result = lab.compose_owns('provider', 18443)
                self.assertNotIn('.Config.Env', str(docker.call_args_list))
                return result
        self.assertTrue(observed(labels, bindings))
        for key, value in (('com.docker.compose.project', 'foreign'), ('com.docker.compose.service', 'database'),
                           ('poseidon.lab.run-id', str(uuid.uuid4())), ('poseidon.lab.runtime-identity', 'legacy')):
            self.assertFalse(observed({**labels, key: value}, bindings))
        missing_run = {k: v for k, v in labels.items() if k != 'poseidon.lab.run-id'}
        self.assertFalse(observed(missing_run, bindings))
        for host, port in (('0.0.0.0', '18443'), ('127.0.0.1', '8443')):
            self.assertFalse(observed(labels, {'8443/tcp': [{'HostIp': host, 'HostPort': port}]}))
        self.assertFalse(observed(labels, bindings, False))
        self.assertFalse(observed(labels, {**bindings, '8080/tcp': [{'HostIp': '127.0.0.1', 'HostPort': '18080'}]}))

    def test_retained_volumes_require_current_run_and_immutable_runtime_labels(self):
        name = lab.PROJECT + '_database-data'
        labels = {'com.docker.compose.project': lab.PROJECT, 'com.docker.compose.volume': 'database-data',
                  'poseidon.lab.run-id': self.runtime['runId'], 'poseidon.lab.runtime-identity': runtime.runtime_identity(self.runtime)}
        with patch.object(lab, 'docker_metadata', side_effect=[name, json.dumps(labels)]):
            self.assertTrue(lab.compose_volume_owns('database-data'))
        for changed in ({**labels, 'poseidon.lab.run-id': str(uuid.uuid4())},
                        {**labels, 'com.docker.compose.project': 'foreign'},
                        {k: v for k, v in labels.items() if k != 'poseidon.lab.runtime-identity'}):
            with patch.object(lab, 'docker_metadata', side_effect=[name, json.dumps(changed)]):
                self.assertFalse(lab.compose_volume_owns('database-data'))
        with patch.object(lab, 'docker_metadata', return_value=''):
            self.assertTrue(lab.compose_volume_owns('database-data'))

    def test_free_ports_do_not_adopt_stale_stopped_services_or_retained_volumes(self):
        for service_owned, volume_owned, reason in ((False, True, 'service_boundary'), (True, False, 'volume_boundary')):
            with patch.object(runtime, 'port_free', return_value=True), patch.object(lab, 'compose_owns', return_value=service_owned), patch.object(lab, 'compose_volume_owns', return_value=volume_owned), patch.object(lab, 'init') as init, patch.object(lab, 'compose') as compose:
                with self.assertRaisesRegex(RuntimeError, reason):
                    lab.start()
                init.assert_not_called()
                compose.assert_not_called()

    def test_selected_stop_rejects_stale_service_before_stopping_app_or_compose(self):
        with patch.object(lab, 'compose_owns', return_value=False), patch.object(lab, 'owned_process') as app, patch.object(lab, 'compose') as compose:
            with self.assertRaisesRegex(RuntimeError, 'service_boundary'):
                lab.stop()
            app.assert_not_called()
            compose.assert_not_called()

    def test_ambient_endpoint_and_runtime_overrides_are_removed_from_application_environment(self):
        ambient = {'ASPNETCORE_URLS': 'https://external.invalid', 'DevelopmentLab__OidcPort': '9999',
                   'POSEIDON_LAB_PROVIDER_PORT': '9999', 'POSEIDON_LAB_DATABASE_PORT': '9998',
                   'POSEIDON_LAB_RUNTIME_FILE': 'foreign', 'POSEIDON_REGISTER_URL': 'https://external.invalid'}
        with patch.dict(lab.os.environ, ambient, clear=True), patch.object(lab, 'root_tool', return_value='/synthetic/dotnet'):
            environment = lab.environment()
        self.assertTrue(set(ambient).isdisjoint(environment))


if __name__ == '__main__':
    unittest.main()
