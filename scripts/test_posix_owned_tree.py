"""QA-GATE-004: real Linux native groups, with synthetic provider lifecycle only.

No .NET, provider, network, daemon configuration or externally supplied PID.
"""
import json
from collections import Counter
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import MagicMock, patch

import api_test_gate
import s3_gate
from posix_owned_tree import PosixOwnedTree, SESSION_ENV, _stat, native_group_options


@unittest.skipUnless(sys.platform.startswith('linux'), 'Linux pidfd/session regression')
class PosixOwnedSessionTests(unittest.TestCase):
    def test_stale_or_foreign_session_marker_cannot_launch_native_capture(self):
        with patch.dict(os.environ, {SESSION_ENV: '1:0'}), patch.object(subprocess, 'Popen') as launch:
            with self.assertRaisesRegex(RuntimeError, 'owned_posix_session_unproven'):
                api_test_gate.capture(['unused-native'], Path.cwd(), 1)
            launch.assert_not_called()

    def test_standalone_capture_still_owns_its_separate_session(self):
        self.assertEqual({'start_new_session': True}, native_group_options({}))

    def test_leader_identity_remains_reserved_until_tree_cleanup(self):
        tree = PosixOwnedTree([sys.executable, '-c', 'raise SystemExit(7)'], dict(os.environ), Path.cwd())
        try:
            self.assertEqual(7, tree.wait(5))
            self.assertEqual('Z', _stat(tree.pid)[0])
            self.assertIsNone(tree.process.returncode)
            self.assertFalse(s3_gate.finish_owned_tree(tree, tree))
            self.assertEqual(7, tree.process.returncode)
        finally:
            if tree.process.returncode is None:
                tree.stop(); tree.close()

    def test_four_shards_retain_complete_disjoint_discovery_multiplicity(self):
        full = ['Poseidon.Api.Tests.Cases.Theory'] * 7 + [
            f'Poseidon.Api.Tests.Cases.Case{i}' for i in range(9)]
        groups = api_test_gate.partition_methods(full, 4)
        self.assertEqual([7, 3, 3, 3], [group.total() for group in groups])
        self.assertTrue(api_test_gate.valid_partition(full, groups, groups))
        self.assertEqual(Counter(full), sum(groups, Counter()))
        for i, group in enumerate(groups):
            for other in groups[i + 1:]:
                self.assertFalse(group.keys() & other.keys())

    def lifecycle(self, timeout=False, unproven=False):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            ready = root / 'ready.json'
            # Native grandchild has its own process group, but inherits the SID.
            grandchild = 'import time; time.sleep(30)'
            native = ('import json,os,subprocess,sys,time; '
                f'p=subprocess.Popen([sys.executable,"-c",{grandchild!r}],process_group=0); '
                f'open({str(ready)!r},"w").write(json.dumps([os.getpid(),p.pid,os.getsid(0),os.getpgrp()])); '
                'time.sleep(30)')
            parent = ('import os,sys,threading,time; from pathlib import Path; '
                f'sys.path.insert(0,{str(Path(s3_gate.__file__).parent)!r}); '
                'from api_test_gate import capture; '
                f'threading.Thread(target=lambda:capture([sys.executable,"-c",{native!r}],Path.cwd(),25),daemon=True).start(); '
                f'ready=Path({str(ready)!r}); '
                'deadline=time.monotonic()+5\n'
                'while not ready.exists():\n'
                ' if time.monotonic()>deadline: os._exit(19)\n'
                ' time.sleep(.01)\n'
                + ('time.sleep(30)' if timeout else 'os._exit(7)'))
            evidence = {}; events = []; launched = []; identities = []
            image = 'sha256:' + 'a' * 64
            original_launch = s3_gate.launch_owned_child
            original_stop = PosixOwnedTree.stop
            # Another test-owned invocation is never part of this gate's SID.
            unrelated = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(30)'],
                start_new_session=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

            def launch(command, env):
                process, tree = original_launch(command, env)
                launched.append(tree)
                # Establish actual native readiness before the timeout starts.
                deadline = time.monotonic() + 5
                while not ready.exists():
                    if time.monotonic() >= deadline:
                        raise AssertionError('native readiness deadline')
                    time.sleep(.01)
                pids = json.loads(ready.read_text())
                self.assertEqual(tree.pid, pids[2])
                self.assertNotEqual(tree.pid, pids[3])
                self.assertEqual(pids[0], pids[3])
                for pid in [tree.pid, *pids[:2]]:
                    state, sid, born = _stat(pid)
                    self.assertEqual(tree.pid, sid)
                    identities.append((pid, born))
                self.assertTrue(any(_stat(pid)[0] not in ('Z', 'X') for pid in pids[:2]))
                return process, tree

            def alive():
                for pid, born in identities:
                    try:
                        state, _, current = _stat(pid)
                    except FileNotFoundError:
                        continue
                    if current == born and state not in ('Z', 'X'):
                        return True
                return False

            def docker(*args):
                if args[:2] == ('image', 'inspect'):
                    return json.dumps([{'Id': image, 'Config': {'Labels': {
                        'org.opencontainers.image.source': 'https://github.com/minio/minio',
                        'org.opencontainers.image.revision': 'b' * 40}}}])
                if args[0] == 'run':
                    events.append('provider-start'); return 'synthetic-owned-provider'
                if args[0] == 'rm':
                    self.assertEqual(('rm', '--force', 'synthetic-owned-provider'), args)
                    self.assertTrue(identities)
                    self.assertFalse(alive(), 'provider removed while native process still alive')
                    self.assertIsNone(unrelated.poll(), 'another invocation was stopped')
                    events.append('provider-remove-after-no-live-members'); return ''
                self.fail('unexpected provider command')

            opener = MagicMock(); opener.open.return_value.__enter__.return_value.status = 200
            try:
                with patch.object(s3_gate, 'ROOT', root), patch.object(s3_gate, 'docker', side_effect=docker), \
                        patch.object(s3_gate, 'owned', return_value=55000), \
                        patch.object(s3_gate, 'private_directory', side_effect=lambda p:p.mkdir(parents=True)), \
                        patch.object(s3_gate, 'write', side_effect=lambda p,t:p.write_text(t)), \
                        patch.object(s3_gate, 'isolated_environment', return_value=dict(os.environ)), \
                        patch.object(s3_gate.urllib.request, 'build_opener', return_value=opener), \
                        patch.object(s3_gate, 'launch_owned_child', side_effect=launch):
                    if unproven:
                        with patch.object(PosixOwnedTree, 'stop', side_effect=RuntimeError('census-unavailable')):
                            with self.assertRaisesRegex(RuntimeError, 'owned_child_tree_stop_failed'):
                                s3_gate.run(image, [sys.executable, '-c', parent], 1, evidence)
                        self.assertTrue(alive())
                        self.assertEqual('STOP_UNPROVEN', evidence['child_tree_status'])
                        self.assertEqual('DEFERRED_CHILD_TREE_STOP_UNPROVEN', evidence['provider_cleanup'])
                        self.assertEqual(['provider-start'], events)
                    else:
                        expected = 'owned_child_deadline' if timeout else 'owned_child_unfinished_descendants'
                        with self.assertRaisesRegex(RuntimeError, expected):
                            s3_gate.run(image, [sys.executable, '-c', parent], 1 if timeout else 5, evidence)
                        self.assertFalse(alive())
                        self.assertEqual('STOPPED_BEFORE_PROVIDER_CLEANUP', evidence['child_tree_status'])
                        self.assertEqual('REMOVED_EXACT_OWNED_CONTAINER', evidence['provider_cleanup'])
                        self.assertEqual(['provider-start', 'provider-remove-after-no-live-members'], events)
                        if not timeout:
                            self.assertEqual(7, evidence['child_exit_code'])
                            self.assertEqual('EXITED_WITH_LIVE_DESCENDANTS', evidence['child_status'])
                        else:
                            self.assertEqual('DEADLINE', evidence['child_status'])
            finally:
                # Only this test's reserved sessions; also cleans intentional
                # unproven-stop injection after provider preservation is checked.
                for tree in launched:
                    if tree.process.returncode is None:
                        original_stop(tree)
                        tree.close()
                unrelated.kill(); unrelated.wait(timeout=5)

    def test_api_parent_nonzero_exit_kills_native_groups_before_provider_cleanup(self):
        self.lifecycle()

    def test_api_parent_timeout_kills_native_groups_before_provider_cleanup(self):
        self.lifecycle(timeout=True)

    def test_unproven_session_stop_preserves_provider_with_real_live_native_groups(self):
        self.lifecycle(unproven=True)


if __name__ == '__main__':
    unittest.main()
