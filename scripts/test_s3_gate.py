import json
import contextlib
import io
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import MagicMock, patch
import s3_gate


class OwnedS3GateTests(unittest.TestCase):
    def test_child_does_not_inherit_operator_lab_database_or_ambient_provider_authority(self):
        inherited = dict(PATH='synthetic-tools', POSEIDON_LAB_CONFIG='private-config-selector',
                         POSEIDON_DATABASE='private-database-selector', AWS_PROFILE='ambient',
                         MINIO_ROOT_USER='ambient', POSEIDON_GATE_S3_OWNER='foreign')
        with patch('s3_gate.environment', return_value=inherited):
            self.assertEqual({'PATH': 'synthetic-tools'}, s3_gate.isolated_environment())

    def test_invalid_child_deadlines_cannot_inspect_or_start_a_provider(self):
        with patch('s3_gate.docker') as docker:
            for deadline in [0, -1, 3601, 1.5, '900', True, False]:
                with self.assertRaisesRegex(RuntimeError, 'invalid_child_deadline'):
                    s3_gate.run('sha256:' + 'a' * 64, ['test'], deadline)
            docker.assert_not_called()

    def test_mutable_images_and_missing_commands_cannot_start_a_provider(self):
        with patch('s3_gate.docker') as docker:
            for image, command in [('minio/minio:latest', ['test']), ('sha256:' + 'a' * 64, []),
                                   ('sha256:'+'a'*64,'shell-string'),('sha256:'+'a'*64,['bad\0argument'])]:
                with self.assertRaises(RuntimeError):
                    s3_gate.run(image, command)
            docker.assert_not_called()

    def test_binding_must_be_single_owned_loopback_and_cleanup_still_checks_identity(self):
        state = {'Id': 'owned-id', 'Name': '/owned-name', 'Config': {'Labels': {s3_gate.LABEL: 'owner'}}, 'NetworkSettings': {'Ports': {'9000/tcp': [{'HostIp': '0.0.0.0', 'HostPort': '9000'}]}}}
        with patch('s3_gate.docker', return_value=json.dumps([state])):
            with self.assertRaises(RuntimeError): s3_gate.owned('owned-id', 'owner', 'owned-name')
            self.assertIsNone(s3_gate.owned('owned-id', 'owner', 'owned-name', require_binding=False))
            with self.assertRaises(RuntimeError): s3_gate.owned('owned-id', 'different-owner', 'owned-name', require_binding=False)
        state['NetworkSettings']['Ports']['9000/tcp'][0]['HostIp'] = '127.0.0.1'
        with patch('s3_gate.docker', return_value=json.dumps([state])):
            self.assertEqual(9000, s3_gate.owned('owned-id', 'owner', 'owned-name'))

    def test_unlabelled_local_images_are_not_silently_treated_as_a_real_owned_provider(self):
        image = 'sha256:' + 'b' * 64
        with patch('s3_gate.docker', return_value=json.dumps([{'Id': image, 'Config': {'Labels': {}}}])) as docker:
            with self.assertRaises(RuntimeError): s3_gate.run(image, ['test'])
            self.assertEqual(1, docker.call_count)



