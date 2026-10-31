"""Fixed verification command contract and fail-closed evidence, with no tools/network."""
import json
import io
import hashlib
from pathlib import Path
import subprocess
import tempfile
import time
import unittest
from unittest.mock import Mock, patch

from nightly_coordinator import Blocked, Coordinator, SubscriptionWorkers, queue_items, test_summary, strict_json
from nightly_python_tests import summary
from codex_accounts import Workers, summarize_account
from test_nightly_coordinator import FakeWorkers


class FixedGateTests(unittest.TestCase):
    def test_qa_json_rejects_duplicate_keys_and_non_json_constants(self):
        for text in ['{"approved":false,"approved":true}', '{"approved":NaN}']:
            with self.assertRaises(ValueError):
                strict_json(text)

    def test_only_exact_startup_hash_is_accepted_unknown_errors_and_tools_reject(self):
        text = 'Synthetic pinned benign startup diagnostic'
        digest = hashlib.sha256(text.encode()).hexdigest()
        final = {'approved': True, 'commit': 'a' * 40, 'issues': []}
        process = Mock(returncode=0)
        self.coordinator.job['calls'] = [{}]
        with patch.object(self.coordinator.workers, 'command', return_value=['fixture'], create=True), \
                patch('nightly_coordinator.subprocess.Popen', return_value=process), \
                patch('nightly_coordinator.QA_STARTUP_DIAGNOSTICS', {digest: 'fixture_startup'}):
            for kind, message, permitted in [('error', text, True), ('error', text + ' auth failed', False),
                    ('error', 'network failed', False), ('command_execution', '', False), ('mcp_tool_call', '', False)]:
                events = [{'type': 'item.completed', 'item': {'type': kind, 'message': message}},
                    {'type': 'item.completed', 'item': {'type': 'agent_message', 'text': json.dumps(final)}},
                    {'type': 'turn.completed', 'usage': {}}]
                process.communicate.return_value = ('\n'.join(json.dumps(e) for e in events), None)
                if permitted:
                    self.assertEqual(self.coordinator.review_cli('kadir-plus', 'fixture'), final)
                else:
                    with self.assertRaises(Blocked):
                        self.coordinator.review_cli('kadir-plus', 'fixture')
            events[0] = {'type': 'error', 'message': text}
            process.communicate.return_value = ('\n'.join(json.dumps(e) for e in events), None)
            with self.assertRaises(Blocked):
                self.coordinator.review_cli('kadir-plus', 'fixture')
        self.assertNotIn(text, json.dumps(self.coordinator.job))

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.projects = ['Poseidon.Persistence.Tests', 'Poseidon.Api.Tests']
        for name in self.projects:
            self.write('tests/' + name + '/' + name + '.csproj',
                       '<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>')
            self.write('tests/' + name + '/Tests.cs', '[Fact] public void Test() {}')
        for name in ['Poseidon.slnx', 'scripts/verify_sources.py', 'scripts/nightly_python_tests.py', 'frontend/package-lock.json',
                     'frontend/scripts/strict-test-reporter.ts',
                     'tests/Poseidon.Browser.Tests/package-lock.json', 'tests/Poseidon.Browser.Tests/playwright.config.ts',
                     'tests/Poseidon.Browser.Tests/node_modules/@playwright/test/cli.js']:
            self.write(name, 'fixture')
        for directory in ['frontend', 'tests/Poseidon.Browser.Tests']:
            self.write(directory + '/package.json', json.dumps({'scripts': {
                k: 'trusted script' for k in ['generate:api', 'lint', 'typecheck', 'test', 'build']}}))
        self.mode = 'passed'
        self.bad_runner = None
        self.commands = []
        self.coordinator = Coordinator(self.root, workers=FakeWorkers(self.root), runner=self.runner)
        self.coordinator.state = {'deadline': time.time() + 1000, 'jobs': {}}
        self.coordinator.job = {'worktree': str(self.root), 'status': 'verifying'}
        self.coordinator.state['jobs']['test'] = self.coordinator.job
        self.coordinator.tick_end = time.time() + 1000
        self.tools = patch.object(self.coordinator, 'tool', side_effect=lambda name: name)
        self.tools.start()
        self.addCleanup(self.tools.stop)

    def write(self, name, value):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(value, encoding='utf-8')

    def runner(self, command, **kwargs):
        self.commands.append(command)
        self.assertEqual(kwargs['cwd'], self.root)
        self.assertLessEqual(kwargs['timeout'], 900)
        output = ''
        if command == ['dotnet', '--version']:
            output = '10.0.403\n'
        elif command == ['node', '--version']:
            output = 'v26.13.0\n'
        elif command[:3] == ['dotnet', 'test', '--solution']:
            if self.mode != 'missing':
                for project in self.projects:
                    count = '0' if self.mode == 'empty' else '12'
                    skipped = '1' if self.mode == 'skipped' else '0'
                    self.write('.runtime/nightly-test-results/' + project + '/results.trx',
                               f'<TestRun xmlns="urn:test"><ResultSummary><Counters executed="{count}" '
                               f'failed="0" notExecuted="{skipped}" /></ResultSummary></TestRun>')
        if 'scripts/nightly_python_tests.py' in command:
            self.report('python')
        elif any(c.startswith('--outputFile=') for c in command):
            self.report('vitest')
        elif '--reporter=list,html,json' in command:
            self.report('playwright')
        return subprocess.CompletedProcess(command, 1 if self.mode == 'failed' and command == ['dotnet', 'tool', 'restore'] else 0, output, 'RAW_SECRET')

    def report(self, runner):
        reports = {
            'python': dict(total=1, passed=1, failed=0, errors=0, skipped=0,
                           expectedFailures=0, unexpectedSuccesses=0),
            'vitest': dict(numTotalTests=1, numPassedTests=1, numFailedTests=0,
                           numPendingTests=0, numTodoTests=0, success=True,
                           testResults=[{'assertionResults': [{'status': 'passed'}]}]),
            'playwright': dict(stats=dict(expected=1, unexpected=0, flaky=0, skipped=0), errors=[],
                               suites=[{'suites': [{'specs': [{'tests': [{'expectedStatus': 'passed',
                                   'status': 'expected', 'results': [{'status': 'passed', 'errors': []}]}]}]}]}]),
        }
        report = reports[runner]
        if runner == self.bad_runner:
            if self.mode == 'missing':
                return
            if self.mode == 'malformed':
                self.write('.runtime/nightly-test-results/' + runner + '.json', 'not json')
                return
            counts = report['stats'] if runner == 'playwright' else report
            if self.mode == 'empty':
                for key in counts:
                    if type(counts[key]) is int:
                        counts[key] = 0
                if runner == 'vitest':
                    report['testResults'] = []
            else:
                counts[{'python': 'skipped', 'vitest': 'numPendingTests', 'playwright': 'skipped'}[runner]] = 1
        self.write('.runtime/nightly-test-results/' + runner + '.json', json.dumps(report))

    def test_exact_fixed_gate_all_projects_no_filters_and_ephemeral_ports(self):
        self.coordinator.gate()
        names = [c['name'] for c in self.coordinator.job['checks']]
        self.assertEqual(names, ['tool_restore', 'locked_restore', 'release_build', 'all_tests',
                         'migration_model', 'python', 'source_lineage', 'frontend_ci', 'browser_ci',
                         'generate:api', 'lint', 'typecheck', 'test', 'build', 'chromium', 'real_api_browser'])
        test = next(c for c in self.commands if c[:3] == ['dotnet', 'test', '--solution'])
        self.assertEqual(test[3], 'Poseidon.slnx')
        self.assertFalse(any('filter' in word for word in test))
        self.assertIn('--minimum-expected-tests', test)
        self.assertIn('--report-xunit-trx', test)
        migration = next(c for c in self.commands if 'has-pending-model-changes' in c)
        self.assertEqual(migration[migration.index('--context') + 1], 'PoseidonDbContext')
        python = next(c for c in self.commands if 'scripts/nightly_python_tests.py' in c)
        self.assertIn('--report', python)
        self.assertTrue(any('--reporter=default' in c and '--reporter=json' in c for c in self.commands))
        self.assertTrue(any('--reporter=./scripts/strict-test-reporter.ts' in c for c in self.commands))
        self.assertTrue(any('--reporter=list,html,json' in c for c in self.commands))
        self.assertEqual(self.coordinator.env['PLAYWRIGHT_JSON_OUTPUT_FILE'],
                         str(self.root / '.runtime/nightly-test-results/playwright.json'))
        self.assertEqual(self.coordinator.env['PLAYWRIGHT_HTML_OPEN'], 'never')
        self.assertNotEqual(self.coordinator.env['POSEIDON_API_PORT'], self.coordinator.env['POSEIDON_UI_PORT'])
        self.assertTrue(all(c['exit_code'] == 0 for c in self.coordinator.job['checks']))

    def test_missing_empty_or_skipped_reports_fail_closed(self):
        for mode in ['missing', 'empty', 'skipped']:
            with self.subTest(mode=mode):
                self.mode = mode
                results = self.root / '.runtime/nightly-test-results'
                if results.exists():
                    for report in results.rglob('*.trx'):
                        report.unlink()
                    for child in results.iterdir():
                        child.rmdir()
                    results.rmdir()
                with self.assertRaises(Blocked):
                    self.coordinator.gate()

    def test_failed_command_records_only_numeric_exit(self):
        self.mode = 'failed'
        with self.assertRaises(Blocked):
            self.coordinator.gate()
        self.assertEqual(self.coordinator.job['checks'][-1], {'name': 'tool_restore', 'exit_code': 1})
        self.assertNotIn('RAW_SECRET', (self.coordinator.runtime / 'report.json').read_text())

    def test_each_runner_zero_exit_missing_empty_skipped_or_malformed_report_blocks(self):
        import shutil
        for runner in ('python', 'vitest', 'playwright'):
            for mode in ('missing', 'empty', 'skipped', 'malformed'):
                with self.subTest(runner=runner, mode=mode):
                    shutil.rmtree(self.root / '.runtime/nightly-test-results', ignore_errors=True)
                    self.bad_runner, self.mode = runner, mode
                    # Keep .NET evidence valid to reach the selected runner.
                    original = self.runner
                    def run(command, **kwargs):
                        if command[:3] == ['dotnet', 'test', '--solution']:
                            self.mode = 'passed'
                            result = original(command, **kwargs)
                            self.mode = mode
                            return result
                        return original(command, **kwargs)
                    self.coordinator.runner = run
                    with self.assertRaisesRegex(Blocked, runner + '_report'):
                        self.coordinator.gate()

    def test_missing_or_empty_project_fails(self):
        path = 'tests/Poseidon.Api.Tests/Poseidon.Api.Tests.csproj'
        (self.root / path).unlink()
        with self.assertRaises(Blocked):
            self.coordinator.gate()
        self.write(path, '<Project><IsTestProject>true</IsTestProject></Project>')
        (self.root / 'tests/Poseidon.Api.Tests/Tests.cs').write_text('// no tests')
        with self.assertRaises(Blocked):
            self.coordinator.gate()


    def test_fresh_readonly_qa_cli_strict_final_message_and_tool_rejection(self):
        self.coordinator.job['calls'] = [{}]
        events = [
            {'type': 'item.completed', 'item': {'type': 'agent_message', 'text':
                json.dumps({'approved': True, 'commit': 'a' * 40, 'issues': []})}},
            {'type': 'turn.completed', 'usage': {'input_tokens': 3, 'raw': 'SECRET'}},
        ]
        process = Mock(returncode=0)
        process.communicate.return_value = ('\n'.join(json.dumps(e) for e in events), None)
        with patch.object(self.coordinator.workers, 'command', return_value=['fake-codex'], create=True), \
                patch('nightly_coordinator.subprocess.Popen', return_value=process) as launch:
            verdict = self.coordinator.review_cli('kadir-plus', 'bounded DATA')
            self.assertTrue(verdict['approved'])
            arguments = launch.call_args.args[0]
            self.assertIn('read-only', arguments)
            self.assertIn('features.shell_tool=false', arguments)
            self.assertIn('features.apply_patch_freeform=false', arguments)
            self.assertIn('features.code_mode_host=false', arguments)
            self.assertIn('features.code_mode=false', arguments)
            self.assertIn('features.code_mode_only=false', arguments)
            self.assertIn('mcp_servers={}', arguments)
            self.assertNotIn('resume', arguments)
            events.insert(0, {'type': 'item.completed', 'item': {'type': 'command_execution'}})
            process.communicate.return_value = ('\n'.join(json.dumps(e) for e in events), None)
            with self.assertRaises(Blocked):
                self.coordinator.review_cli('kadir-plus', 'bounded DATA')
        self.assertEqual(self.coordinator.job['calls'][-1]['usage'], {'input_tokens': 3})

    def test_missing_empty_null_or_unknown_item_cannot_precede_approval(self):
        process = Mock(returncode=0)
        self.coordinator.job['calls'] = [{}]
        approval = {'type': 'item.completed', 'item': {'type': 'agent_message', 'text':
            json.dumps({'approved': True, 'commit': 'a' * 40, 'issues': []})}}
        with patch.object(self.coordinator.workers, 'command', return_value=['fixture'], create=True), \
                patch('nightly_coordinator.subprocess.Popen', return_value=process):
            for event in [{'type': 'item.started'}, {'type': 'item.completed', 'item': {}},
                          {'type': 'item.started', 'item': None},
                          {'type': 'item.completed', 'item': {'type': 'unknown'}}]:
                process.communicate.return_value = ('\n'.join(json.dumps(value) for value in [
                    event, approval, {'type': 'turn.completed', 'usage': {}}]), None)
                with self.subTest(event=event), self.assertRaises(Blocked):
                    self.coordinator.review_cli('esra-pro', 'fixture')

    def test_missing_package_script_fails(self):
        self.write('frontend/package.json', '{"scripts":{"test":"anything"}}')
        with self.assertRaises(Blocked):
            self.coordinator.gate()
        self.assertFalse(self.commands)


