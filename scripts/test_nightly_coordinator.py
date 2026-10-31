"""Network/model-free coordinator safety and lifecycle tests, on every platform."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import Mock, patch

from codex_accounts import BusyError, Workers, exclusive, write_json
from nightly_coordinator import (Blocked, Coordinator, ci_result, eligible, inside,
                                 numeric_usage, ports, qa_approved, queue_items)

SHA = 'a' * 40


def metadata(used=10):
    return dict(ready=True, authenticated=True, plan_matches=True, limits_known=True,
                windows=[{'usedPercent': used}])


def pr(commit=SHA, checks=None):
    return dict(number=1, url='https://github.com/example/repo/pull/1', headRefOid=commit,
                headRefName='nightly/ops-test-01', baseRefName='main', state='OPEN',
                statusCheckRollup=checks if checks is not None else [
                    dict(name=name, status='COMPLETED', conclusion='SUCCESS')
                    for name in ('persistence-and-security', 'frontend-and-browser',
                                 'windows-automation', 'nightly-installer-construction', 'extra-check')])


class FakeWorkers:
    def __init__(self, root):
        self.root, self.config = root, {}
        self.calls = []
        self.status = {'esra-pro': metadata(), 'kadir-plus': metadata()}
        self.change = True

    def home(self, profile):
        return self.root / '.runtime/homes' / profile

    def environment(self, profile):
        return os.environ.copy()

    def account(self, profile):
        assert (self.home(profile) / 'worker.lock').exists()
        return self.status[profile]

    def execute(self, profile, work, prompt):
        assert (self.home(profile) / 'worker.lock').exists()
        self.calls.append((profile, work, prompt))
        if self.change:
            (work / 'example.txt').write_text('implemented\n', encoding='utf-8')
        return 'produced', {'input_tokens': 7, 'raw': 'SECRET'}


class LifecycleTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.git('init', '-q')
        self.git('config', 'user.name', 'NightlyTests')
        self.git('config', 'user.email', 'tests@example.invalid')
        (self.root / 'automation').mkdir()
        (self.root / 'task.md').write_text('Small task. Trace OPS-TEST-01.', encoding='utf-8')
        (self.root / '.gitignore').write_text('.runtime/\n', encoding='utf-8')
        self.item = dict(id='OPS-TEST-01', task='task.md', source='user:OPS-COORDINATOR-01',
                         acceptance=['Changes are tested'], dependencies=[], scope=['example.txt'])
        self.queue([self.item])
        self.git('add', '.')
        self.git('commit', '-qm', 'fixture')
        self.git('update-ref', 'refs/remotes/origin/main', 'HEAD')
        self.workers = FakeWorkers(self.root)
        self.commands = []
        self.coordinator = Coordinator(self.root, enabled=True, workers=self.workers, runner=self.run_command)

    def git(self, *args):
        return subprocess.run(['git', *args], cwd=self.root, check=True, capture_output=True, text=True).stdout.strip()

    def queue(self, items):
        (self.root / 'automation/nightly-queue.json').write_text(json.dumps(items), encoding='utf-8')

    def run_command(self, args, **kwargs):
        self.commands.append(args)
        self.assertLessEqual(kwargs['timeout'], 900)
        if args[:3] == ['git', 'fetch', 'origin']:
            return subprocess.CompletedProcess(args, 0, '', '')
        if args[0] != 'git' or args[1] == 'push':
            self.fail('Unexpected external command: ' + str(args))
        return subprocess.run(args, **kwargs)

    def tick(self):
        return self.coordinator.once()['jobs'].get(self.item['id'])

    def prepared(self):
        self.assertEqual(self.tick()['status'], 'verifying')
        with patch.object(self.coordinator, 'gate', side_effect=lambda: self.coordinator.job.update(
                checks=[{'name': 'fixed_gate', 'exit_code': 0}])), patch.object(self.coordinator, 'npm', return_value=['fake-npm']), \
                patch.object(self.coordinator, 'command', wraps=self.coordinator.command) as command:
            original = self.coordinator.runner
            def runner(args, **kwargs):
                if args == ['fake-npm']:
                    return subprocess.CompletedProcess(args, 0, '', '')
                return original(args, **kwargs)
            with patch.object(self.coordinator, 'runner', runner):
                self.assertEqual(self.tick()['status'], 'awaiting_qa')
        return self.coordinator.job

    def reviewed(self):
        job = self.prepared()
        with patch.object(self.coordinator, 'review_cli', return_value={'approved': True, 'commit': job['commit'], 'issues': []}):
            self.assertEqual(self.tick()['status'], 'awaiting_ci')
        return self.coordinator.job

    def test_disabled_default_and_publish_default_have_no_side_effects(self):
        self.coordinator.enabled = False
        self.assertIsNone(self.tick())
        self.assertFalse(self.commands)
        self.coordinator.enabled = True
        self.reviewed()
        before = list(self.commands)
        self.tick()
        self.assertEqual([c for c in self.commands[len(before):] if c[:2] != ['git', 'ls-files']], [])
        self.assertEqual(len(self.workers.calls), 1)

    def test_separate_phases_commit_and_numeric_usage(self):
        job = self.reviewed()
        self.assertEqual(job['calls'][0]['profile'], 'esra-pro')
        self.assertEqual(job['calls'][1]['profile'], 'kadir-plus')
        self.assertEqual(job['calls'][0]['usage'], {'input_tokens': 7})
        self.assertNotEqual(job['commit'], job['base'])
        self.assertEqual(self.git('rev-parse', 'HEAD'), job['base'])
        self.assertNotIn('SECRET', (self.coordinator.runtime / 'report.json').read_text())
        self.assertTrue((self.coordinator.runtime / 'report.md').is_file())

    def test_zero_changes_blocks_despite_model_success(self):
        self.workers.change = False
        self.assertEqual(self.tick()['reason'], 'zero_changes')
        self.tick()
        self.assertEqual(len(self.workers.calls), 1)

    def test_busy_and_quota_use_only_fresh_eligible_profile(self):
        self.workers.status['kadir-plus'] = metadata(85)
        with exclusive(self.workers.home('esra-pro') / 'worker.lock'):
            self.assertEqual(self.tick()['status'], 'waiting_for_account')
        self.assertFalse(self.workers.calls)
        self.workers.status['esra-pro'] = metadata(90)
        self.workers.status['kadir-plus'] = metadata(10)
        self.assertEqual(self.tick()['status'], 'verifying')
        self.assertEqual(self.workers.calls[0][0], 'kadir-plus')

    def test_unrelated_old_jobs_do_not_block_new_queue(self):
        old = self.root / '.runtime/account-workers/jobs/old'
        old.mkdir(parents=True)
        (old / 'state.json').write_text('{"status":"blocked"}')
        self.assertEqual(self.tick()['status'], 'verifying')

    def test_persisted_lease_precedes_dispatch(self):
        original = self.workers.execute
        def execute(*args):
            state = json.loads((self.coordinator.runtime / 'state.json').read_text())
            job = state['jobs'][self.item['id']]
            self.assertEqual(job['status'], 'developing')
            self.assertEqual(job['inflight'], 'model')
            self.assertIsInstance(job['lease']['pid'], int)
            return original(*args)
        with patch.object(self.workers, 'execute', execute):
            self.tick()

    def test_interrupted_development_verification_or_qa_never_replays(self):
        self.tick()
        state = self.coordinator.state
        for stage, action in [('developing', 'model'), ('verifying', 'fixed_gate'), ('awaiting_qa', 'qa_model')]:
            job = state['jobs'][self.item['id']]
            job.update(status=stage, inflight=action)
            write_json(self.coordinator.runtime / 'state.json', state)
            with patch.object(self.workers, 'execute') as execute:
                self.assertEqual(self.tick()['reason'], 'reconciliation_needed_interrupted_phase')
                execute.assert_not_called()

    def test_failed_gate_preserves_partial_files_and_stays_blocked(self):
        job = self.tick()
        work = Path(job['worktree'])
        with patch.object(self.coordinator, 'gate', side_effect=Blocked('gate_failed')):
            self.assertEqual(self.tick()['status'], 'blocked')
        self.assertEqual((work / 'example.txt').read_text(), 'implemented\n')
        self.tick()
        self.assertEqual(len(self.workers.calls), 1)

    def test_model_cap_and_deadline_block_without_extra_call(self):
        self.prepared()
        self.coordinator.max_calls = 1
        self.assertEqual(self.tick()['reason'], 'model_call_cap')
        self.assertEqual(len(self.workers.calls), 1)
        state = self.coordinator.state
        job = state['jobs'][self.item['id']]
        job.update(status='awaiting_qa', inflight=None)
        state['deadline'] = 0
        write_json(self.coordinator.runtime / 'state.json', state)
        self.assertEqual(self.tick()['reason'], 'deadline_reached')

    def test_max_jobs_dependencies_and_at_most_one_item(self):
        second = dict(self.item, id='OPS-TEST-02')
        self.queue([self.item, second])
        self.tick()
        self.assertEqual(len(self.coordinator.state['jobs']), 1)
        state = self.coordinator.state
        state['jobs'][self.item['id']].update(status='blocked')
        write_json(self.coordinator.runtime / 'state.json', state)
        self.tick()
        self.assertEqual(len(self.coordinator.state['jobs']), 1)

    def test_wrong_commit_or_unstructured_qa_blocks(self):
        self.prepared()
        with patch.object(self.coordinator, 'review_cli', return_value={'approved': True, 'commit': SHA, 'issues': []}):
            self.assertEqual(self.tick()['reason'], 'independent_qa_rejected_or_invalid')

    def test_qa_worktree_mutation_blocks(self):
        job = self.prepared()
        def review(*args):
            (Path(job['worktree']) / 'example.txt').write_text('tampered')
            return {'approved': True, 'commit': job['commit'], 'issues': []}
        with patch.object(self.coordinator, 'review_cli', side_effect=review):
            self.assertEqual(self.tick()['reason'], 'reviewed_head_or_worktree_changed')

    def test_scope_sensitive_binary_symlink_and_large_patch_rejected(self):
        job = self.tick()
        work = Path(job['worktree'])
        self.coordinator.job = job
        for name in ['.env', 'auth.json', 'outside.txt']:
            (work / name).write_text('private')
            with self.assertRaises(Blocked):
                self.coordinator.patch(self.item)
            (work / name).unlink()
        for content in ['x' * 80001, 'a\0b']:
            (work / 'example.txt').write_text(content)
            with self.assertRaises(Blocked):
                self.coordinator.patch(self.item)
        with patch.object(Path, 'is_symlink', return_value=True):
            with self.assertRaises(Blocked):
                self.coordinator.patch(self.item)

    def setup_publish(self):
        self.reviewed()
        self.coordinator.publish = True
        self.coordinator.job['pr'] = 1
        self.coordinator.job['pr_url'] = pr()['url']
        self.coordinator.save()
        return pr(self.coordinator.job['commit'])

    def test_ci_pending_missing_and_failed_or_skipped(self):
        data = self.setup_publish()
        data['statusCheckRollup'] = []
        with patch.object(self.coordinator, 'pr_data', return_value=data), patch.object(self.coordinator, 'gh') as gh:
            self.assertEqual(self.tick()['status'], 'awaiting_ci')
            gh.assert_not_called()
        data['statusCheckRollup'] = [dict(name='persistence-and-security', status='COMPLETED', conclusion='FAILURE'),
                                  dict(name='frontend-and-browser', status='COMPLETED', conclusion='SUCCESS')]
        with patch.object(self.coordinator, 'pr_data', return_value=data):
            self.assertEqual(self.tick()['reason'], 'ci_failed_or_skipped')

    def test_pr_changed_head_blocks(self):
        data = self.setup_publish()
        data['headRefOid'] = SHA
        with patch.object(self.coordinator, 'pr_data', return_value=data):
            self.assertEqual(self.tick()['reason'], 'pr_head_or_base_changed')

    def test_unknown_pr_restart_never_duplicates(self):
        self.reviewed()
        self.coordinator.publish = True
        self.coordinator.job['inflight'] = 'create_pr'
        self.coordinator.save()
        with patch.object(self.coordinator, 'gh', return_value='[]') as gh:
            self.assertEqual(self.tick()['reason'], 'reconciliation_needed_unknown_publish_outcome')
            self.assertEqual(gh.call_count, 1)
            self.assertEqual(gh.call_args.args[:2], ('pr', 'list'))

    def test_known_pr_restart_reconciles_and_waits(self):
        self.reviewed()
        data = pr(self.coordinator.job['commit'], checks=[])
        self.coordinator.publish = True
        self.coordinator.job['inflight'] = 'create_pr'
        self.coordinator.save()
        with patch.object(self.coordinator, 'gh', return_value=json.dumps([data])) as gh, \
                patch.object(self.coordinator, 'pr_data', return_value=data):
            self.assertEqual(self.tick()['status'], 'awaiting_ci')
            self.assertIsNone(self.coordinator.job['inflight'])
            self.assertEqual(gh.call_count, 1)

    def test_merge_requires_second_head_check_and_match_commit(self):
        data = self.setup_publish()
        merged = dict(data, state='MERGED')
        with patch.object(self.coordinator, 'pr_data', side_effect=[data, data, merged]), \
                patch.object(self.coordinator, 'gh', return_value='') as gh:
            self.assertEqual(self.tick()['status'], 'merged')
            args = gh.call_args.args
            self.assertIn('--match-head-commit', args)
            self.assertIn(self.coordinator.job['commit'], args)
            self.assertNotIn('--admin', args)


class ValidationTests(unittest.TestCase):
    def test_quota_all_windows_finite_and_below_reserve(self):
        for used in (85, 100, -1, float('nan'), float('inf'), True, '10', None):
            self.assertFalse(eligible(metadata(used)))
        good = metadata()
        good['windows'].append({'usedPercent': 85})
        self.assertFalse(eligible(good))
        self.assertTrue(eligible(metadata(84.9)))
        self.assertFalse(eligible({}))

    def test_strict_qa_no_regex_acceptance(self):
        for value in ['APPROVED', {'approved': True, 'commit': SHA, 'issues': ['bug']},
                      {'approved': 1, 'commit': SHA, 'issues': []},
                      {'approved': True, 'commit': SHA, 'issues': [], 'extra': True}]:
            self.assertFalse(qa_approved(value, SHA))

    def test_ci_every_check_including_additional(self):
        data = pr()
        for conclusion in ('SKIPPED', 'NEUTRAL', 'FAILURE', 'CANCELLED', None):
            data['statusCheckRollup'][-1]['conclusion'] = conclusion
            with self.assertRaises(Blocked):
                ci_result(data, SHA)
        data = pr()
        data['statusCheckRollup'][-1].update(status='IN_PROGRESS', conclusion=None)
        self.assertEqual(ci_result(data, SHA), 'pending')
        self.assertEqual(ci_result(pr(), SHA), 'passed')
        self.assertEqual(ci_result(pr(checks=[]), SHA), 'pending')

    def test_paths_reject_traversal_absolute_and_symlinks(self):
        for name in ('../outside.md', '/tmp/a', 'C:/secret', 'a\\b'):
            with self.assertRaises(Blocked):
                inside(Path.cwd(), name)

    def test_ci_requires_each_expected_check_even_when_all_reported_checks_pass(self):
        for name in ('persistence-and-security', 'frontend-and-browser',
                     'windows-automation', 'nightly-installer-construction'):
            with self.subTest(missing=name):
                data = pr()
                data['statusCheckRollup'] = [c for c in data['statusCheckRollup'] if c['name'] != name]
                self.assertEqual(ci_result(data, SHA), 'pending')
        self.assertEqual(ci_result(pr(checks=[dict(name='extra', status='COMPLETED',
                                                 conclusion='SUCCESS')]), SHA), 'pending')

    def test_settings_bounded(self):
        for settings in ({'max_jobs': 0}, {'max_calls': 41}, {'hours': 9}, {'timeout': 901}):
            with self.assertRaises(ValueError):
                Coordinator(**settings)

    def test_usage_and_ports(self):
        self.assertEqual(numeric_usage({'a': 1, 'b': True, 'c': 'secret', 'd': float('nan')}), {'a': 1})
        selected = ports()
        self.assertNotEqual(selected['POSEIDON_API_PORT'], selected['POSEIDON_UI_PORT'])

    def test_sanitized_environment_no_inherited_context(self):
        fixture = tempfile.TemporaryDirectory()
        self.addCleanup(fixture.cleanup)
        workers = Workers.__new__(Workers)
        workers.root = Path(fixture.name)
        workers.home_root = workers.root / 'profiles'
        workers.profiles = {'esra-pro': {}}
        (workers.root / '.tools/node/node-v26.13.0-win-x64').mkdir(parents=True)
        (workers.root / '.tools/dotnet').mkdir(parents=True)
        with patch.dict(os.environ, {'ANTHROPIC_API_KEY': 'private', 'CODEX_SESSION_ID': 'private',
                                     'MCP_TOOL_ID': 'private', 'OPENAI_API_KEY': 'private'}):
            environment = workers.environment('esra-pro')
        for key in ('ANTHROPIC_API_KEY', 'CODEX_SESSION_ID', 'MCP_TOOL_ID', 'OPENAI_API_KEY'):
            self.assertNotIn(key, environment)
        self.assertEqual(environment['PYTHONUTF8'], '1')
        self.assertIn(str(workers.root / '.tools/node/node-v26.13.0-win-x64'), environment['PATH'])


class AccountSelectionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.workers = FakeWorkers(self.root)
        self.coordinator = Coordinator(self.root, workers=self.workers)

    def test_stopped_metadata_failure_checks_alternate_own_quota(self):
        original = self.workers.account
        def account(profile):
            if profile == 'kadir-plus':
                raise RuntimeError('metadata unavailable')
            return original(profile)
        self.workers.account = account
        with self.coordinator.account('kadir-plus') as profile:
            self.assertEqual(profile, 'esra-pro')
            self.assertTrue((self.workers.home(profile) / 'worker.lock').exists())
        self.assertFalse((self.workers.home('kadir-plus') / 'worker.lock').exists())

    def test_both_metadata_failures_wait_without_dispatch(self):
        self.workers.account = Mock(side_effect=RuntimeError('metadata unavailable'))
        with self.coordinator.account('esra-pro') as profile:
            self.assertIsNone(profile)
        self.assertEqual(self.workers.account.call_count, 2)
        self.assertEqual(self.workers.calls, [])

    def test_work_failure_propagates_without_account_retry(self):
        original = self.workers.account
        self.workers.account = Mock(side_effect=original)
        with self.assertRaises(BusyError):
            with self.coordinator.account('esra-pro'):
                raise BusyError('job failure')
        self.assertEqual(self.workers.account.call_count, 1)

    def test_uncertain_metadata_termination_keeps_lock_and_stops(self):
        from codex_accounts import WorkerStillRunningError
        self.workers.account = Mock(side_effect=WorkerStillRunningError())
        with self.assertRaises(WorkerStillRunningError):
            with self.coordinator.account('esra-pro'):
                self.fail('must not dispatch')
        self.assertEqual(self.workers.account.call_count, 1)
        self.assertTrue((self.workers.home('esra-pro') / 'worker.lock').exists())


if __name__ == '__main__':
    unittest.main()
