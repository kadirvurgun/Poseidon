"""Preserve original failure without logging exception payloads; no real DB/processes."""
import contextlib,importlib.util,io,json,pathlib,signal,subprocess,tempfile,unittest
from unittest.mock import Mock,patch
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('uat_diagnostics',ROOT/'scripts/uat.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class StartupDiagnosticsTests(unittest.TestCase):
 def test_fixed_failure_classes_never_include_payloads(self):
  for error,expected in [(OSError(11,'secret payload'),'OS_ERROR_ERRNO_11'),(FileNotFoundError(2,'secret'),'OS_ERROR_ERRNO_2'),(subprocess.TimeoutExpired(['secret'],2),'SUBPROCESS_TIMEOUT'),(subprocess.CalledProcessError(1,['secret']),'SUBPROCESS_FAILED'),(KeyboardInterrupt('secret'),'INTERRUPTED'),(TimeoutError('secret'),'TIMEOUT'),(RuntimeError('secret'),'RUNTIME_ERROR')]:
   self.assertEqual(m.failure_class(error),expected)
 def exercise(self,queued):
  with tempfile.TemporaryDirectory() as directory:
   home=pathlib.Path(directory);state=home/'.poseidon-synthetic-uat';state.mkdir(mode=0o700)
   for name in ['pgdata','socket']:(state/name).mkdir(mode=0o700)
   (state/'owner.json').write_text(json.dumps({'purpose':'poseidon-synthetic-uat-v1','repository':str(ROOT)}))
   cluster=Mock();cluster.start.side_effect=OSError(11,'DO_NOT_PRINT_SECRET')
   def close():
    if queued:__import__('os').kill(__import__('os').getpid(),signal.SIGINT)
    raise RuntimeError('Cluster PID differs from owned child; cleanup refused.')
   cluster.close.side_effect=close
   out,err=io.StringIO(),io.StringIO()
   with patch.object(m.pathlib.Path,'home',return_value=home),patch.object(m,'OwnedPostgres',return_value=cluster),patch.object(m.subprocess,'run') as run,patch.object(m.subprocess,'Popen') as spawn,patch.object(m.subprocess,'check_output') as output,contextlib.redirect_stdout(out),contextlib.redirect_stderr(err):
    code=m.main(['--local-emulation','--skip-build'])
    run.assert_not_called();spawn.assert_not_called();output.assert_not_called()
   self.assertEqual(code,1);self.assertNotIn('Stopped.',out.getvalue());self.assertIn('Original failure: POSTGRES_START / OS_ERROR_ERRNO_11',err.getvalue());self.assertIn('Cluster PID differs',err.getvalue());self.assertNotIn('DO_NOT_PRINT_SECRET',err.getvalue())
 def test_start_failure_and_cleanup_mismatch_both_survive(self):self.exercise(False)
 def test_second_interrupt_cannot_hide_original_or_cleanup_failure(self):self.exercise(True)
if __name__=='__main__':unittest.main()
