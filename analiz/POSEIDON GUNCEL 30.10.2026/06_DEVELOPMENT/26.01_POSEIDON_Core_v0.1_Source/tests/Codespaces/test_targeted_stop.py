"""Deterministic pidfd stop guards. Fake target; no real process is signaled."""
import contextlib,io,sys,fcntl,importlib.util,os,pathlib,signal,tempfile,unittest
from unittest.mock import patch
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('targeted_stop',ROOT/'scripts/uat-stop-task-db.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class StopTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.home=pathlib.Path(self.temp.name);self.state=self.home/'.poseidon-synthetic-uat';self.state.mkdir(mode=0o700)
  for name in ['pgdata','socket']:(self.state/name).mkdir(mode=0o700)
  self.pidfile=self.state/'pgdata/postmaster.pid';self.pidfile.write_text('123\n');self.lock=self.state/'supervisor.lock';self.lock.touch()
 def exercise(self,identities=None,peer=None,diagnosis='TASK_DB_IDENTITY_MATCH',exits=None,namespace=None,remove=True):
  with contextlib.ExitStack() as stack:
   stack.enter_context(patch.object(m.pathlib.Path,'home',return_value=self.home))
   stack.enter_context(patch.object(m.diag,'self_namespace',side_effect=namespace))
   stack.enter_context(patch.object(m.diag,'process_identity',side_effect=identities or [('postgres',5),('postgres',5)]))
   stack.enter_context(patch.object(m.diag,'socket_peer',return_value=peer or (123,os.getuid())))
   stack.enter_context(patch.object(m.diag,'diagnose',side_effect=diagnosis if isinstance(diagnosis,Exception) or callable(diagnosis) else None,return_value=diagnosis))
   stack.enter_context(patch.object(m.os,'pidfd_open',side_effect=lambda *a:os.open('/dev/null',os.O_RDONLY)))
   poll=stack.enter_context(patch.object(m,'exited',side_effect=exits or [False,False,True]))
   def send(*args):
    self.assertEqual(args[1],signal.SIGTERM)
    with self.lock.open() as probe:
     with self.assertRaises(BlockingIOError):fcntl.flock(probe,fcntl.LOCK_EX|fcntl.LOCK_NB)
    if remove:self.pidfile.unlink()
   signal_call=stack.enter_context(patch.object(m.signal,'pidfd_send_signal',side_effect=send))
   try:result=m.stop()
   except Exception:result=('REFUSED',1)
   return result,signal_call,poll
 def test_success_holds_lock_and_signals_only_pinned_fd(self):
  result,send,_=self.exercise();self.assertEqual(result,('TASK_DB_STOPPED',0));send.assert_called_once()
  with self.lock.open() as probe:fcntl.flock(probe,fcntl.LOCK_EX|fcntl.LOCK_NB)
 def test_pid_reuse_or_peer_mismatch_never_signals(self):
  for kw in [{'identities':[('postgres',5),('postgres',6)]},{'peer':(456,os.getuid())},{'peer':(123,os.getuid()+1)},{'exits':[True]},{'exits':[False,True]}]:
   result,send,_=self.exercise(**kw);self.assertEqual(result,('REFUSED',1));send.assert_not_called()
 def test_failed_diagnostic_or_namespace_never_signals(self):
  for kw in [{'diagnosis':'CHECK_INCONCLUSIVE'},{'diagnosis':PermissionError()},{'namespace':RuntimeError()}]:
   result,send,_=self.exercise(**kw);self.assertEqual(result,('REFUSED',1));send.assert_not_called()
 def test_busy_supervisor_is_not_adopted(self):
  with self.lock.open() as owner:
   fcntl.flock(owner,fcntl.LOCK_EX|fcntl.LOCK_NB)
   result,send,_=self.exercise();self.assertEqual(result,('STOP_REFUSED_SUPERVISOR_BUSY',1));send.assert_not_called()
 def test_timeout_has_no_escalation_or_deletion(self):
  result,send,_=self.exercise(exits=[False,False,False],remove=False);self.assertEqual(result,('STOP_PENDING_NO_ESCALATION',1));send.assert_called_once();self.assertTrue(self.pidfile.exists())
 def test_residual_files_are_not_deleted(self):
  result,send,_=self.exercise(remove=False);self.assertEqual(result,('STOPPED_REQUIRES_INSPECTION',1));self.assertTrue(self.pidfile.exists())
 def test_missing_or_symlink_lock_is_not_created_or_followed(self):
  self.lock.unlink();result,send,_=self.exercise();self.assertEqual(result,('REFUSED',1));send.assert_not_called();self.assertFalse(self.lock.exists())
  self.lock.symlink_to(self.pidfile);result,send,_=self.exercise();self.assertEqual(result,('REFUSED',1));send.assert_not_called()
 def test_unsupported_pidfd_never_falls_back(self):
  with patch.object(m.os,'pidfd_open') as placeholder,patch.object(m.signal,'pidfd_send_signal') as send:
   del m.os.pidfd_open
   with self.assertRaises(m.Refused):m.stop()
   send.assert_not_called()
 def test_self_namespace_check_is_explicit(self):
  with patch.object(pathlib.Path,'read_text',return_value=f'NSpid: {os.getpid()+1}\n'):
   with self.assertRaises(m.diag.Inconclusive):m.diag.self_namespace()
 def test_pidfile_changes_during_revalidation_never_signals(self):
  def change():self.pidfile.write_text('456\n');return 'TASK_DB_IDENTITY_MATCH'
  result,send,_=self.exercise(diagnosis=change);self.assertEqual(result,('REFUSED',1));send.assert_not_called()
 def test_cli_requires_explicit_action_and_never_prints_exception_payload(self):
  with patch.object(sys,'argv',['uat-stop-task-db.py']),patch.object(m,'stop') as call,contextlib.redirect_stdout(io.StringIO()) as out:
   self.assertEqual(m.main(),1);call.assert_not_called();self.assertEqual(out.getvalue(),'STOP_REFUSED_ARGUMENTS\n')
  with patch.object(sys,'argv',['uat-stop-task-db.py','--graceful']),patch.object(m,'stop',side_effect=RuntimeError('PRIVATE_PAYLOAD')),contextlib.redirect_stdout(io.StringIO()) as out:
   self.assertEqual(m.main(),1);self.assertEqual(out.getvalue(),'STOP_REFUSED_OR_UNCONFIRMED\n')

if __name__=='__main__':unittest.main()