class OwnedS3ChildLifecycleTests(unittest.TestCase):
    def run_fixture(self, failure=None, exit_code=0, stop_failure=None, cleanup_failure=None, already_exited=False):
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        root = Path(temporary.name); events = []; evidence = {}; image = 'sha256:' + 'a'*64
        process = MagicMock(); process.pid = 123456
        process.wait.side_effect = failure if failure else None
        process.wait.return_value = exit_code
        process.poll.return_value = exit_code if already_exited else None
        def stop(owned):
            self.assertIs(process, owned); events.append('stop-owned-tree')
            if stop_failure: raise stop_failure
        def docker(*args):
            if args[:2] == ('image', 'inspect'):
                return json.dumps([{'Id':image,'Config':{'Labels':{
                    'org.opencontainers.image.source':'https://github.com/minio/minio',
                    'org.opencontainers.image.revision':'b'*40}}}])
            if args[0] == 'run': events.append('start-owned-provider'); return 'exact-owned-container'
            if args[0] == 'rm':
                self.assertEqual(('rm','--force','exact-owned-container'), args)
                events.append('remove-owned-provider')
                if cleanup_failure: raise cleanup_failure
                return ''
            self.fail('unexpected Docker command')
        def owned(identity, owner, name, require_binding=True):
            self.assertEqual('exact-owned-container', identity)
            if not require_binding: events.append('verify-owned-cleanup')
            return 55000
        response = MagicMock(); response.__enter__.return_value.status = 200
        opener = MagicMock(); opener.open.return_value = response
        tree=MagicMock()
        def finish(owned_process, owned_tree, force=False):
            self.assertIs(process,owned_process);self.assertIs(tree,owned_tree)
            if force:
                s3_gate.Workers.stop(owned_process)
                return True
            return False
        with patch.object(s3_gate, 'ROOT', root), patch.object(s3_gate, 'docker', side_effect=docker), \
                patch.object(s3_gate, 'owned', side_effect=owned), \
                patch.object(s3_gate, 'private_directory', side_effect=lambda p:p.mkdir(parents=True)), \
                patch.object(s3_gate, 'write', side_effect=lambda p,t:p.write_text(t)), \
                patch.object(s3_gate, 'isolated_environment', return_value={}), \
                patch.object(s3_gate.urllib.request, 'build_opener', return_value=opener), \
                patch.object(s3_gate, 'launch_owned_child', return_value=(process,tree)) as popen, \
                patch.object(s3_gate, 'finish_owned_tree', side_effect=finish), \
                patch.object(s3_gate.Workers, 'stop', side_effect=stop) as stopped:
            try:
                result = s3_gate.run(image, ['closed-test-child'], 17, evidence)
                caught = None
            except BaseException as exception:
                result = None; caught = exception
        self.assertEqual([], list(root.rglob('container.env')))
        return result, caught, evidence, events, popen, stopped, process

    def test_normal_child_retains_exact_exit_and_cleans_only_owned_provider_without_tree_kill(self):
        result, failure, evidence, events, popen, stopped, process = self.run_fixture(exit_code=7)
        self.assertEqual(7, result); self.assertIsNone(failure); stopped.assert_not_called()
        self.assertEqual('EXITED', evidence['child_status']); self.assertEqual(7, evidence['child_exit_code'])
        self.assertEqual('REMOVED_EXACT_OWNED_CONTAINER', evidence['provider_cleanup'])
        self.assertEqual(['start-owned-provider','verify-owned-cleanup','remove-owned-provider'], events)
        process.wait.assert_called_once_with(timeout=17)
        self.assertEqual(['closed-test-child'],popen.call_args.args[0])

    def test_timeout_stops_only_live_owned_child_tree_before_provider_cleanup(self):
        result, failure, evidence, events, _, stopped, process = self.run_fixture(
            failure=subprocess.TimeoutExpired('private-command',17,output=b'private-sdk-body'))
        self.assertIsNone(result); self.assertEqual('owned_child_deadline', str(failure))
        stopped.assert_called_once_with(process)
        self.assertEqual('DEADLINE', evidence['child_status']); self.assertIsNone(evidence['child_exit_code'])
        self.assertEqual('STOPPED_BEFORE_PROVIDER_CLEANUP', evidence['child_tree_status'])
        self.assertLess(events.index('stop-owned-tree'), events.index('verify-owned-cleanup'))
        self.assertLess(events.index('stop-owned-tree'), events.index('remove-owned-provider'))
        self.assertNotIn('private-', json.dumps(evidence))

    def test_keyboard_interrupt_stops_owned_tree_before_provider_cleanup_and_never_emits_body(self):
        _, failure, evidence, events, _, stopped, process = self.run_fixture(failure=KeyboardInterrupt('private-body'))
        self.assertEqual('owned_child_interrupted', str(failure)); stopped.assert_called_once_with(process)
        self.assertEqual('INTERRUPTED', evidence['child_status'])
        self.assertLess(events.index('stop-owned-tree'),events.index('remove-owned-provider'))
        self.assertNotIn('private-body',json.dumps(evidence))

    def test_wait_failure_also_stops_only_owned_tree(self):
        _, failure, evidence, events, _, stopped, process = self.run_fixture(failure=OSError('private-body'))
        self.assertEqual('owned_child_wait_failed',str(failure)); stopped.assert_called_once_with(process)
        self.assertEqual('WAIT_FAILED',evidence['child_status'])
        self.assertLess(events.index('stop-owned-tree'),events.index('remove-owned-provider'))

    def test_unproven_tree_stop_defers_provider_removal_and_reports_failure(self):
        _, failure, evidence, events, _, stopped, _ = self.run_fixture(
            failure=subprocess.TimeoutExpired('child',17), stop_failure=OSError('private-cleanup'))
        self.assertEqual('owned_child_tree_stop_failed',str(failure)); stopped.assert_called_once()
        self.assertEqual('STOP_UNPROVEN',evidence['child_tree_status'])
        self.assertEqual('DEFERRED_CHILD_TREE_STOP_UNPROVEN',evidence['provider_cleanup'])
        self.assertNotIn('remove-owned-provider',events); self.assertNotIn('verify-owned-cleanup',events)
        self.assertNotIn('private-cleanup',json.dumps(evidence))

    def test_contained_tree_is_stopped_even_when_its_direct_parent_already_exited(self):
        _, failure, evidence, events, _, stopped, process = self.run_fixture(
            failure=subprocess.TimeoutExpired('child',17),already_exited=True)
        stopped.assert_called_once_with(process);self.assertEqual('owned_child_deadline',str(failure))
        self.assertEqual('REMOVED_EXACT_OWNED_CONTAINER',evidence['provider_cleanup'])
        self.assertLess(events.index('stop-owned-tree'),events.index('remove-owned-provider'))

    def test_provider_cleanup_failure_preserves_completed_child_exit(self):
        _, failure, evidence, _, _, stopped, _ = self.run_fixture(exit_code=9,cleanup_failure=OSError('private-container'))
        stopped.assert_not_called(); self.assertEqual('owned_s3_provider_cleanup_failed',str(failure))
        self.assertEqual(9,evidence['child_exit_code']); self.assertEqual('EXITED',evidence['child_status'])
        self.assertEqual('FAILED_OWNERSHIP_OR_REMOVAL',evidence['provider_cleanup'])
        self.assertEqual('REMOVED_PRIVATE_ENV_FILE',evidence['config_cleanup'])

    def test_main_emits_closed_timeout_or_interrupt_evidence_without_exception_command_or_credentials(self):
        for status, expected in (('DEADLINE',2),('INTERRUPTED',130)):
            def run(image,command,timeout,evidence):
                evidence.update(stage='owned_s3_child',child_status=status,child_exit_code=None,
                    child_tree_status='STOPPED_BEFORE_PROVIDER_CLEANUP',provider_cleanup='REMOVED_EXACT_OWNED_CONTAINER')
                raise RuntimeError('private-command-and-credential')
            output=io.StringIO()
            with patch.object(s3_gate,'run',side_effect=run),patch('sys.argv',['s3_gate','--image','sha256:'+'a'*64,'--','test']),contextlib.redirect_stdout(output):
                self.assertEqual(expected,s3_gate.main())
            evidence=json.loads(output.getvalue());self.assertEqual(status,evidence['child_status'])
            self.assertEqual(expected,evidence['exit_code']);self.assertNotIn('private-',output.getvalue())


