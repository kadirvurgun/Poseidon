import json
import subprocess
import unittest
from unittest.mock import MagicMock, patch

import api_test_gate as gate
from test_critical_write_coverage import CoverageFixture


class ApiTestGateTests(unittest.TestCase):
    discovery = b'    Poseidon.Api.Tests.Cases.Actual_case(first)\n    Poseidon.Api.Tests.Cases.Actual_case(second)\n'

    def setUp(self):
        # Native runner output is mocked in these parser tests. Explicit synthetic
        # temp observations exercise the REAL coverage evaluator, never a default bypass.
        self.coverage = CoverageFixture(); self.addCleanup(self.coverage.close)
        target = patch.object(gate.critical_write_coverage, 'verify_target'); target.start(); self.addCleanup(target.stop)

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
        self.coverage.write(self.coverage.report())
        with patch.object(gate, 'root_tool', return_value='dotnet'), \
                patch.object(gate.critical_write_coverage, 'prepare', return_value=self.coverage.run), \
                patch.object(gate, 'capture', side_effect=results) as run:
            code, evidence = gate.run('integration')
            return code, evidence, run

    def test_only_complete_nonempty_discovered_scope_can_pass(self):
        code, evidence, run = self.execute(b'total: 2\nfailed: 0\nsucceeded: 2\nskipped: 0\n')
        self.assertEqual(0, code)
        self.assertEqual('PASS_EXACT_DISCOVERED_SCOPE', evidence['status'])
        self.assertFalse(evidence['raw_output_persisted'])
        self.assertEqual('PASS_ACTUAL_CURRENT_CRITICAL_SCOPE', evidence['critical_write_coverage']['status'])
        self.assertEqual(2, run.call_count)
        for call in run.call_args_list:
            self.assertIn('--filter-trait', call.args[0])
            self.assertIn('Category=Integration', call.args[0])
        self.assertFalse(any(key.startswith(gate.critical_write_coverage.ENV_PREFIX) for key in run.call_args_list[0].kwargs['env']))
        self.assertEqual(self.coverage.run.run_id, run.call_args_list[1].kwargs['env'][gate.critical_write_coverage.ENV_PREFIX + 'RUN_ID'])

    def test_native_success_cannot_pass_when_actual_write_observations_are_missing(self):
        results = [subprocess.CompletedProcess([], 0, self.discovery),
                   subprocess.CompletedProcess([], 0, b'total: 2\nfailed: 0\nsucceeded: 2\nskipped: 0\n')]
        with patch.object(gate, 'root_tool', return_value='dotnet'), \
                patch.object(gate.critical_write_coverage, 'prepare', return_value=self.coverage.run), \
                patch.object(gate, 'capture', side_effect=results):
            code, evidence = gate.run('integration')
        self.assertEqual(2, code)
        self.assertEqual('CRITICAL_WRITE_COVERAGE_FAILED_OR_UNPROVEN', evidence['status'])
        self.assertIn('MISSING_OR_EXCESSIVE_CAPTURE_REPORTS', evidence['critical_write_coverage']['errors'])

    def test_unit_scope_cannot_inherit_stale_coverage_or_claim_integration_coverage(self):
        results = [subprocess.CompletedProcess([], 0, self.discovery),
                   subprocess.CompletedProcess([], 0, b'total: 2\nfailed: 0\nsucceeded: 2\nskipped: 0\n')]
        prefix = gate.critical_write_coverage.ENV_PREFIX
        with patch.dict(gate.os.environ, {prefix + 'DIRECTORY': 'stale-private-path'}), \
                patch.object(gate, 'root_tool', return_value='dotnet'), \
                patch.object(gate.critical_write_coverage, 'prepare') as prepare, \
                patch.object(gate, 'capture', side_effect=results) as capture:
            code, evidence = gate.run('unit')
        self.assertEqual(0, code); prepare.assert_not_called()
        self.assertNotIn('critical_write_coverage', evidence)
        for call in capture.call_args_list:
            self.assertFalse(any(key.startswith(prefix) for key in call.kwargs['env']))

    def test_unsafe_or_unpinned_coverage_setup_cannot_launch_execution(self):
        with patch.object(gate, 'root_tool', return_value='dotnet'), \
                patch.object(gate.critical_write_coverage, 'prepare', side_effect=gate.critical_write_coverage.CoverageInputError('TARGET_NOT_CLEAN_AND_PINNED')), \
                patch.object(gate, 'capture', return_value=subprocess.CompletedProcess([], 0, self.discovery)) as capture:
            code, evidence = gate.run('integration')
        self.assertEqual(2, code); self.assertEqual(1, capture.call_count)
        self.assertEqual('CRITICAL_WRITE_COVERAGE_SETUP_FAILED', evidence['status'])

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
        with patch.object(gate, 'root_tool', return_value='dotnet'), \
                patch.object(gate.critical_write_coverage, 'prepare', return_value=self.coverage.run), \
                patch.object(gate, 'capture', side_effect=[
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


class ApiShardGateTests(unittest.TestCase):
    setUp = ApiTestGateTests.setUp
    full = ['Poseidon.Api.Tests.Cases.Theory', 'Poseidon.Api.Tests.Cases.Theory',
            'Poseidon.Api.Tests.Cases.A', 'Poseidon.Api.Tests.Cases.B']

    @staticmethod
    def listed(names):
        return ('\n'.join(names) + '\n').encode()

    @staticmethod
    def summary(count, failed=0, skipped=0):
        return f'total: {count}\nfailed: {failed}\nsucceeded: {count-failed-skipped}\nskipped: {skipped}\n'.encode()

    def execute_shards(self, mutate=None, outputs=None, discoveries=None):
        groups = gate.partition_methods(self.full, 3)
        actual = [list(group.elements()) for group in groups]
        if mutate: mutate(actual)
        self.coverage.write(self.coverage.report())
        executions = []
        def capture(args, root, timeout, env=None):
            self.assertGreater(timeout, 0); self.assertLessEqual(timeout, 1800)
            if '--filter-method' not in args:
                return subprocess.CompletedProcess([], 0, self.listed(self.full))
            methods = args[args.index('--filter-method')+1:]
            methods = methods[:next((i for i, name in enumerate(methods) if name.startswith('--')), len(methods))]
            index = next(i for i, group in enumerate(groups) if sorted(group) == methods)
            if '--list-tests' in args:
                if discoveries and index in discoveries:
                    value = discoveries[index]
                    if isinstance(value, Exception): raise value
                    return value
                self.assertFalse(any(k.startswith(gate.critical_write_coverage.ENV_PREFIX) for k in env))
                return subprocess.CompletedProcess([], 0, self.listed(actual[index]))
            executions.append((index, env[gate.critical_write_coverage.ENV_PREFIX+'RUN_ID']))
            if outputs and index in outputs:
                result = outputs[index]
                if isinstance(result, Exception): raise result
                return result
            return subprocess.CompletedProcess([], 0, self.summary(groups[index].total()))
        with patch.object(gate, 'root_tool', return_value='dotnet'), \
                patch.object(gate.critical_write_coverage, 'prepare', return_value=self.coverage.run) as prepare, \
                patch.object(gate.critical_write_coverage, 'evaluate', wraps=gate.critical_write_coverage.evaluate) as evaluate, \
                patch.object(gate, 'capture', side_effect=capture):
            code, report = gate.run('integration', shards=3)
        return code, report, executions, prepare, evaluate

    def test_parallel_exact_theory_multiset_uses_single_coverage_run(self):
        code, report, executions, prepare, evaluate = self.execute_shards()
        self.assertEqual(0, code)
        self.assertEqual('EXACT_DISJOINT_COMPLETE_MULTISET', report['partition']['status'])
        self.assertEqual([2, 1, 1], report['partition']['cases'])
        self.assertEqual([4], report['count_observations']['succeeded'])
        self.assertEqual(3, len(executions))
        self.assertEqual({self.coverage.run.run_id}, {run_id for _, run_id in executions})
        prepare.assert_called_once(); evaluate.assert_called_once()

    def test_complete_sharded_evidence_is_accepted_by_the_independent_native_scope_validator(self):
        from copy import deepcopy
        code, report, _, _, _ = self.execute_shards()
        self.assertEqual(0, code)
        self.assertEqual(0, report['exit_code'])
        self.assertTrue(gate.critical_write_coverage.native_scope_complete(report, self.coverage.run))
        for exit_code in (None, True, 1, 2):
            altered = deepcopy(report); altered['exit_code'] = exit_code
            self.assertFalse(gate.critical_write_coverage.native_scope_complete(altered, self.coverage.run))
        altered = deepcopy(report); altered.pop('exit_code')
        self.assertFalse(gate.critical_write_coverage.native_scope_complete(altered, self.coverage.run))
        altered = deepcopy(report); altered['count_observations']['skipped'] = [1]
        self.assertFalse(gate.critical_write_coverage.native_scope_complete(altered, self.coverage.run))

    def test_partial_overlap_foreign_empty_and_theory_multiplicity_fail_before_execution(self):
        def missing(groups): groups[0].pop()
        def overlap(groups): groups[1].append(groups[0][0])
        def foreign(groups): groups[0][0] = 'Poseidon.Api.Tests.Cases.Foreign'
        def empty(groups): groups[1].clear()
        def swapped(groups): groups[0], groups[1] = groups[1], groups[0]
        for mutate in (missing, overlap, foreign, empty, swapped):
            with self.subTest(mutate=mutate.__name__):
                code, report, executions, prepare, evaluate = self.execute_shards(mutate=mutate)
                self.assertEqual(2, code)
                self.assertEqual('SHARD_DISCOVERY_PARTITION_FAILED', report['status'])
                self.assertEqual([], executions); prepare.assert_not_called(); evaluate.assert_not_called()

    def test_failed_skipped_partial_and_duplicate_summary_cannot_hide_other_shards(self):
        for output, exit_code in ((self.summary(2, failed=1), 1), (self.summary(2, skipped=1), 0),
                                  (b'', 0), (self.summary(1), 0), (self.summary(2)*2, 0)):
            with self.subTest(output=output):
                code, report, executions, _, evaluate = self.execute_shards(
                    outputs={0: subprocess.CompletedProcess([], exit_code, output)})
                self.assertNotEqual(0, code); self.assertEqual(3, len(executions))
                self.assertNotEqual(0, report['exit_code'])
                self.assertNotEqual('PASS_EXACT_DISCOVERED_SCOPE', report['status']); evaluate.assert_not_called()
                if output in (b'', self.summary(2)*2): self.assertNotIn('count_observations', report)

    def test_parallel_deadline_collects_every_shard_but_cannot_evaluate_coverage(self):
        code, report, executions, _, evaluate = self.execute_shards(
            outputs={0: subprocess.TimeoutExpired('dotnet', 1, output=b'private-parameter')})
        self.assertEqual(2, code); self.assertEqual(3, len(executions))
        self.assertEqual('INTERRUPTED_DEADLINE', report['status']); evaluate.assert_not_called()
        self.assertNotIn('count_observations', report); self.assertNotIn('private-parameter', json.dumps(report))

    def test_parallel_exception_collects_every_shard_and_cannot_claim_summary(self):
        code, report, executions, _, evaluate = self.execute_shards(outputs={1: OSError('private-message')})
        self.assertEqual(2, code); self.assertEqual(3, len(executions))
        self.assertEqual('GATE_EXECUTION_FAILED', report['status']); evaluate.assert_not_called()
        self.assertNotIn('private-message', json.dumps(report)); self.assertNotIn('count_observations', report)

    def test_method_filter_preserves_individual_arguments_and_command_limit_is_closed(self):
        args = gate.method_command(['dotnet', 'test'], {'Poseidon.Api.Tests.Cases.A': 1})
        self.assertEqual(['dotnet', 'test', '--filter-method', 'Poseidon.Api.Tests.Cases.A'], args)
        with self.assertRaises(ValueError): gate.method_command(['x'*30000], {'Poseidon.Api.Tests.Cases.A': 1})

    def test_invalid_shard_config_cannot_launch_or_weaken_scope(self):
        with patch.object(gate, 'capture') as capture:
            for scope, shards in (('unit', 2), ('all', 4), ('integration', 0), ('integration', 5), ('integration', True)):
                with self.assertRaises(ValueError): gate.run(scope, shards=shards)
            capture.assert_not_called()

    def test_single_global_deadline_includes_discovery_and_setup(self):
        with patch.object(gate, 'root_tool', return_value='dotnet'), \
                patch.object(gate.time, 'monotonic', side_effect=[0, 0, 1801, 1801]), \
                patch.object(gate.critical_write_coverage, 'prepare', return_value=self.coverage.run), \
                patch.object(gate, 'capture', return_value=subprocess.CompletedProcess([], 0, self.listed(self.full))) as capture:
            code, report = gate.run('integration')
        self.assertEqual(2, code); self.assertEqual('INTERRUPTED_DEADLINE', report['status'])
        self.assertEqual(1, capture.call_count)

    def test_ci_uses_four_integration_shards_and_executes_new_node_fixture_unit_gate(self):
        workflow = (gate.ROOT / '.github/workflows/backend-foundation.yml').read_text(encoding='utf-8')
        self.assertIn('POSEIDON_API_GATE_SHARDS: 4', workflow)
        self.assertIn('node --test tests/Poseidon.Browser.Tests/lab/register-lane-fixture.test.ts', workflow)

    def test_failed_native_shard_discovery_never_prepares_or_executes(self):
        result = subprocess.CompletedProcess([], 1, self.listed([self.full[0], self.full[0]]))
        code, report, executions, prepare, evaluate = self.execute_shards(discoveries={0: result})
        self.assertEqual(2, code); self.assertEqual('SHARD_DISCOVERY_PARTITION_FAILED', report['status'])
        self.assertEqual([], executions); prepare.assert_not_called(); evaluate.assert_not_called()

    def test_shard_discovery_deadline_or_process_failure_never_prepares_coverage(self):
        for failure, status in ((subprocess.TimeoutExpired('dotnet', 1), 'INTERRUPTED_DEADLINE'),
                                (OSError('private-discovery-message'), 'GATE_EXECUTION_FAILED')):
            code, report, executions, prepare, evaluate = self.execute_shards(discoveries={0: failure})
            self.assertEqual(2, code); self.assertEqual(status, report['status'])
            self.assertEqual([], executions); prepare.assert_not_called(); evaluate.assert_not_called()
            self.assertNotIn('private-discovery-message', json.dumps(report))