class StructuredSummaryTests(unittest.TestCase):
    def test_actual_unittest_outcomes_cannot_hide_skips_or_expected_failures(self):
        class FakeTests(unittest.TestCase):
            def test_pass(self):
                pass

            @unittest.skip('synthetic skip')
            def test_skip(self):
                pass

            @unittest.expectedFailure
            def test_expected_failure(self):
                self.fail('synthetic failure')

        result = unittest.TextTestRunner(stream=io.StringIO()).run(
            unittest.defaultTestLoader.loadTestsFromTestCase(FakeTests))
        counts = summary(result)
        self.assertTrue(result.wasSuccessful())  # unittest alone accepts these outcomes.
        self.assertEqual(counts, dict(total=3, passed=1, failed=0, errors=0, skipped=1,
                                     expectedFailures=1, unexpectedSuccesses=0))
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / 'python.json'
            report.write_text(json.dumps(counts))
            with self.assertRaises(Blocked):
                test_summary(report, 'python')

    def test_incomplete_wrong_types_and_other_nonpassing_outcomes_rejected(self):
        fixtures = {
            'python': dict(total=1, passed=1, failed=0, errors=0, skipped=0,
                           expectedFailures=0, unexpectedSuccesses=0),
            'vitest': dict(numTotalTests=1, numPassedTests=1, numFailedTests=0,
                           numPendingTests=0, numTodoTests=0, success=True,
                           testResults=[{'assertionResults': [{'status': 'passed'}]}]),
            'playwright': dict(stats=dict(expected=1, unexpected=0, flaky=0, skipped=0), errors=[],
                               suites=[{'specs': [{'tests': [{'expectedStatus': 'passed', 'status': 'expected',
                                                            'results': [{'status': 'passed', 'errors': []}]}]}]}]),
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'report.json'
            for runner, valid in fixtures.items():
                path.write_text(json.dumps(valid))
                test_summary(path, runner)
                counts = valid['stats'] if runner == 'playwright' else valid
                for key, value in counts.items():
                    if type(value) is not int:
                        continue
                    for replacement in (None, True, '1', -1, 2):
                        invalid = json.loads(json.dumps(valid))
                        target = invalid['stats'] if runner == 'playwright' else invalid
                        target[key] = replacement
                        path.write_text(json.dumps(invalid))
                        with self.subTest(runner=runner, key=key, replacement=replacement):
                            with self.assertRaises(Blocked):
                                test_summary(path, runner)
                    invalid = json.loads(json.dumps(valid))
                    del (invalid['stats'] if runner == 'playwright' else invalid)[key]
                    path.write_text(json.dumps(invalid))
                    with self.assertRaises(Blocked):
                        test_summary(path, runner)
            invalid = fixtures['vitest']
            invalid['testResults'][0]['assertionResults'][0]['status'] = 'todo'
            path.write_text(json.dumps(invalid))
            with self.assertRaises(Blocked):
                test_summary(path, 'vitest')


    def test_playwright_expected_failures_and_incomplete_nested_results_block(self):
        valid = dict(stats=dict(expected=1, unexpected=0, flaky=0, skipped=0), errors=[],
                     suites=[{'suites': [{'specs': [{'tests': [{'expectedStatus': 'passed', 'status': 'expected',
                                                              'results': [{'status': 'passed', 'errors': []}]}]}]}]}])
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'report.json'
            path.write_text(json.dumps(valid))
            test_summary(path, 'playwright')
            for mode in ('expected-failure', 'actual-failure', 'retry-failure', 'empty-results', 'missing-suites', 'count-mismatch'):
                invalid = json.loads(json.dumps(valid))
                test = invalid['suites'][0]['suites'][0]['specs'][0]['tests'][0]
                if mode == 'expected-failure':
                    test['expectedStatus'] = 'failed'
                    test['results'][0]['status'] = 'failed'
                elif mode == 'actual-failure':
                    test['results'][0]['status'] = 'failed'
                elif mode == 'retry-failure':
                    test['results'].insert(0, {'status': 'failed'})
                elif mode == 'empty-results':
                    test['results'] = []
                elif mode == 'missing-suites':
                    del invalid['suites']
                else:
                    invalid['stats']['expected'] = 2
                path.write_text(json.dumps(invalid))
                with self.subTest(mode=mode), self.assertRaises(Blocked):
                    test_summary(path, 'playwright')


class QueueTests(unittest.TestCase):
    def test_subscription_route_is_explicit_without_login_or_model_call(self):
        with patch.object(Workers, 'command', return_value=['fake-codex']):
            command = SubscriptionWorkers.__new__(SubscriptionWorkers).command()
        self.assertIn('forced_login_method="chatgpt"', command)
        self.assertIn('model_provider="openai"', command)
        self.assertIn('model_reasoning_effort="medium"', command)
        self.assertIn('features.multi_agent=false', command)

    def test_additional_account_windows_are_not_dropped(self):
        profile = {'id': 'esra-pro', 'expected_plan': 'pro'}
        windows = {'rateLimitsByLimitId': {
            'codex': {'primary': {'usedPercent': 10}},
            'additional': {'secondary': {'usedPercent': 90}},
        }}
        summary = summarize_account(profile, {'type': 'chatgpt', 'planType': 'pro'}, windows)
        self.assertEqual([w['usedPercent'] for w in summary['windows']], [10, 90])
        from nightly_coordinator import eligible
        self.assertFalse(eligible(summary))

    def test_untrusted_queue_shapes_paths_ids_and_dependencies_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'task.md').write_text('Trusted task')
            valid = {'id': 'OPS-1', 'task': 'task.md', 'source': 'source:1',
                     'acceptance': ['test'], 'dependencies': [], 'scope': ['scripts/*.py']}
            invalid = [[valid, valid], [dict(valid, task='../task.md')],
                       [dict(valid, command='arbitrary shell')], [dict(valid, scope=['**/*'])],
                       [dict(valid, dependencies=['UNKNOWN'])], [dict(valid, acceptance=[])]]
            for items in invalid:
                (root / 'queue.json').write_text(json.dumps(items))
                with self.assertRaises(Blocked):
                    queue_items(root, 'queue.json')
            (root / 'queue.json').write_text(json.dumps([valid]))
            self.assertEqual(queue_items(root, 'queue.json'), [valid])


if __name__ == '__main__':
    unittest.main()