class OwnedTreeContainmentTests(unittest.TestCase):
    def test_windows_launcher_uses_atomic_owned_job_and_closes_it_if_start_fails(self):
        tree=MagicMock();tree.start.side_effect=RuntimeError('private-error')
        with patch.object(s3_gate.os,'name','nt'),patch.object(s3_gate,'WindowsOwnedTree',return_value=tree):
            with self.assertRaises(RuntimeError):s3_gate.launch_owned_child(['owned-child'],{})
        tree.close.assert_called_once();tree.start.assert_called_once_with(['owned-child'],{})

    def test_dead_leader_descendants_are_terminated_by_owned_job_before_handle_close(self):
        events=[];tree=MagicMock();tree.active.return_value=4
        tree.stop.side_effect=lambda:events.append('stop');tree.close.side_effect=lambda:events.append('close')
        self.assertTrue(s3_gate.finish_owned_tree(MagicMock(),tree))
        self.assertEqual(['stop','close'],events)

    def test_job_close_still_contains_children_if_stop_proof_fails(self):
        tree=MagicMock();tree.active.return_value=4;tree.stop.side_effect=OSError('private')
        with self.assertRaises(OSError):s3_gate.finish_owned_tree(MagicMock(),tree,True)
        tree.close.assert_called_once()

    def test_unfinished_descendants_after_successful_or_failed_parent_exit_cannot_pass(self):
        for code in (0,7):
            process=MagicMock();process.wait.return_value=code;evidence={}
            with patch.object(s3_gate,'launch_owned_child',return_value=(process,object())), \
                    patch.object(s3_gate,'finish_owned_tree',return_value=True):
                with self.assertRaisesRegex(RuntimeError,'owned_child_unfinished_descendants'):
                    s3_gate.owned_child(['test'],{},1,evidence)
            self.assertEqual(code,evidence['child_exit_code'])
            self.assertEqual('EXITED_WITH_LIVE_DESCENDANTS',evidence['child_status'])
            self.assertEqual('STOPPED_BEFORE_PROVIDER_CLEANUP',evidence['child_tree_status'])

    def test_missing_containment_never_claims_posix_tree_stop(self):
        with self.assertRaisesRegex(RuntimeError,'owned_child_containment_unproven'):
            s3_gate.finish_owned_tree(MagicMock(),None)

    def test_actual_owned_tree_contains_parent_exit_with_live_descendant(self):
        import sys
        # Real bounded native process tree, no database/provider/network and no
        # externally supplied PID. Atomic job/session ownership is the only kill authority.
        grandchild='import time; time.sleep(30)'
        parent='import subprocess,sys; subprocess.Popen([sys.executable,"-c",'+repr(grandchild)+']); sys.exit(7)'
        evidence={}
        with self.assertRaisesRegex(RuntimeError,'owned_child_unfinished_descendants'):
            s3_gate.owned_child([sys.executable,'-c',parent],dict(os.environ),10,evidence)
        self.assertEqual(7,evidence['child_exit_code'])
        self.assertEqual('EXITED_WITH_LIVE_DESCENDANTS',evidence['child_status'])
        self.assertEqual('STOPPED_BEFORE_PROVIDER_CLEANUP',evidence['child_tree_status'])

    def test_actual_owned_tree_deadline_stops_parent_and_descendant(self):
        import sys
        grandchild='import time; time.sleep(30)'
        parent='import subprocess,sys,time; subprocess.Popen([sys.executable,"-c",'+repr(grandchild)+']); time.sleep(30)'
        evidence={}
        with self.assertRaisesRegex(RuntimeError,'owned_child_deadline'):
            s3_gate.owned_child([sys.executable,'-c',parent],dict(os.environ),.25,evidence)
        self.assertEqual('DEADLINE',evidence['child_status'])
        self.assertEqual('STOPPED_BEFORE_PROVIDER_CLEANUP',evidence['child_tree_status'])
