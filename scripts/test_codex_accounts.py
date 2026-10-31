from concurrent.futures import ThreadPoolExecutor
from contextlib import contextmanager
import json
import io
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import Mock, patch

from codex_accounts import BusyError, MetadataError, ROOT, WorkerStillRunningError, Workers, evaluate_run, exclusive, quota_error, summarize_account, write_json


def limits(used=10):
    return {'rateLimits': {'primary': {'usedPercent': used, 'windowDurationMins': 300, 'resetsAt': 2000000000}}}


class ProtocolTests(unittest.TestCase):
    def test_internal_metadata_error_refreshes_once_same_locked_profile_then_reads_limits(self):
        workers = Workers()
        for refresh_error in (None, -32603, 401):
            messages = [{'id': 1, 'result': {}}, {'id': 2, 'error': {'code': -32603, 'message': 'SECRET'}}]
            messages.append({'id': 4, 'error': {'code': refresh_error, 'message': 'SECRET'}} if refresh_error else
                {'id': 4, 'result': {'account': {'type': 'chatgpt', 'planType': 'prolite'}}})
            if not refresh_error:
                messages.append({'id': 3, 'result': limits(18)})
            process = Mock()
            process.stdin = io.StringIO()
            process.stdout = io.StringIO('\n'.join(json.dumps(v) for v in messages) + '\n')
            with tempfile.TemporaryDirectory() as folder, patch.object(workers, 'home', return_value=Path(folder)), \
                    patch.object(workers, 'initialize'), patch.object(workers, 'command', return_value=['codex']), \
                    patch.object(workers, 'finish_metadata'), patch('codex_accounts.subprocess.Popen', return_value=process):
                with exclusive(workers.home('esra-pro') / 'worker.lock'):
                    if refresh_error:
                        with self.assertRaises(MetadataError) as raised:
                            workers.account('esra-pro')
                        self.assertNotIn('SECRET', str(raised.exception))
                    else:
                        self.assertTrue(workers.account('esra-pro')['ready'])
                    self.assertTrue((workers.home('esra-pro') / 'worker.lock').exists())
            requests = [json.loads(line) for line in process.stdin.getvalue().splitlines()]
            reads = [r for r in requests if r['method'] == 'account/read']
            self.assertEqual([r['params']['refreshToken'] for r in reads], [False, True])

    def test_auth_error_never_attempts_refresh(self):
        workers = Workers()
        process = Mock(stdin=io.StringIO(), stdout=io.StringIO('\n'.join(json.dumps(v) for v in [
            {'id': 1, 'result': {}}, {'id': 2, 'error': {'code': 401, 'message': 'SECRET'}}]) + '\n'))
        with patch.object(workers, 'initialize'), patch.object(workers, 'command', return_value=['codex']), \
                patch.object(workers, 'finish_metadata'), patch('codex_accounts.subprocess.Popen', return_value=process):
            with self.assertRaises(MetadataError):
                workers.account('esra-pro')
        self.assertNotIn('"refreshToken": true', process.stdin.getvalue())

    def test_native_launcher_and_node_fallback_preserve_auth_arguments(self):
        workers = Workers()
        with patch('codex_accounts.os.name', 'nt'), patch.object(Path, 'is_file', return_value=True):
            command = workers.command()
            self.assertTrue(command[0].endswith('codex.exe'))
            self.assertEqual(command[1:], ['--no-daemon', '-c', 'cli_auth_credentials_store="keyring"'])
        for platform in ['nt', 'posix']:
            with patch('codex_accounts.os.name', platform), patch.object(Path, 'is_file', autospec=True, side_effect=lambda path: path.name == 'codex.js'), patch('codex_accounts.shutil.which', return_value='node'):
                command = workers.command()
                self.assertEqual(command[0], 'node')
                self.assertTrue(command[1].endswith('codex.js'))
                self.assertEqual(command[2:], ['--no-daemon', '-c', 'cli_auth_credentials_store="keyring"'])

    def test_metadata_error_exposes_only_code_and_fixed_categories(self):
        workers = Workers()
        raw = 'credential token=FAKE_SECRET private@example.invalid C:/private/account.json'
        process = Mock()
        process.stdin = io.StringIO()
        process.stdout = io.StringIO('\n'.join(json.dumps(value) for value in [
            {'id': 1, 'result': {}}, {'id': 2, 'error': {'code': -32603, 'message': raw}},
        ]) + '\n')
        with patch.object(workers, 'initialize'), patch.object(workers, 'command', return_value=['codex']), patch.object(workers, 'finish_metadata'), patch('codex_accounts.subprocess.Popen', return_value=process):
            with self.assertRaises(RuntimeError) as raised:
                workers.account('esra-pro')
        self.assertEqual(str(raised.exception), 'Account metadata unavailable (code=-32603, category=credential,token,account,json); no worker dispatched.')
        self.assertNotIn('FAKE_SECRET', str(raised.exception))
        self.assertNotIn('private@example.invalid', str(raised.exception))

    def test_structured_quota_failure_is_classified(self):
        event = {'type': 'turn.failed', 'error': {'code': 'usage_limit_reached'}}
        self.assertTrue(quota_error(event))
        self.assertEqual(evaluate_run(1, json.dumps(event)), ('quota', None))

    def test_quoted_quota_in_tool_output_cannot_trigger_handoff(self):
        event = {'type': 'item.completed', 'item': {'text': 'usage_limit_reached'}}
        self.assertFalse(quota_error(event))
        self.assertEqual(evaluate_run(1, json.dumps(event)), ('failed', None))

    def test_recovered_error_followed_by_success_is_produced(self):
        output = '\n'.join(json.dumps(event) for event in [
            {'type': 'error', 'message': 'temporary transport error'},
            {'type': 'turn.completed', 'usage': {'input_tokens': 12, 'secret': 'never persist'}},
        ])
        self.assertEqual(evaluate_run(0, output), ('produced', {'input_tokens': 12}))

    def test_exit_zero_without_completed_turn_is_not_success(self):
        self.assertEqual(evaluate_run(0, 'not JSON'), ('failed', None))

    def test_historical_quota_cannot_override_terminal_nonquota_failure(self):
        for recovered in [False, True]:
            events = [{'type': 'error', 'code': 'usage_limit_reached'}]
            if recovered:
                events.append({'type': 'turn.completed', 'usage': {}})
            events.append({'type': 'turn.failed', 'error': {'code': 'authentication_error'}})
            output = '\n'.join(json.dumps(event) for event in events)
            self.assertEqual(evaluate_run(1, output), ('failed', None))

    def test_recovered_quota_then_nonzero_exit_is_not_handoff(self):
        output = '\n'.join(json.dumps(event) for event in [
            {'type': 'error', 'code': 'usage_limit_reached'},
            {'type': 'turn.completed', 'usage': {}},
        ])
        self.assertEqual(evaluate_run(1, output), ('failed', None))

    def test_account_plan_must_match_and_private_identity_is_not_returned(self):
        profile = {'id': 'esra-pro', 'expected_plan': 'pro'}
        result = summarize_account(profile, {'type': 'chatgpt', 'planType': 'plus', 'email': 'private@example.invalid'}, limits())
        self.assertFalse(result['ready'])
        self.assertNotIn('private@example.invalid', json.dumps(result))

    def test_exhausted_or_unknown_limits_do_not_dispatch(self):
        profile = {'id': 'esra-pro', 'expected_plan': 'pro'}
        account = {'type': 'chatgpt', 'planType': 'pro'}
        self.assertFalse(summarize_account(profile, account, limits(100))['ready'])
        self.assertFalse(summarize_account(profile, account, {})['ready'])
        self.assertTrue(summarize_account(profile, account, limits(10))['ready'])

    def test_explicit_pro_variants_do_not_admit_plus_or_other_plans(self):
        profile = {'id': 'esra-pro', 'expected_plan': 'pro', 'accepted_plans': ['pro', 'prolite', 'promax']}
        for plan in ['pro', 'prolite', 'promax']:
            self.assertTrue(summarize_account(profile, {'type': 'chatgpt', 'planType': plan}, limits())['ready'])
        for plan in ['plus', 'free', 'unknown']:
            self.assertFalse(summarize_account(profile, {'type': 'chatgpt', 'planType': plan}, limits())['ready'])

    def test_exclusive_lock_rejects_concurrent_holder_and_releases(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'worker.lock'
            with exclusive(path):
                with self.assertRaises(BusyError):
                    with exclusive(path):
                        self.fail('Duplicate holder admitted')
            self.assertFalse(path.exists())

    def test_stop_os_failure_is_uncertain_instead_of_releasing_worker(self):
        process = Mock(pid=12345)
        target = 'codex_accounts.subprocess.run' if os.name == 'nt' else 'codex_accounts.os.killpg'
        with patch(target, side_effect=OSError('cannot terminate')):
            with self.assertRaises(WorkerStillRunningError):
                Workers.stop(process)

    def test_windows_taskkill_timeout_is_bounded_and_uncertain(self):
        process = Mock(pid=12345)
        with patch('codex_accounts.os.name', 'nt'), patch('codex_accounts.subprocess.run', side_effect=subprocess.TimeoutExpired('taskkill', 10)) as kill:
            with self.assertRaises(WorkerStillRunningError):
                Workers.stop(process)
        self.assertEqual(kill.call_args.kwargs['timeout'], 10)

    def test_metadata_cleanup_interrupt_or_close_failure_stops_child(self):
        for failure in ['close', 'wait']:
            process = Mock()
            if failure == 'close':
                process.stdin.close.side_effect = OSError('pipe failure')
                expected = OSError
            else:
                process.wait.side_effect = KeyboardInterrupt()
                expected = KeyboardInterrupt
            with patch.object(Workers, 'stop') as stop:
                with self.assertRaises(expected):
                    Workers.finish_metadata(process)
            stop.assert_called_once_with(process)


class FakeWorkers(Workers):
    def __init__(self, root):
        super().__init__(root, root / 'private-homes')
        self.results = {'esra-pro': 'produced', 'kadir-plus': 'produced'}
        self.calls = []
        self.initialize()

    def account(self, profile):
        return {'ready': True, 'exhausted': False}

    def execute(self, profile, worktree, prompt):
        self.calls.append(profile)
        path = worktree / 'partial.txt'
        if self.results[profile] == 'quota':
            path.write_text('preserved partial work', encoding='utf-8')
        elif path.exists():
            if path.read_text(encoding='utf-8') != 'preserved partial work':
                raise AssertionError('Partial work was lost')
        return self.results[profile], {'input_tokens': 1}


class DispatchTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / 'automation').mkdir()
        (self.root / 'automation/codex-accounts.json').write_bytes((ROOT / 'automation/codex-accounts.json').read_bytes())
        subprocess.run(['git', 'init', '-q'], cwd=self.root, check=True)
        subprocess.run(['git', '-c', 'user.name=WorkerTests', '-c', 'user.email=tests@example.invalid',
                        'commit', '--allow-empty', '-qm', 'Initial test commit'], cwd=self.root, check=True)
        self.task = self.root / 'task.md'
        self.task.write_text('Implement a small tested behavior.', encoding='utf-8')
        self.workers = FakeWorkers(self.root)

    def test_quota_handoff_reuses_worktree_and_preserves_partial_changes(self):
        self.workers.results['esra-pro'] = 'quota'
        state = self.workers.run(self.task)
        self.assertEqual(self.workers.calls, ['esra-pro', 'kadir-plus'])
        self.assertEqual(state['status'], 'needs_review')
        self.assertTrue((Path(state['worktree']) / 'partial.txt').exists())
        self.assertEqual(state['attempts'][0]['result'], 'quota')

    def test_watch_single_account_never_falls_back_on_quota(self):
        from codex_watch import eligible
        from test_codex_watch import metadata
        self.workers.results['esra-pro'] = 'quota'
        with patch.object(self.workers, 'account', return_value=metadata()):
            state = self.workers.run(self.task, 'esra-pro', single_account=True, eligibility=eligible)
        self.assertEqual(self.workers.calls, ['esra-pro'])
        self.assertEqual(state['status'], 'waiting_for_quota')

    def test_watch_rechecks_reserve_under_lock_before_execute(self):
        from codex_watch import eligible
        from test_codex_watch import metadata
        def account(profile):
            self.assertTrue((self.workers.home(profile) / 'worker.lock').exists())
            return metadata(85)
        with patch.object(self.workers, 'account', side_effect=account):
            state = self.workers.run(self.task, 'esra-pro', single_account=True, eligibility=eligible)
        self.assertEqual(self.workers.calls, [])
        self.assertEqual(state['status'], 'waiting_for_account')

    def test_non_quota_failure_and_timeout_never_switch_accounts(self):
        for failure in ['failed', 'timeout']:
            self.workers.calls.clear()
            self.workers.results['esra-pro'] = failure
            state = self.workers.run(self.task)
            self.assertEqual(state['status'], 'blocked')
            self.assertEqual(self.workers.calls, ['esra-pro'])

    def test_metadata_failure_preserves_safe_diagnostic_without_handoff(self):
        safe = 'Account metadata unavailable (code=-32603, category=credential); no worker dispatched.'
        with patch.object(self.workers, 'account', side_effect=MetadataError(safe)):
            state = self.workers.run(self.task)
        self.assertEqual(state['status'], 'blocked')
        self.assertEqual(state['attempts'], [{'profile': 'esra-pro', 'result': 'worker-error', 'diagnostic': safe}])
        self.assertEqual(self.workers.calls, [])

    def test_execute_requests_authorized_full_access_in_own_worktree(self):
        process = Mock(returncode=0)
        process.communicate.return_value = (json.dumps({'type': 'turn.completed', 'usage': {}}), None)
        with patch.object(self.workers, 'command', return_value=['codex']), patch('codex_accounts.subprocess.Popen', return_value=process) as launch:
            self.assertEqual(Workers.execute(self.workers, 'esra-pro', self.root, 'bounded task'), ('produced', {}))
        arguments = launch.call_args.args[0]
        self.assertEqual(arguments[arguments.index('-s') + 1], 'danger-full-access')
        self.assertEqual(arguments[arguments.index('-C') + 1], str(self.root))
        self.assertEqual(arguments[arguments.index('-a') + 1], 'never')

    def test_both_quotas_wait_and_resume_same_job(self):
        self.workers.results = {'esra-pro': 'quota', 'kadir-plus': 'quota'}
        state = self.workers.run(self.task)
        self.assertEqual(state['status'], 'waiting_for_quota')
        self.workers.results['esra-pro'] = 'produced'
        resumed = self.workers.resume(state['job_id'])
        self.assertEqual(resumed['worktree'], state['worktree'])
        self.assertEqual(resumed['status'], 'needs_review')
        with self.assertRaises(ValueError):
            self.workers.resume(state['job_id'])

    def test_parallel_jobs_have_different_worktrees_and_preferred_accounts(self):
        with ThreadPoolExecutor(max_workers=2) as executor:
            pro = executor.submit(self.workers.run, self.task, 'esra-pro')
            plus = executor.submit(self.workers.run, self.task, 'kadir-plus')
            jobs = [pro.result(), plus.result()]
        self.assertNotEqual(jobs[0]['worktree'], jobs[1]['worktree'])
        self.assertEqual({job['status'] for job in jobs}, {'needs_review'})
        self.assertEqual(set(self.workers.calls), {'esra-pro', 'kadir-plus'})

    def test_busy_primary_account_can_use_idle_secondary(self):
        with exclusive(self.workers.home('esra-pro') / 'worker.lock'):
            state = self.workers.run(self.task)
        self.assertEqual(self.workers.calls, ['kadir-plus'])
        self.assertEqual(state['status'], 'needs_review')

    def test_resume_reloads_state_after_job_lock_is_acquired(self):
        self.workers.results = {'esra-pro': 'quota', 'kadir-plus': 'quota'}
        state = self.workers.run(self.task)
        directory = self.workers.runtime / 'jobs' / state['job_id']
        self.workers.calls.clear()

        @contextmanager
        def completed_before_lock(path):
            if path == directory / 'job.lock':
                current = json.loads((directory / 'state.json').read_text(encoding='utf-8'))
                current['status'] = 'needs_review'
                write_json(directory / 'state.json', current)
            with exclusive(path):
                yield

        with patch('codex_accounts.exclusive', completed_before_lock):
            with self.assertRaises(ValueError):
                self.workers.resume(state['job_id'])
        self.assertEqual(self.workers.calls, [])

    def test_execute_interrupt_stops_child_tree_before_propagating(self):
        process = Mock()
        process.communicate.side_effect = KeyboardInterrupt()
        with patch.object(self.workers, 'command', return_value=['fake-codex']), \
                patch('codex_accounts.subprocess.Popen', return_value=process), \
                patch.object(self.workers, 'stop') as stop:
            with self.assertRaises(KeyboardInterrupt):
                Workers.execute(self.workers, 'esra-pro', self.root, 'task')
        stop.assert_called_once_with(process)

    def test_uncertain_worker_keeps_profile_and_job_locks_for_reconciliation(self):
        with patch.object(self.workers, 'execute', side_effect=WorkerStillRunningError('uncertain')):
            with self.assertRaises(WorkerStillRunningError):
                self.workers.run(self.task)
        jobs = list((self.workers.runtime / 'jobs').iterdir())
        self.assertEqual(len(jobs), 1)
        state = json.loads((jobs[0] / 'state.json').read_text(encoding='utf-8'))
        self.assertEqual(state['status'], 'reconcile_required')
        self.assertTrue((jobs[0] / 'job.lock').exists())
        self.assertTrue((self.workers.home('esra-pro') / 'worker.lock').exists())

    def test_cli_environment_removes_api_keys_and_keeps_oauth_homes_separate(self):
        with patch.dict(os.environ, {'CODEX_API_KEY': 'never-forward', 'OPENAI_API_KEY': 'never-forward', 'CODEX_ACCESS_TOKEN': 'never-forward'}):
            pro = self.workers.environment('esra-pro')
            plus = self.workers.environment('kadir-plus')
        self.assertNotEqual(pro['CODEX_HOME'], plus['CODEX_HOME'])
        self.assertNotIn('CODEX_API_KEY', pro)
        self.assertNotIn('OPENAI_API_KEY', pro)
        self.assertNotIn('CODEX_ACCESS_TOKEN', pro)


if __name__ == '__main__':
    unittest.main()
