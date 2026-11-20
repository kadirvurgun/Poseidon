import json
import subprocess
import tempfile
import unittest
from pathlib import Path

import perf_baseline as perf


def raw(endpoints=None, slow=None):
    return {'schemaVersion': perf.RAW_SCHEMA, 'fixture': {'vessels': 10, 'workOrders': 1000, 'evidence': 'NOT_INCLUDED'},
            'transport': 'in-process', 'iterations': 5, 'warmup': 1,
            'endpoints': endpoints if endpoints is not None else [
                {'key': 'today', 'surface': 'Today', 'requestsPerSample': 1, 'samplesMs': [10, 20, 30, 40, 50],
                 'statuses': [200] * 5, 'dbCommands': [4, 4, 5, 4, 4]}],
            'slowQueries': slow or []}


class PerfBaselineTests(unittest.TestCase):
    def args(self, *extra):
        return perf.parser().parse_args(list(extra))

    def test_nearest_rank_percentiles(self):
        values = list(range(1, 101))
        self.assertEqual(50, perf.percentile(values, 0.50))
        self.assertEqual(95, perf.percentile(values, 0.95))
        self.assertEqual(7, perf.percentile([7], 0.95))
        self.assertEqual(5, perf.percentile([5, 1, 4, 2, 3], 0.95))
        with self.assertRaises(ValueError):
            perf.percentile([], 0.5)

    def test_envelope_reports_measured_values_without_sla_claim(self):
        envelope = perf.build_envelope(raw(), {'schemaVersion': perf.SCHEMA, 'slaClaim': False})
        today = envelope['endpoints'][0]
        self.assertEqual(('MEASURED', 30, 50, 10, 50, 30.0), (today['state'], today['p50Ms'], today['p95Ms'], today['minMs'], today['maxMs'], today['meanMs']))
        self.assertEqual({'p50': 4, 'max': 5}, today['dbCommandsPerSample'])
        self.assertEqual('MEASURED', envelope['status'])
        self.assertFalse(envelope['slaClaim'])
        self.assertEqual([], envelope['notMeasuredSuccessPath'])

    def test_error_path_samples_are_never_reported_as_a_baseline(self):
        endpoint = {'key': 'audit-ready-chains', 'samplesMs': [1, 2, 3], 'statuses': [200, 403, 200]}
        envelope = perf.build_envelope(raw([endpoint]), {})
        row = envelope['endpoints'][0]
        self.assertEqual('NOT_MEASURED_SUCCESS_PATH', row['state'])
        self.assertNotIn('p95Ms', row)
        self.assertEqual({'200': 2, '403': 1}, row['statusCounts'])
        self.assertEqual(['audit-ready-chains'], envelope['notMeasuredSuccessPath'])

    def test_slow_queries_keep_endpoint_and_bounded_sql_only_above_threshold(self):
        slow = [{'endpoint': 'today', 'durationMs': 750.5, 'sql': 'SELECT 1 ' * 1000},
                {'endpoint': 'today', 'durationMs': 499.0, 'sql': 'SELECT fast'},
                {'endpoint': 'fixture', 'durationMs': 1200, 'sql': 'INSERT ...'}]
        envelope = perf.build_envelope(raw(slow=slow), {})
        self.assertEqual(2, envelope['slowQueryCount'])
        self.assertEqual(['fixture', 'today'], [q['endpoint'] for q in envelope['slowQueries']])
        self.assertTrue(all(len(q['sql']) <= perf.SQL_LIMIT and len(q['sqlSha256']) == 64 for q in envelope['slowQueries']))

    def test_invalid_raw_samples_are_rejected(self):
        for bad in (raw([]), {**raw(), 'schemaVersion': 'other'},
                    raw([{'key': 'x', 'samplesMs': [1, 2], 'statuses': [200]}]),
                    raw([{'key': 'x', 'samplesMs': [-1], 'statuses': [200]}]),
                    raw([{'key': 'x', 'samplesMs': [True], 'statuses': [200]}])):
            with self.assertRaises(ValueError):
                perf.build_envelope(bad, {})

    def test_missing_docker_is_not_run_and_never_launches_the_test(self):
        def runner(*_a, **_k):
            raise AssertionError('must not launch')
        with tempfile.TemporaryDirectory() as root:
            code, envelope = perf.run(self.args(), Path(root), probe=lambda: False, runner=runner)
        self.assertEqual(3, code)
        self.assertEqual(('NOT_RUN', 'docker_unavailable'), (envelope['status'], envelope['reason']))
        self.assertEqual('NOT_AGREED', envelope['agreedEnvelope']['state'])

    def test_skip_measurement_is_not_run(self):
        with tempfile.TemporaryDirectory() as root:
            code, envelope = perf.run(self.args('--skip-measurement'), Path(root), probe=lambda: True)
        self.assertEqual((3, 'NOT_RUN'), (code, envelope['status']))

    def test_measurement_runs_only_the_performance_trait_and_consumes_raw_samples(self):
        calls = []
        def runner(command, cwd, env, capture_output, timeout):
            calls.append((command, env))
            Path(env['POSEIDON_PERF_OUTPUT']).write_text(json.dumps(raw()), encoding='utf-8')
            return subprocess.CompletedProcess(command, 0, b'private runner output')
        with tempfile.TemporaryDirectory() as root:
            code, envelope = perf.run(self.args('--iterations', '7', '--work-orders', '12'), Path(root), probe=lambda: True, runner=runner)
            self.assertEqual([], list((Path(root) / '.runtime/perf').glob('raw-*.json')))
        self.assertEqual(0, code)
        command, env = calls[0]
        self.assertEqual(['--filter-trait', 'Category=Performance'], command[-2:])
        self.assertEqual(('7', '12'), (env['POSEIDON_PERF_ITERATIONS'], env['POSEIDON_PERF_WORK_ORDERS']))
        self.assertFalse(envelope['runner']['raw_output_persisted'])
        self.assertNotIn('private runner output', json.dumps(envelope))

    def test_failed_test_run_is_failed_not_measured(self):
        def runner(command, **_k):
            return subprocess.CompletedProcess(command, 1, b'')
        with tempfile.TemporaryDirectory() as root:
            code, envelope = perf.run(self.args(), Path(root), probe=lambda: True, runner=runner)
        self.assertEqual((2, 'FAILED'), (code, envelope['status']))

    def test_written_envelope_has_timestamped_and_latest_copies(self):
        with tempfile.TemporaryDirectory() as root:
            path = perf.write(Path(root), {'status': 'NOT_RUN'})
            self.assertTrue(path.name.startswith('perf-baseline-'))
            self.assertEqual({'status': 'NOT_RUN'}, json.loads((Path(root) / '.runtime/perf/latest.json').read_text(encoding='utf-8')))


if __name__ == '__main__':
    unittest.main()
