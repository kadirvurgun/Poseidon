"""Bounded native usage projection tests; authored, NOT RUN."""
from datetime import datetime, timezone
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from native_usage_observation import event_observation, recent_usage

NOW = 1800000000


def event(used=57, at=NOW):
    return {'timestamp': datetime.fromtimestamp(at, timezone.utc).isoformat(), 'type': 'event_msg',
            'payload': {'type': 'token_count', 'info': {'private': 'PRIVATE_CONTEXT'}, 'rate_limits': {
                'limit_id': 'codex', 'plan_type': 'prolite', 'credits': {'balance': '0'},
                'primary': {'used_percent': used, 'window_minutes': 10080, 'resets_at': NOW + 9999},
                'secondary': None}}}


def rollout(home, name='rollout-test.jsonl', events=None):
    day = datetime.fromtimestamp(NOW).astimezone()
    path = Path(home) / 'sessions' / day.strftime('%Y/%m/%d') / name
    path.parent.mkdir(parents=True, exist_ok=True)
    header = {'type': 'session_meta', 'payload': {'id': '10000000-0000-0000-0000-000000000001',
                                               'cwd': 'PRIVATE_PATH', 'instructions': 'PRIVATE_CONTEXT'}}
    path.write_text('\n'.join(json.dumps(row) for row in [header, *(events or [event()])]) + '\n', encoding='utf-8')
    return path


class NativeObservationTests(unittest.TestCase):
    def test_only_provider_token_count_projects_reported_windows_and_never_credit_balance(self):
        value = event_observation(event(), NOW)
        self.assertEqual(1, len(value['windows']))
        self.assertEqual(10080, value['windows'][0]['windowDurationMins'])
        self.assertEqual(57, value['windows'][0]['usedPercent'])
        self.assertNotIn('PRIVATE', json.dumps(value))
        self.assertNotIn('credits', value)
        self.assertNotIn('ready', value)
        for kind in ('tool_result', 'task_complete', 'error'):
            changed = event(); changed['payload']['type'] = kind
            self.assertIsNone(event_observation(changed, NOW))

    def test_malformed_future_stale_or_unbounded_values_never_become_known_usage(self):
        for changed in (event(at=NOW + 1), event(at=NOW - 4501), event(used=True),
                        event(used=float('nan')), event(used=-1), event(used=101)):
            self.assertIsNone(event_observation(changed, NOW))
        for key, value in (('window_minutes', True), ('window_minutes', 0),
                           ('resets_at', NOW - 1), ('used_percent', '12')):
            changed = event(); changed['payload']['rate_limits']['primary'][key] = value
            self.assertIsNone(event_observation(changed, NOW))
        changed = event(); changed['payload']['rate_limits']['limit_id'] = 'different-model'
        self.assertIsNone(event_observation(changed, NOW))

    def test_newest_event_is_selected_without_reading_another_profile_or_exporting_context(self):
        with tempfile.TemporaryDirectory() as directory:
            home = Path(directory) / 'esra'
            rollout(home, events=[event(56, NOW - 30), event(58, NOW - 10)])
            rollout(home, name='rollout-older.jsonl', events=[event(55, NOW - 60)])
            rollout(Path(directory) / 'kadir', events=[event(99)])
            observed = recent_usage(home, NOW)
            self.assertEqual(58, observed['windows'][0]['usedPercent'])
            self.assertNotIn('PRIVATE', json.dumps(observed))
            self.assertNotIn('session', json.dumps(observed))

    def test_partial_current_event_and_large_private_tool_output_do_not_hide_previous_valid_observation(self):
        with tempfile.TemporaryDirectory() as directory:
            path = rollout(directory)
            with path.open('ab') as stream:
                stream.write(b'PRIVATE_TOOL_OUTPUT' * 65536 + b'\n')
                stream.write(b'{"type":"event_msg","payload":')
            observed = recent_usage(directory, NOW)
            self.assertEqual(57, observed['windows'][0]['usedPercent'])
            self.assertNotIn('PRIVATE', json.dumps(observed))

    def test_missing_malformed_oversized_or_incompletely_enumerated_logs_fail_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            self.assertIsNone(recent_usage(directory, NOW))
            path = rollout(directory)
            with patch('native_usage_observation.MAX_FILES', 0):
                self.assertIsNone(recent_usage(directory, NOW))
            path.write_bytes(b'not-json\n')
            self.assertIsNone(recent_usage(directory, NOW))
            path.write_bytes(b' ' * (128 * 1024 + 1) + b'\n')
            self.assertIsNone(recent_usage(directory, NOW))

    def test_symlink_or_windows_junction_cannot_redirect_profile_session_reads(self):
        with tempfile.TemporaryDirectory() as directory:
            rollout(directory)
            for method in ('is_symlink', 'is_junction'):
                with self.subTest(method=method), patch.object(Path, method, return_value=True), \
                        patch.object(Path, 'open', side_effect=AssertionError('linked content must not be opened')):
                    self.assertIsNone(recent_usage(directory, NOW))


if __name__ == '__main__':
    unittest.main()
