"""Actual run()/CLI, queued POSIX signals, fake cluster; no subprocess/network."""
import contextlib,importlib.util,io,json,os,pathlib,signal,tempfile,unittest
from unittest.mock import Mock,patch
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('uat_signals',ROOT/'scripts/uat.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class ShutdownSignalsTests(unittest.TestCase):
 def exercise(self,fail,queued):
  with tempfile.TemporaryDirectory() as directory:
   home=pathlib.Path(directory);state=home/'.poseidon-synthetic-uat';state.mkdir(mode=0o700)
   (state/'pgdata').mkdir(mode=0o700);(state/'socket').mkdir(mode=0o700)
   (state/'owner.json').write_text(json.dumps({'purpose':'poseidon-synthetic-uat-v1','repository':str(ROOT)}))
   cluster=Mock();cluster.start.side_effect=KeyboardInterrupt('first Ctrl+C')
   def close():
    if queued:os.kill(os.getpid(),queued) # queued until cleanup restores its signal mask
    if fail:raise RuntimeError('Owned PostgreSQL did not stop; test cleanup timeout')
   cluster.close.side_effect=close
   stdout,stderr=io.StringIO(),io.StringIO()
   old=signal.getsignal(signal.SIGINT);signal.signal(signal.SIGINT,signal.default_int_handler)
   try:
    with patch.object(m.pathlib.Path,'home',return_value=home),patch.object(m,'OwnedPostgres',return_value=cluster),patch.object(m.subprocess,'run') as run,patch.object(m.subprocess,'Popen') as spawn,patch.object(m.subprocess,'check_output') as output,contextlib.redirect_stdout(stdout),contextlib.redirect_stderr(stderr):
     code=m.main(['--local-emulation','--skip-build'])
     run.assert_not_called();spawn.assert_not_called();output.assert_not_called();cluster.close.assert_called_once()
   finally:signal.signal(signal.SIGINT,old)
   return code,stdout.getvalue(),stderr.getvalue()
 def test_queued_second_sigint_preserves_cleanup_timeout_and_nonzero_cli(self):
  code,out,err=self.exercise(True,signal.SIGINT)
  self.assertEqual(code,1);self.assertNotIn('Stopped.',out);self.assertIn('Cleanup failed:',err);self.assertIn('did not stop',err)
 def test_queued_sigterm_preserves_cleanup_timeout_and_nonzero_cli(self):
  code,out,err=self.exercise(True,signal.SIGTERM)
  self.assertEqual(code,1);self.assertNotIn('Stopped.',out);self.assertIn('Cleanup failed:',err)
 def test_ordinary_successful_stop(self):
  code,out,err=self.exercise(False,None)
  self.assertEqual(code,0);self.assertIn('Stopped.',out);self.assertEqual(err,'')
 def test_second_sigint_after_successful_cleanup_is_still_successful_stop(self):
  code,out,err=self.exercise(False,signal.SIGINT)
  self.assertEqual(code,0);self.assertIn('Stopped.',out);self.assertEqual(err,'')
 def test_failure_without_second_signal_is_nonzero(self):
  code,out,err=self.exercise(True,None)
  self.assertEqual(code,1);self.assertNotIn('Stopped.',out);self.assertIn('Cleanup failed:',err)
if __name__=='__main__':unittest.main()
