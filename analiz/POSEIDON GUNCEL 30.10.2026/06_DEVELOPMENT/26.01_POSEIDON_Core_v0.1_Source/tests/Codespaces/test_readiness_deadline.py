"""Transient probe timeout must not abort a still-starting owned postmaster."""
import importlib.util,json,pathlib,subprocess,tempfile,unittest
from unittest.mock import Mock,patch
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('uat_readiness',ROOT/'scripts/uat.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class ReadinessTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup);self.state=pathlib.Path(self.tmp.name);self.data=self.state/'pgdata';self.sock=self.state/'socket'
  self.data.mkdir(mode=0o700);self.sock.mkdir(mode=0o700);(self.state/'owner.json').write_text(json.dumps({'purpose':'poseidon-synthetic-uat-v1','repository':str(ROOT)}))
  self.c=m.OwnedPostgres(self.data,self.sock,self.state,{});self.c.identity=self.c.verify();self.c.process=Mock(pid=777);self.c.process.poll.return_value=None
  (self.data/'postmaster.pid').write_text('777\n')
 def test_transient_probe_timeout_then_owned_ready_succeeds(self):
  with patch.object(m.subprocess,'run',side_effect=[subprocess.TimeoutExpired('pg_isready',2),subprocess.CompletedProcess([],0)]) as call,patch.object(m.time,'sleep'):
   self.c.wait_ready()
  self.assertEqual(call.call_count,2)
  for args in call.call_args_list:
   self.assertIn('-t',args.args[0]);self.assertEqual(args.args[0][args.args[0].index('-t')+1],'1');self.assertLessEqual(args.kwargs['timeout'],2)
  self.c.process.send_signal.assert_not_called()
 def test_total_deadline_bounds_repeated_probe_timeouts(self):
  with patch.object(m.subprocess,'run',side_effect=subprocess.TimeoutExpired('pg_isready',2)) as call,patch.object(m.time,'monotonic',side_effect=[0,0,0,31,31]),patch.object(m.time,'sleep') as sleep:
   with self.assertRaisesRegex(TimeoutError,'bounded startup deadline'):self.c.wait_ready()
   call.assert_called_once();sleep.assert_not_called()
 def test_ready_other_pid_is_not_adopted(self):
  (self.data/'postmaster.pid').write_text('888\n')
  with patch.object(m.subprocess,'run',return_value=subprocess.CompletedProcess([],0)):
   with self.assertRaisesRegex(RuntimeError,'identity mismatch'):self.c.wait_ready()
  self.c.process.send_signal.assert_not_called()
 def test_exited_child_or_invalid_probe_fails_closed(self):
  self.c.process.poll.return_value=1
  with patch.object(m.subprocess,'run') as call:
   with self.assertRaisesRegex(RuntimeError,'exited'):self.c.wait_ready()
   call.assert_not_called()
  self.c.process.poll.return_value=None
  with patch.object(m.subprocess,'run',return_value=subprocess.CompletedProcess([],3)):
   with self.assertRaisesRegex(RuntimeError,'readiness check failed'):self.c.wait_ready()
if __name__=='__main__':unittest.main()
