import json
import subprocess
import unittest
from unittest.mock import MagicMock, patch

import api_test_gate as gate


class ApiTestGateTests(unittest.TestCase):
    discovery = b'    Poseidon.Api.Tests.Cases.Actual_case(first)\n    Poseidon.Api.Tests.Cases.Actual_case(second)\n'

    def test_failure_locations_only_emit_existing_test_filenames_and_line_numbers(self):
        raw = (b'private/request/TodayMaintenanceTests.cs:line 21 secret request body\n'
               b'unknown/ForeignDiagnostic.cs:12 private body\n'
               b'private/TodayMaintenanceTests.cs:21\n')
        result = gate.failure_locations(raw, gate.ROOT)
        self.assertEqual([{'file': 'TodayMaintenanceTests.cs', 'line': 21}], result)
        self.assertNotIn('private', json.dumps(result))
        self.assertNotIn('secret', json.dumps(result))

    def execute(self, output, exit_code=0, discovery=None):
        results = [subprocess.CompletedProcess([], 0, self.discovery if discovery is None else discovery),
                   subprocess.CompletedProcess([], exit_code, output)]
        with patch.object(gate, 'root_tool', return_value='dotnet'), patch.object(gate, 'capture', side_effect=results) as run:
            code, evidence = gate.run('integration')
            return code, evidence, run

    def test_only_complete_nonempty_discovered_scope_can_pass(self):
        code, evidence, run = self.execute(b'total: 2\nfailed: 0\nsucceeded: 2\nskipped: 0\n')
        self.assertEqual(0, code)
        self.assertEqual('PASS_EXACT_DISCOVERED_SCOPE', evidence['status'])
        self.assertFalse(evidence['raw_output_persisted'])
        self.assertEqual(2, run.call_count)
        for call in run.call_args_list:
            self.assertIn('--filter-trait', call.args[0])
            self.assertIn('Category=Integration', call.args[0])

    def test_success_exit_does_not_hide_skipped_missing_duplicate_or_partial_summaries(self):
        for output in (b'', b'total: 1\nfailed: 0\nsucceeded: 1\nskipped: 0\n',
                       b'total: 2\nfailed: 0\nsucceeded: 1\nskipped: 1\n',
                       b'total: 2\ntotal: 2\nfailed: 0\nsucceeded: 2\nskipped: 0\n'):
            code, evidence, _ = self.execute(output)
            self.assertNotEqual(0, code)
            self.assertNotEqual('PASS_EXACT_DISCOVERED_SCOPE', evidence['status'])

    def test_empty_discovery_cannot_execute_or_pass(self):
        code, evidence, run = self.execute(b'', discovery=b'No test cases')
        self.assertEqual(2, code)
        self.assertEqual(1, run.call_count)
        self.assertEqual('DISCOVERY_FAILED_OR_EMPTY', evidence['status'])

    def test_failure_emits_only_discovered_static_identifier_without_parameters_or_diagnostics(self):
        marker = b'private-diagnostic-parameter'
        output = b'failed Actual_case(' + marker + b')\nSDK failure ' + marker + b'\ntotal: 2\nfailed: 1\nsucceeded: 1\nskipped: 0\n'
        code, evidence, _ = self.execute(output, exit_code=1)
        self.assertEqual(1, code)
        self.assertEqual(['Actual_case'], evidence['failed_static_test_names'])
        self.assertNotIn(marker.decode(), json.dumps(evidence))

    def test_invalid_scope_and_deadline_never_launch_a_child(self):
        with patch.object(gate, 'capture') as run:
            for scope, timeout in (('foreign', 10), ('all', 0), ('all', True), ('all', 1801)):
                with self.assertRaises(ValueError): gate.run(scope, timeout=timeout)
            run.assert_not_called()

    def test_deadline_is_nonpassing_and_never_exports_partial_output(self):
        with patch.object(gate, 'root_tool', return_value='dotnet'), \
                patch.object(gate, 'capture', side_effect=subprocess.TimeoutExpired('dotnet', 1, output=b'private-timeout-body')):
            code, evidence = gate.run('all')
        self.assertEqual(2, code)
        self.assertEqual('INTERRUPTED_DEADLINE', evidence['status'])
        self.assertNotIn('private-timeout-body', json.dumps(evidence))

    def test_capture_deadline_stops_only_its_owned_process_tree_before_returning(self):
        process = MagicMock()
        process.communicate.side_effect = subprocess.TimeoutExpired('dotnet', 1)
        process.poll.return_value = None
        with patch.object(gate.subprocess, 'Popen', return_value=process), patch.object(gate.Workers, 'stop') as stop:
            with self.assertRaises(subprocess.TimeoutExpired): gate.capture(['dotnet'], gate.ROOT, 1)
        stop.assert_called_once_with(process)
        process.stdout.close.assert_called_once()

    def test_execution_deadline_exposes_only_discovered_identifiers_and_stays_nonpassing(self):
        discovery = b'Poseidon.Api.Tests.Cases.Actual_case\n'
        partial = (b'Poseidon.Api.Tests.Cases.Actual_case(private-parameter)\n'
                   b'Poseidon.Api.Tests.Foreign.secret_name\nprivate-diagnostic-body\n'
                   b'total: 1\nfailed: 0\nsucceeded: 1\nskipped: 0\n')
        with patch.object(gate, 'root_tool', return_value='dotnet'), patch.object(gate, 'capture', side_effect=[
                subprocess.CompletedProcess([], 0, discovery), subprocess.TimeoutExpired('dotnet', 1, output=partial)]) as capture:
            code, evidence = gate.run('integration')
        self.assertEqual(2, code)
        self.assertEqual('INTERRUPTED_DEADLINE', evidence['status'])
        self.assertEqual('execution', evidence['interrupted_stage'])
        detail = evidence['deadline_diagnostics']
        self.assertEqual(['Poseidon.Api.Tests.Cases.Actual_case'], detail['last_static_identifiers'])
        self.assertEqual('diagnostic_occurrence_only', detail['identifier_semantics'])
        self.assertEqual([1], detail['partial_count_observations']['succeeded'])
        self.assertNotIn('private-', json.dumps(evidence))
        self.assertNotIn('secret_name', json.dumps(evidence))
        self.assertIn('--output', capture.call_args_list[1].args[0])
        self.assertIn('Category=Integration', capture.call_args_list[1].args[0])

    def test_discovery_deadline_cannot_authorize_output_names(self):
        with patch.object(gate, 'root_tool', return_value='dotnet'), patch.object(gate, 'capture',
                side_effect=subprocess.TimeoutExpired('dotnet', 1, output=b'Poseidon.Api.Tests.Cases.Untrusted')):
            code, evidence = gate.run('all')
        self.assertEqual(2, code)
        self.assertEqual('discovery', evidence['interrupted_stage'])
        self.assertEqual([], evidence['deadline_diagnostics']['last_static_identifiers'])

    def test_partial_diagnostic_names_are_bounded_and_repeated_names_move_to_end(self):
        names = ['Poseidon.Api.Tests.Cases.Test' + str(i) for i in range(12)]
        output = ('\n'.join(names + [names[0]])).encode()
        detail = gate.deadline_diagnostics(output, names)
        self.assertEqual(12, detail['static_identifier_count'])
        self.assertEqual(names[5:] + [names[0]], detail['last_static_identifiers'])
        self.assertEqual([], gate.observations(b'total: ' + b'9' * 5000)['total'])

    def test_required_ci_uses_the_real_owned_provider_and_value_free_scope_gate(self):
        workflow = (gate.ROOT / '.github/workflows/backend-foundation.yml').read_text(encoding='utf-8')
        self.assertIn('docker build --iidfile .runtime/s3-gate-image-id --file infra/s3-gate/Dockerfile infra/s3-gate', workflow)
        self.assertIn('python3 scripts/api_test_gate.py --scope unit', workflow)
        self.assertIn('python3 scripts/s3_gate.py --image "$(cat .runtime/s3-gate-image-id)" --child-timeout 1980 -- python3 scripts/api_test_gate.py --scope integration', workflow)
        self.assertLess(workflow.index('Build pinned source S3 provider'), workflow.index('name: PostgreSQL integration tests'))

    def test_source_provider_build_has_byte_and_builder_pins_without_persisted_credentials(self):
        dockerfile = (gate.ROOT / 'infra/s3-gate/Dockerfile').read_text(encoding='utf-8')
        self.assertRegex(dockerfile, r'FROM docker.io/library/golang:[^\s]+@sha256:[0-9a-f]{64} AS build')
        self.assertRegex(dockerfile, r'ADD --checksum=sha256:[0-9a-f]{64} https://codeload.github.com/minio/minio/tar.gz/[0-9a-f]{40}')
        self.assertIn('go build -mod=readonly', dockerfile)
        self.assertIn('USER 65532:65532', dockerfile)
        self.assertNotIn('MINIO_ROOT_', dockerfile)
