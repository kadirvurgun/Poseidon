"""OPS-DASH-RUNTIME-01 synthetic files and loopback HTTP only; no live services."""
import http.client
import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import Mock, patch

from codex_accounts import write_json, exclusive, BusyError
from ops_runtime import Driver, Store, iso, server
from nightly_coordinator import Blocked, Coordinator
from test_nightly_coordinator import FakeWorkers


class RuntimeTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.now = 1800000000
        self.store = Store(self.root, lambda: self.now)
        self.quota()

    def quota(self, used=90, weekly=91, state='known'):
        profiles = {p: {'state': state, 'ready': used < 85 and weekly < 85, 'plan': 'plus',
            'windows': [{'usedPercent': used, 'windowDurationMins': 300, 'resetsAt': self.now + 300},
                        {'usedPercent': weekly, 'windowDurationMins': 10080, 'resetsAt': self.now + 9000}],
            'email': 'PRIVATE_EMAIL', 'error': 'PRIVATE_ERROR'} for p in ('esra-pro', 'kadir-plus')}
        write_json(self.root / '.runtime/account-workers/quota.json', {
            'timestamp': iso(self.now), 'next_check': iso(self.now + 3600), 'profiles': profiles})

    def test_contract_flat_jobs_actual_windows_project_and_redaction(self):
        write_json(self.root / '.runtime/nightly/state.json', {'model_calls': 2, 'jobs': {'OPS-TEST-01': {
            'status': 'blocked', 'reason': 'qa_used_tools', 'commit': 'a' * 40,
            'worktree': 'PRIVATE_PATH', 'prompt': 'PRIVATE_PROMPT', 'calls': [
                {'profile': 'esra-pro', 'phase': 'development', 'usage': {'input_tokens': 7, 'secret': 'PRIVATE'}}]}}})
        write_json(self.root / 'docs/project-status.json', {'completion_percent': .82, 'accepted_story_points': 8,
            'total_story_points': 970, 'accepted_tasks': 2, 'total_tasks': 121, 'current_stage': 'S0; other'})
        write_json(self.store.runtime / 'activities/a.json', {'id': 'activity-1', 'profile': 'esra-pro',
            'taskId': 'OPS-TEST-01', 'status': 'running', 'startedAt': iso(self.now), 'prompt': 'PRIVATE'})
        status = self.store.status()
        self.assertEqual(status['schemaVersion'], 1)
        self.assertIsInstance(status['coordinator']['jobs'], list)
        self.assertEqual(status['coordinator']['jobs'][0]['reason'], 'qa_used_tools')
        self.assertEqual(status['project']['completionPercent'], .82)
        self.assertEqual(len(status['activities']), 1)
        self.assertEqual(len(status['alarms']), 4)
        self.assertNotIn('PRIVATE', json.dumps(status))
        self.assertEqual(status['events'], [])

    def test_malformed_file_shapes_are_unknown_without_private_fields(self):
        for value in ([], {'profiles': []}, {'profiles': {'esra-pro': {'state': [], 'plan': {}}}}):
            write_json(self.root / '.runtime/account-workers/quota.json', value)
            self.assertFalse(self.store.status()['quota']['profiles']['esra-pro']['ready'])
        write_json(self.root / '.runtime/nightly/state.json', {'jobs': {'OPS-TEST': {
            'status': {}, 'phase': [], 'reason': 'PRIVATE_ERROR', 'calls': [None, {}]}}})
        write_json(self.store.runtime / 'service.json', {'status': []})
        status = self.store.status()
        self.assertNotIn('PRIVATE_ERROR', json.dumps(status))
        self.assertEqual(status['coordinator']['jobs'][0]['reason'], 'operation_blocked')

    def test_busy_last_known_is_never_ready_and_stale_is_unknown(self):
        path = self.root / '.runtime/account-workers/quota.json'
        value = json.loads(path.read_text())
        value['profiles']['esra-pro'] = {'state': 'busy', 'ready': True, 'lastKnown': {
            'windows': [{'usedPercent': 10, 'windowDurationMins': 10080, 'resetsAt': self.now + 300}],
            'observedAt': iso(self.now - 60)}}
        write_json(path, value)
        profile = self.store.status()['quota']['profiles']['esra-pro']
        self.assertFalse(profile['ready'])
        self.assertEqual(profile['lastKnown']['observedAt'], iso(self.now - 60))
        self.now += 4501
        self.assertEqual(self.store.status()['quota']['profiles']['kadir-plus']['state'], 'unavailable')

    def test_readonly_strict_host_and_only_exact_route(self):
        service = server(self.store, 0)
        worker = threading.Thread(target=service.serve_forever, daemon=True)
        worker.start()
        self.addCleanup(service.server_close)
        self.addCleanup(service.shutdown)
        for method, path, host, expected in [
                ('GET', '/ops/status', None, 200), ('GET', '/ops/status', 'evil.invalid', 403),
                ('GET', '/ops/status', 'localhost', 403), ('GET', '/ops/status?x=1', None, 404),
                ('GET', '/../../private', None, 404), ('POST', '/ops/status', None, 501)]:
            connection = http.client.HTTPConnection('127.0.0.1', service.server_port, timeout=2)
            connection.request(method, path, headers={'Host': host} if host else {})
            response = connection.getresponse()
            self.assertEqual(response.status, expected)
            self.assertIsNone(response.getheader('Access-Control-Allow-Origin'))
            response.read()
            connection.close()

    def test_http_remains_responsive_while_single_background_worker_waits(self):
        self.quota(used=10, weekly=18)
        entered, release = threading.Event(), threading.Event()
        coordinator = Mock()
        def run():
            entered.set()
            release.wait(5)
            return {'jobs': {}}
        coordinator.once.side_effect = run
        driver = Driver(self.store, Mock(), coordinator)
        worker = threading.Thread(target=driver.tick)
        service = server(self.store, 0)
        http_worker = threading.Thread(target=service.serve_forever, daemon=True)
        http_worker.start()
        worker.start()
        try:
            self.assertTrue(entered.wait(2))
            connection = http.client.HTTPConnection('127.0.0.1', service.server_port, timeout=2)
            connection.request('GET', '/ops/status')
            response = connection.getresponse()
            self.assertEqual(response.status, 200)
            self.assertEqual(json.loads(response.read())['coordinator']['status'], 'running')
            connection.close()
        finally:
            release.set()
            worker.join(5)
            service.shutdown()
            service.server_close()

    def test_existing_durable_runtime_lease_prevents_second_mutator(self):
        driver = Driver(self.store, Mock(), Mock())
        with exclusive(self.store.runtime / 'runtime.lock'):
            with self.assertRaises(BusyError):
                driver.run(threading.Event())
        driver.watch.once.assert_not_called()
        driver.coordinator.once.assert_not_called()

    def test_no_reset_timestamp_means_no_invented_resume_alarm(self):
        path = self.root / '.runtime/account-workers/quota.json'
        value = json.loads(path.read_text())
        for p in value['profiles'].values():
            for w in p['windows']:
                w.pop('resetsAt')
        write_json(path, value)
        alarms = self.store.status()['alarms']
        self.assertTrue(all(a['state'] == 'waiting' and 'resumeAt' not in a for a in alarms))

    def test_event_log_bounded_and_unknown_event_fields_removed(self):
        self.store.runtime.mkdir(parents=True)
        (self.store.runtime / 'events.jsonl').write_text(''.join(json.dumps({
            'id': str(index), 'at': iso(self.now), 'type': 'snapshot', 'usedPercent': index}) + '\n'
            for index in range(504)), encoding='utf-8')
        self.store.record('snapshot', profile='esra-pro', usedPercent=504, prompt='PRIVATE')
        self.store.record('unknown', message='PRIVATE')
        events = self.store.status()['events']
        self.assertEqual(len(events), 500)
        self.assertEqual(events[-1]['usedPercent'], 504)
        self.assertNotIn('PRIVATE', json.dumps(events))

    def test_metadata_only_pause_real_reset_then_recheck_weekly_still_blocks(self):
        watch = Mock()
        coordinator = Mock()
        driver = Driver(self.store, watch, coordinator)
        driver.tick()
        coordinator.once.assert_not_called()
        self.assertEqual(driver.poll_at, self.now + 360)
        self.now += 360
        watch.once.side_effect = lambda: self.quota(used=2, weekly=91)
        driver.tick()
        self.assertEqual(watch.once.call_count, 2)
        coordinator.once.assert_not_called()
        self.assertEqual(driver.poll_at, self.now + 3600)
        self.now += 3600
        watch.once.side_effect = lambda: self.quota(used=2, weekly=18)
        coordinator.once.return_value = {'jobs': {}}
        driver.tick()
        coordinator.once.assert_called_once()

    def test_unknown_quota_never_dispatches_and_no_synthetic_five_hour_window(self):
        self.quota(state='unavailable')
        coordinator = Mock()
        Driver(self.store, Mock(), coordinator).tick()
        coordinator.once.assert_not_called()
        path = self.root / '.runtime/account-workers/quota.json'
        value = json.loads(path.read_text())
        value['profiles']['esra-pro']['windows'] = value['profiles']['esra-pro']['windows'][1:]
        write_json(path, value)
        self.assertEqual(len(self.store.status()['quota']['profiles']['esra-pro']['lastKnown']['windows']), 1)

    def continuous(self, state):
        write_json(self.root / '.runtime/nightly/state.json', state)
        write_json(self.root / 'automation/nightly-queue.json', [])
        return Coordinator(self.root, enabled=True, continuous=True, max_jobs=4, max_calls=8,
            workers=FakeWorkers(self.root), wall=lambda: self.now)

    def test_rollover_durable_no_restart_reset_preserves_jobs_and_dependency_records(self):
        state = {'started': self.now - 28800, 'deadline': self.now + 10, 'max_jobs': 4, 'max_calls': 8,
            'model_calls': 8, 'jobs': {'OLD': {'status': 'merged'}, 'NEXT': {'status': 'awaiting_qa'}},
            'window_jobs': ['OLD', 'NEXT']}
        coordinator = self.continuous(state)
        with patch.object(coordinator, 'git', side_effect=lambda *a: 'automation/nightly-queue.json' if a[0] == 'ls-files' else ''):
            self.assertEqual(coordinator.once()['model_calls'], 8)
        self.now += 10
        with patch.object(coordinator, 'git', side_effect=lambda *a: 'automation/nightly-queue.json' if a[0] == 'ls-files' else ''):
            renewed = coordinator.once()
        self.assertEqual(renewed['model_calls'], 0)
        self.assertEqual(renewed['jobs'], state['jobs'])
        self.assertEqual(len(renewed['renewals']), 1)
        restarted = self.continuous(renewed)
        with patch.object(restarted, 'git', side_effect=lambda *a: 'automation/nightly-queue.json' if a[0] == 'ls-files' else ''):
            self.assertEqual(restarted.once()['deadline'], renewed['deadline'])

    def test_rollover_refuses_blocked_or_uncertain_phase(self):
        for job in [{'status': 'blocked', 'reason': 'qa_used_tools'},
                    {'status': 'awaiting_qa', 'inflight': 'qa_model'}, {'status': 'verifying'}]:
            coordinator = self.continuous({'started': 1, 'deadline': self.now - 1,
                'max_jobs': 4, 'max_calls': 8, 'model_calls': 8, 'jobs': {'S0-ADR-01': job}})
            state = coordinator.once()
            self.assertEqual(state['reason'], 'reconciliation_required_before_renewal')
            self.assertEqual(state['jobs']['S0-ADR-01'], job)
            self.assertEqual(state['model_calls'], 8)

    def test_rollover_waits_for_profile_lock(self):
        coordinator = self.continuous({'started': 1, 'deadline': self.now - 1,
            'max_jobs': 4, 'max_calls': 8, 'model_calls': 8, 'jobs': {}})
        with exclusive(coordinator.workers.home('esra-pro') / 'worker.lock'):
            self.assertEqual(coordinator.once()['reason'], 'reconciliation_required_before_renewal')

    def test_continuous_requires_committed_queue_before_dispatch(self):
        coordinator = self.continuous({'started': self.now, 'deadline': self.now + 100,
            'max_jobs': 4, 'max_calls': 8, 'model_calls': 0, 'jobs': {}})
        with patch.object(coordinator, 'git', side_effect=['automation/nightly-queue.json',
                ' M automation/nightly-queue.json']):
            with self.assertRaisesRegex(Blocked, 'must_be_committed'):
                coordinator.once()

    def test_call_cap_wait_keeps_qa_stage_without_dispatch(self):
        state = {'started': 1, 'deadline': self.now + 100, 'max_jobs': 4, 'max_calls': 8,
            'model_calls': 8, 'jobs': {}}
        coordinator = self.continuous(state)
        (self.root / 'task.md').write_text('Trusted synthetic task')
        item = {'id': 'OPS-TEST-01', 'task': 'task.md', 'source': 'user:OPS-DASH-RUNTIME-01',
                'acceptance': ['fixture'], 'dependencies': [], 'scope': ['scripts/example.py']}
        write_json(self.root / 'automation/nightly-queue.json', [item])
        state['jobs'][item['id']] = {'id': item['id'], 'status': 'awaiting_qa', 'metadata': item}
        write_json(self.root / '.runtime/nightly/state.json', state)
        with patch.object(coordinator, 'git', side_effect=lambda *a: 'automation/nightly-queue.json\0task.md' if a[0] == 'ls-files' else ''), \
                patch.object(coordinator, 'review') as review:
            result = coordinator.once()
        review.assert_not_called()
        self.assertEqual(result['jobs'][item['id']]['status'], 'awaiting_qa')
        self.assertEqual(result['reason'], 'window_call_budget_wait')
        self.assertEqual(self.store.status()['alarms'][-1]['resumeAt'], state['deadline'])


if __name__ == '__main__':
    unittest.main()
