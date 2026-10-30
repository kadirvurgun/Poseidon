"""Real flock on isolated temporary files; fake cluster, no DB/network/process control."""
import contextlib,fcntl,importlib.util,io,json,pathlib,tempfile,types,unittest
from unittest.mock import Mock,patch
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('uat_lock',ROOT/'scripts/uat.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class SupervisorLockTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
  self.home=pathlib.Path(self.temp.name);self.state=self.home/'.poseidon-synthetic-uat';self.state.mkdir(mode=0o700)
  for name in ['pgdata','socket']:(self.state/name).mkdir(mode=0o700)
  (self.state/'owner.json').write_text(json.dumps({'purpose':'poseidon-synthetic-uat-v1','repository':str(ROOT)}))
 def locked(self):
  with (self.state/'supervisor.lock').open('a') as probe:
   try:fcntl.flock(probe,fcntl.LOCK_EX|fcntl.LOCK_NB)
   except BlockingIOError:return True
   fcntl.flock(probe,fcntl.LOCK_UN);return False
 def exercise(self,error,close_error=None,cli=False):
  cluster=Mock();cluster.start.side_effect=error
  def close():
   self.assertTrue(self.locked(), 'Lock must cover cleanup')
   if close_error:raise close_error
  cluster.close.side_effect=close
  with patch.object(m.pathlib.Path,'home',return_value=self.home),patch.object(m,'OwnedPostgres',return_value=cluster),patch.object(m.subprocess,'run') as run,patch.object(m.subprocess,'Popen') as spawn,patch.object(m.subprocess,'check_output') as output:
   if cli:
    with contextlib.redirect_stdout(io.StringIO()) as out:code=m.main(['--local-emulation','--skip-build'])
    self.assertEqual(code,0);self.assertIn('Stopped.',out.getvalue())
   else:
    try:m.run(types.SimpleNamespace(skip_build=True,local_emulation=True))
    except BaseException as e:self.retained=e # Keep traceback alive: release cannot depend on GC.
   run.assert_not_called();spawn.assert_not_called();output.assert_not_called()
  return cluster
 def test_interrupt_releases_before_stopped_and_allows_restart(self):
  error=KeyboardInterrupt('synthetic interrupt');self.exercise(error,cli=True)
  self.assertFalse(self.locked());self.exercise(KeyboardInterrupt('restart'),cli=True);self.assertFalse(self.locked())
 def test_start_failure_releases_despite_retained_traceback(self):
  self.exercise(RuntimeError('start failed'));self.assertFalse(self.locked())
 def test_cleanup_failure_releases_despite_retained_traceback(self):
  self.exercise(KeyboardInterrupt(),RuntimeError('cleanup failed'));self.assertFalse(self.locked())
 def test_existing_lock_refuses_before_cluster_start(self):
  with (self.state/'supervisor.lock').open('a') as owner:
   fcntl.flock(owner,fcntl.LOCK_EX|fcntl.LOCK_NB)
   with patch.object(m.pathlib.Path,'home',return_value=self.home),patch.object(m,'OwnedPostgres') as cluster:
    with self.assertRaisesRegex(RuntimeError,'already held'):
     m.run(types.SimpleNamespace(skip_build=True,local_emulation=True))
    cluster.assert_not_called()
   self.assertTrue(self.locked())
  self.assertFalse(self.locked())
if __name__=='__main__':unittest.main()
