"""OPS-HOURLY-01 acceptance: fake metadata, time, worker and scheduler only."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import Mock, patch

from codex_accounts import WorkerStillRunningError, exclusive
from codex_watch import PROFILES, Watch, eligible, run_loop


def metadata(used=10, plan='pro'):
    return {'ready': True, 'authenticated': True, 'plan_matches': True, 'plan': plan,
            'limits_known': True, 'exhausted': False,
            'windows': [{'usedPercent': used, 'windowDurationMins': 300, 'resetsAt': 2000000000}]}


class WatchTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.workers = Mock()
        self.workers.runtime = self.root / 'runtime'
        self.workers.home.side_effect = lambda profile: self.root / profile
        self.workers.account.side_effect = lambda profile: metadata(plan='pro' if profile == 'esra-pro' else 'plus')
        self.workers.run.return_value = {'status': 'needs_review', 'job_id': 'fake-job'}
        self.watch = Watch(self.workers, wall=lambda: 1700000000)

    def test_atomic_snapshot_both_profiles_no_default_model_dispatch(self):
        with patch('codex_accounts.os.replace', wraps=os.replace) as replace:
            result = self.watch.once()
        self.assertEqual(set(result['profiles']), set(PROFILES))
        self.assertEqual(result['timestamp'], '2023-11-14T22:13:20+00:00')
        self.assertEqual(result['next_check'], '2023-11-14T23:13:20+00:00')
        self.assertEqual(result['selected'], 'esra-pro')
        self.assertEqual(json.loads((self.workers.runtime / 'quota.json').read_text()), result)
        self.assertEqual(replace.call_args.args[1], self.workers.runtime / 'quota.json')
        self.assertFalse((self.workers.runtime / 'quota.tmp').exists())
        self.workers.run.assert_not_called()

    def test_reserve_unknown_invalid_and_exhausted_windows_fail_closed(self):
        for used in [85, 99, 100, -1, float('nan'), float('inf'), None, '10', True]:
            with self.subTest(used=used):
                self.assertFalse(eligible(metadata(used)))
        self.assertTrue(eligible(metadata(84.99)))
        for key in ['ready', 'authenticated', 'plan_matches', 'limits_known']:
            value = metadata()
            value[key] = False
            self.assertFalse(eligible(value))
        for windows in [[], [{}], [metadata()['windows'][0], {'usedPercent': 85}]]:
            value = metadata()
            value['windows'] = windows
            self.assertFalse(eligible(value))

    def test_failed_atomic_replace_keeps_previous_snapshot_and_never_dispatches(self):
        self.watch.once()
        path = self.workers.runtime / 'quota.json'
        original = path.read_bytes()
        with patch('codex_accounts.os.replace', side_effect=OSError('disk failure')):
            with self.assertRaises(OSError):
                self.watch.once(Path('trusted.md'))
        self.assertEqual(path.read_bytes(), original)
        self.workers.run.assert_not_called()

    def test_uncertain_termination_retains_watch_lock_even_when_snapshot_write_fails(self):
        self.workers.account.side_effect = [WorkerStillRunningError('private'), metadata()]
        with patch('codex_watch.write_json', side_effect=OSError('disk failure')):
            with self.assertRaises(WorkerStillRunningError):
                self.watch.once()
        self.assertTrue((self.workers.runtime / 'watch.lock').exists())
        self.assertTrue((self.workers.home('esra-pro') / 'worker.lock').exists())

    def test_esra_priority_uses_each_accounts_own_quota(self):
        self.workers.account.side_effect = lambda p: metadata(84 if p == 'esra-pro' else 1)
        self.assertEqual(self.watch.once()['selected'], 'esra-pro')
        self.workers.account.side_effect = lambda p: metadata(85 if p == 'esra-pro' else 84)
        self.assertEqual(self.watch.once()['selected'], 'kadir-plus')
        self.workers.account.side_effect = lambda p: metadata(100 if p == 'esra-pro' else 85)
        self.assertIsNone(self.watch.once()['selected'])

    def test_busy_profile_unknown_never_read_or_ready_and_no_concurrent_job(self):
        with exclusive(self.workers.home('esra-pro') / 'worker.lock'):
            result = self.watch.once(Path('trusted.md'))
        self.assertEqual(result['profiles']['esra-pro'], {'profile': 'esra-pro', 'state': 'busy', 'ready': False})
        self.workers.account.assert_called_once_with('kadir-plus')
        self.workers.run.assert_not_called()

    def test_raw_errors_and_unexpected_metadata_fields_never_persist(self):
        secret = 'secret@example.invalid TOKEN=private C:/private/auth.json'
        value = metadata()
        value.update(email=secret, diagnostic=secret, plan=secret)
        value['windows'][0]['resetsAt'] = secret
        self.workers.account.side_effect = [RuntimeError(secret), value]
        result = self.watch.once(Path('trusted.md'))
        serialized = (self.workers.runtime / 'quota.json').read_text()
        self.assertNotIn(secret, serialized)
        self.assertFalse(result['profiles']['esra-pro']['ready'])
        self.assertEqual(result['profiles']['esra-pro']['state'], 'unavailable')
        self.workers.run.assert_not_called()

    def test_uncertain_metadata_keeps_profile_and_watch_locks(self):
        self.workers.account.side_effect = [WorkerStillRunningError('private'), metadata()]
        with self.assertRaises(WorkerStillRunningError):
            self.watch.once()
        self.assertTrue((self.workers.home('esra-pro') / 'worker.lock').exists())
        self.assertTrue((self.workers.runtime / 'watch.lock').exists())
        self.assertFalse(json.loads((self.workers.runtime / 'quota.json').read_text())['profiles']['esra-pro']['ready'])

    def test_single_durable_dispatch_preserves_review_and_blocked(self):
        for status in ['needs_review', 'blocked', 'waiting_for_quota']:
            with self.subTest(status=status):
                receipt = self.workers.runtime / 'dispatch.json'
                receipt.unlink(missing_ok=True)
                self.workers.run.reset_mock()
                self.workers.run.return_value = {'status': status, 'job_id': 'fake-job'}
                self.assertEqual(self.watch.once(Path('trusted.md'))['dispatch'], status)
                self.assertEqual(self.watch.once(Path('trusted.md'))['dispatch'], 'existing_job_requires_review')
                self.workers.run.assert_called_once_with(Path('trusted.md'), 'esra-pro', single_account=True, eligibility=eligible)
                self.assertEqual(json.loads(receipt.read_text())['status'], status)

    def test_existing_job_or_uncertain_dispatch_cannot_duplicate(self):
        (self.workers.runtime / 'jobs' / 'old-job').mkdir(parents=True)
        self.watch.once(Path('trusted.md'))
        self.workers.run.assert_not_called()
        (self.workers.runtime / 'jobs' / 'old-job').rmdir()
        self.workers.run.side_effect = KeyboardInterrupt()
        with self.assertRaises(KeyboardInterrupt):
            self.watch.once(Path('trusted.md'))
        self.assertEqual(json.loads((self.workers.runtime / 'dispatch.json').read_text())['status'], 'dispatching')
        self.watch.once(Path('trusted.md'))
        self.workers.run.assert_called_once()

    def test_loop_uses_monotonic_hourly_wait_and_interrupts(self):
        now = [0]
        watch = Mock()
        watch.once.side_effect = [None, KeyboardInterrupt()]

        def sleep(seconds):
            self.assertLessEqual(seconds, 30)
            now[0] += seconds

        with self.assertRaises(KeyboardInterrupt):
            run_loop(watch, monotonic=lambda: now[0], sleep=sleep)
        self.assertEqual(now[0], 3600)
        self.assertEqual(watch.once.call_count, 2)

    def test_slow_iteration_does_not_burst(self):
        now = [0]
        watch = Mock()

        def once(task):
            if now[0] == 0:
                now[0] = 4000
            else:
                raise KeyboardInterrupt()

        watch.once.side_effect = once
        with self.assertRaises(KeyboardInterrupt):
            run_loop(watch, monotonic=lambda: now[0], sleep=lambda seconds: now.__setitem__(0, now[0] + seconds))
        self.assertEqual(now[0], 7600)


@unittest.skipUnless(os.name == 'nt', 'Windows scheduler construction')
class SchedulerTests(unittest.TestCase):
    def test_preview_constructs_task_with_mocked_scheduler_and_never_registers(self):
        script = Path(__file__).with_name('install-hourly-watch.ps1').resolve()
        with tempfile.TemporaryDirectory(prefix='quota scheduler ') as directory:
            python = Path(directory) / 'pythonw.exe'
            python.touch()
            # Every scheduler cmdlet is a fake. Execute the real installer in
            # Preview mode to validate argument binding and paths without install.
            command = r'''
function New-ScheduledTaskAction { param($Execute,$Argument,$WorkingDirectory) @{execute=$Execute; arguments=$Argument; cwd=$WorkingDirectory} }
function New-ScheduledTaskTrigger { param([switch]$Once,$At,$RepetitionInterval) @{once=$Once.IsPresent; interval=$RepetitionInterval.TotalSeconds} }
function New-ScheduledTaskPrincipal { param($UserId,$LogonType,$RunLevel) @{logon=$LogonType; level=$RunLevel} }
function New-ScheduledTaskSettingsSet { param($MultipleInstances,[switch]$StartWhenAvailable,$ExecutionTimeLimit) @{instances=$MultipleInstances; available=$StartWhenAvailable.IsPresent; timeout=$ExecutionTimeLimit.TotalSeconds} }
function New-ScheduledTask { param($Action,$Trigger,$Principal,$Settings) @{action=$Action; trigger=$Trigger; principal=$Principal; settings=$Settings} }
function Register-ScheduledTask { throw 'Registration forbidden in test' }
& $env:WATCH_INSTALLER -PythonPath $env:WATCH_PYTHON -Preview | ConvertTo-Json -Depth 5 -Compress
'''
            env = dict(os.environ, WATCH_INSTALLER=str(script), WATCH_PYTHON=str(python))
            result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-Command', command],
                                    env=env, capture_output=True, text=True, timeout=30, check=True)
            task = json.loads(result.stdout)
            self.assertEqual(task['action']['execute'], str(python))
            self.assertEqual(task['action']['arguments'], '"' + str(script.with_name('codex_watch.py')) + '" once')
            self.assertEqual(task['action']['cwd'], str(script.parent.parent))
            self.assertEqual(task['trigger'], {'once': True, 'interval': 3600})
            self.assertEqual(task['principal'], {'logon': 'Interactive', 'level': 'Limited'})
            self.assertEqual(task['settings'], {'instances': 'IgnoreNew', 'available': True, 'timeout': 300})


if __name__ == '__main__':
    unittest.main()
