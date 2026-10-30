"""Fixed verification command contract and fail-closed evidence, with no tools/network."""
import json
from pathlib import Path
import subprocess
import tempfile
import time
import unittest
from unittest.mock import Mock, patch

from nightly_coordinator import Blocked, Coordinator, SubscriptionWorkers, queue_items
from codex_accounts import Workers, summarize_account
from test_nightly_coordinator import FakeWorkers


class FixedGateTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.projects = ['Poseidon.Persistence.Tests', 'Poseidon.Api.Tests']
        for name in self.projects:
            self.write('tests/' + name + '/' + name + '.csproj',
                       '<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>')
            self.write('tests/' + name + '/Tests.cs', '[Fact] public void Test() {}')
        for name in ['Poseidon.slnx', 'scripts/verify_sources.py', 'frontend/package-lock.json',
                     'tests/Poseidon.Browser.Tests/package-lock.json', 'tests/Poseidon.Browser.Tests/playwright.config.ts',
                     'tests/Poseidon.Browser.Tests/node_modules/@playwright/test/cli.js']:
            self.write(name, 'fixture')
        for directory in ['frontend', 'tests/Poseidon.Browser.Tests']:
            self.write(directory + '/package.json', json.dumps({'scripts': {
                k: 'trusted script' for k in ['generate:api', 'lint', 'typecheck', 'test', 'build']}}))
        self.mode = 'passed'
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
        return subprocess.CompletedProcess(command, 1 if self.mode == 'failed' and command == ['dotnet', 'tool', 'restore'] else 0, output, 'RAW_SECRET')

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
            self.assertIn('mcp_servers={}', arguments)
            self.assertNotIn('resume', arguments)
            events.insert(0, {'type': 'item.completed', 'item': {'type': 'command_execution'}})
            process.communicate.return_value = ('\n'.join(json.dumps(e) for e in events), None)
            with self.assertRaises(Blocked):
                self.coordinator.review_cli('kadir-plus', 'bounded DATA')
        self.assertEqual(self.coordinator.job['calls'][-1]['usage'], {'input_tokens': 3})

    def test_missing_package_script_fails(self):
        self.write('frontend/package.json', '{"scripts":{"test":"anything"}}')
        with self.assertRaises(Blocked):
            self.coordinator.gate()
        self.assertFalse(self.commands)


class QueueTests(unittest.TestCase):
    def test_subscription_route_is_explicit_without_login_or_model_call(self):
        with patch.object(Workers, 'command', return_value=['fake-codex']):
            command = SubscriptionWorkers.__new__(SubscriptionWorkers).command()
        self.assertIn('forced_login_method="chatgpt"', command)
        self.assertIn('model_provider="openai"', command)

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
