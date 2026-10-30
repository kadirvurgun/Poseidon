"""Deterministic lifecycle faults; no database or child process is created."""
import importlib.util,json,pathlib,signal,subprocess,tempfile,unittest
from unittest.mock import Mock,patch
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('uat_lifecycle',ROOT/'scripts/uat.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class LifecycleTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.state=pathlib.Path(self.temp.name);self.data=self.state/'pgdata';self.sock=self.state/'socket'
  self.data.mkdir(mode=0o700);self.sock.mkdir(mode=0o700)
  (self.state/'owner.json').write_text(json.dumps({'purpose':'poseidon-synthetic-uat-v1','repository':str(ROOT)}))
  self.cluster=m.OwnedPostgres(self.data,self.sock,self.state,{})
  self.child=Mock(pid=777);self.child.poll.return_value=None
 def tearDown(self):self.temp.cleanup()
 def result(self,code):return subprocess.CompletedProcess([],code)
 def start_fault(self,error,pidfile=True,expected=None):
  def run(cmd,**kw):
   if 'status' in cmd:return self.result(3)
   if 'stop' in cmd:self.child.poll.return_value=0;return self.result(0)
   if pidfile:(self.data/'postmaster.pid').write_text('777\n')
   raise error
  with patch.object(m.subprocess,'run',side_effect=run) as action,patch.object(m.subprocess,'Popen',return_value=self.child) as spawn:
   with self.assertRaises(expected or type(error)):
    try:self.cluster.start()
    finally:self.cluster.close()
   self.assertTrue(self.cluster.attempted);spawn.assert_called_once();self.child.wait.assert_called_once()
   stops=[c for c in action.call_args_list if 'stop' in c.args[0]]
   self.assertEqual(len(stops),0)
   self.child.send_signal.assert_called_once_with(signal.SIGINT)
 def test_readiness_timeout_stops_owned_child(self):
  with patch.object(m.time,'monotonic',side_effect=[0,0,0,31,31]):
   # A probe timeout is retried until deadline, then the owned child is cleaned up.
   self.start_fault(subprocess.TimeoutExpired('pg_isready',2),expected=TimeoutError)
 def test_readiness_failure_stops_owned_child(self):self.start_fault(OSError('synthetic failure'))
 def test_ctrl_c_stops_owned_child(self):self.start_fault(KeyboardInterrupt())
 def test_interruption_before_pidfile_signals_only_tracked_child(self):self.start_fault(KeyboardInterrupt(),False)
 def test_spawn_failure_does_not_stop_any_cluster(self):
  with patch.object(m.subprocess,'run',return_value=self.result(3)) as action,patch.object(m.subprocess,'Popen',side_effect=OSError('spawn failed')):
   with self.assertRaises(OSError):
    try:self.cluster.start()
    finally:self.cluster.close()
   self.assertTrue(self.cluster.attempted);self.assertEqual(len(action.call_args_list),1)
 def test_preexisting_server_is_never_adopted_or_stopped(self):
  with patch.object(m.subprocess,'run',return_value=self.result(0)) as action,patch.object(m.subprocess,'Popen') as spawn:
   with self.assertRaises(RuntimeError):
    try:self.cluster.start()
    finally:self.cluster.close()
   self.assertFalse(self.cluster.attempted);spawn.assert_not_called();self.assertEqual(len(action.call_args_list),1)
 def test_unknown_status_is_not_permission_to_start_or_stop(self):
  with patch.object(m.subprocess,'run',return_value=self.result(4)) as action,patch.object(m.subprocess,'Popen') as spawn:
   with self.assertRaises(RuntimeError):
    try:self.cluster.start()
    finally:self.cluster.close()
   spawn.assert_not_called();self.assertEqual(len(action.call_args_list),1)
 def test_foreign_marker_rejected_before_any_command(self):
  (self.state/'owner.json').write_text('{}')
  with patch.object(m.subprocess,'run') as run,patch.object(m.subprocess,'Popen') as spawn:
   with self.assertRaises(RuntimeError):
    try:self.cluster.start()
    finally:self.cluster.close()
   run.assert_not_called();spawn.assert_not_called()
 def test_pid_mismatch_never_stops_replacement(self):
  self.cluster.identity=self.cluster.verify();self.cluster.attempted=True;self.cluster.process=self.child
  (self.data/'postmaster.pid').write_text('888\n')
  with patch.object(m.subprocess,'run') as run:
   with self.assertRaises(RuntimeError):self.cluster.close()
   run.assert_not_called();self.child.send_signal.assert_not_called()
 def test_stop_uses_only_retained_child_not_path_based_command(self):
  self.cluster.identity=self.cluster.verify();self.cluster.attempted=True;self.cluster.process=self.child
  (self.data/'postmaster.pid').write_text('777\n')
  with patch.object(m.subprocess,'run') as command:
   self.cluster.close();self.child.send_signal.assert_called_once_with(signal.SIGINT);self.child.wait.assert_called_once();command.assert_not_called()
 def test_creation_is_recorded_and_interrupts_deferred_until_handle_exists(self):
  original=signal.pthread_sigmask(signal.SIG_BLOCK,set())
  def spawn(*args,**kwargs):
   self.assertTrue(self.cluster.attempted)
   blocked=signal.pthread_sigmask(signal.SIG_BLOCK,set())
   self.assertTrue({signal.SIGINT,signal.SIGTERM}.issubset(blocked))
   raise OSError('synthetic spawn failure')
  with patch.object(m.subprocess,'run',return_value=self.result(3)),patch.object(m.subprocess,'Popen',side_effect=spawn):
   with self.assertRaises(OSError):
    try:self.cluster.start()
    finally:self.cluster.close()
  self.assertEqual(signal.pthread_sigmask(signal.SIG_BLOCK,set()),original)
 def test_exited_child_never_stops_a_replacement(self):
  self.cluster.identity=self.cluster.verify();self.cluster.attempted=True;self.cluster.process=self.child;self.child.poll.return_value=1
  with patch.object(m.subprocess,'run') as run:self.cluster.close();run.assert_not_called()
 def test_cleanup_timeout_is_explicit_unresolved_outcome(self):
  self.cluster.identity=self.cluster.verify();self.cluster.attempted=True;self.cluster.process=self.child
  self.child.wait.side_effect=subprocess.TimeoutExpired('owned postgres',20)
  with self.assertRaisesRegex(RuntimeError,'did not stop'):self.cluster.close()
 def test_successful_restart_after_fault(self):
  self.start_fault(OSError('synthetic readiness failure'))
  next_cluster=m.OwnedPostgres(self.data,self.sock,self.state,{})
  child=Mock(pid=779);child.poll.return_value=None
  def run(cmd,**kw):
   if 'status' in cmd:return self.result(3)
   if 'stop' in cmd:child.poll.return_value=0;return self.result(0)
   (self.data/'postmaster.pid').write_text('779\n');return self.result(0)
  with patch.object(m.subprocess,'run',side_effect=run),patch.object(m.subprocess,'Popen',return_value=child):
   try:next_cluster.start()
   finally:next_cluster.close()
  child.wait.assert_called_once()
if __name__=='__main__':unittest.main()
