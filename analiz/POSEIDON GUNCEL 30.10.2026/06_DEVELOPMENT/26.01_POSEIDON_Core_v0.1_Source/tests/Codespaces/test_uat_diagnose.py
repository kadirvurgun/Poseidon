"""Read-only diagnostic contracts with isolated filesystem/socket and mocked SQL."""
import contextlib,importlib.util,io,json,os,pathlib,socket,subprocess,sys,tempfile,unittest
from unittest.mock import patch
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('diagnose_uat',ROOT/'scripts/uat-diagnose.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class DiagnoseTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup);self.home=pathlib.Path(self.tmp.name)
  self.state=self.home/'.poseidon-synthetic-uat';self.data=self.state/'pgdata';self.sock=self.state/'socket'
  for p in (self.state,self.data,self.sock):p.mkdir(mode=0o700)
  self.pid=self.data/'postmaster.pid';self.lock=self.sock/'.s.PGSQL.55433.lock'
  (self.state/'owner.json').write_text(json.dumps({'purpose':'poseidon-synthetic-uat-v1','repository':str(ROOT)}))
  value=f'123\n{self.data}\n1700000000\n55433\n{self.sock}\n';self.pid.write_text(value);self.lock.write_text(value)
  self.socket=socket.socket(socket.AF_UNIX);self.socket.bind(str(self.sock/'.s.PGSQL.55433'));self.addCleanup(self.socket.close)
  self.bin=self.home/'bin';self.bin.mkdir();self.postgres=self.bin/'postgres'
  for name in ('postgres','psql'):
   fd=os.open(self.bin/name,os.O_CREAT|os.O_EXCL|os.O_WRONLY,0o700);os.close(fd)
 def runcheck(self,sql=None,identity=None,sql_error=None,peer=None):
  sql=sql or json.dumps([str(self.data),str(self.sock),'55433',1700000000.25])
  with patch.object(m,'socket_peer',side_effect=peer or [(123,os.getuid()),(123,os.getuid())]),patch.object(m.pathlib.Path,'home',return_value=self.home),patch.object(m.shutil,'which',return_value=str(self.postgres)),patch.object(m,'process_identity',side_effect=identity or [(self.postgres,42),(self.postgres,42)]),patch.object(m.subprocess,'run',side_effect=sql_error,return_value=subprocess.CompletedProcess([],0,sql,'')) as action:
   try:result=m.diagnose()
   except Exception as error:
    self.reason=getattr(error,'reason',None);result='CHECK_INCONCLUSIVE'
  return result,action
 def test_match_uses_explicit_local_read_only_sql_and_no_ambient_configuration(self):
  with patch.dict(os.environ,{'PGHOST':'remote','PGSERVICE':'secret','PGPASSWORD':'secret','HTTP_PROXY':'remote'}):result,action=self.runcheck()
  self.assertEqual(result,'TASK_DB_IDENTITY_MATCH');args=action.call_args.args[0];kw=action.call_args.kwargs
  self.assertIn('-XAtw',args);self.assertIn('ON_ERROR_STOP=1',args);self.assertEqual(args[args.index('-h')+1],str(self.sock));self.assertEqual(args[args.index('-d')+1],'postgres');self.assertEqual(kw['timeout'],5)
  self.assertNotIn('secret',str(kw));self.assertNotIn('remote',str(kw));self.assertEqual(kw['env']['PGPASSFILE'],'/dev/null');self.assertIn('read_only=on',kw['env']['PGOPTIONS']);self.assertTrue(args[-1].startswith('SELECT json_build_array(current_setting'))
 def test_wrong_server_directory_socket_port_or_start_is_inconclusive(self):
  good=[str(self.data),str(self.sock),'55433',1700000000.25]
  for index,value in [(0,'/other'),(1,'/other'),(2,'5432'),(3,0),(3,'NaN')]:
   bad=good.copy();bad[index]=value;self.assertEqual(self.runcheck(json.dumps(bad))[0],'CHECK_INCONCLUSIVE')
 def test_malformed_or_oversized_pidfile_prevents_sql(self):
  for value in ['invalid','x'*9000]:
   self.pid.write_text(value);result,action=self.runcheck();self.assertEqual(result,'CHECK_INCONCLUSIVE');action.assert_not_called();self.assertEqual(self.reason,'PID_CHECK')
 def test_pidfile_symlink_prevents_sql(self):
  self.pid.unlink();self.pid.symlink_to(self.lock);result,action=self.runcheck();self.assertEqual(result,'CHECK_INCONCLUSIVE');action.assert_not_called();self.assertEqual(self.reason,'PID_CHECK')
 def test_wrong_marker_prevents_sql(self):
  (self.state/'owner.json').write_text('{}');result,action=self.runcheck();self.assertEqual(result,'CHECK_INCONCLUSIVE');action.assert_not_called();self.assertEqual(self.reason,'MARKER_CHECK')
 def test_socket_pid_mismatch_prevents_sql(self):
  self.lock.write_text(self.lock.read_text().replace('123\n','456\n',1));result,action=self.runcheck();self.assertEqual(result,'CHECK_INCONCLUSIVE');action.assert_not_called();self.assertEqual(self.reason,'SOCKET_PID_CHECK')
 def test_non_socket_prevents_sql(self):
  p=self.sock/'.s.PGSQL.55433';p.unlink();p.write_text('');result,action=self.runcheck();self.assertEqual(result,'CHECK_INCONCLUSIVE');action.assert_not_called();self.assertEqual(self.reason,'SOCKET_CHECK')
 def test_executable_mismatch_prevents_sql(self):
  result,action=self.runcheck(identity=[(pathlib.Path('/other'),42)]);self.assertEqual(result,'CHECK_INCONCLUSIVE');action.assert_not_called();self.assertEqual(self.reason,'EXECUTABLE_CHECK')
 def test_process_reuse_is_inconclusive(self):self.assertEqual(self.runcheck(identity=[(self.postgres,42),(self.postgres,43)])[0],'CHECK_INCONCLUSIVE')
 def test_namespace_translation_or_wrong_uid_is_inconclusive(self):
  for status in [f'NSpid:\t999 123\nUid:\t{os.getuid()} {os.getuid()} {os.getuid()} {os.getuid()}\n','NSpid:\t123\nUid:\t0 0 0 0\n']:
   with patch.object(pathlib.Path,'read_text',return_value=status):
    with self.assertRaises(m.Inconclusive):m.process_identity(123)
 def test_unavailable_tools_timeout_permission_or_process_disappearance_never_leak(self):
  for error in [FileNotFoundError('secret'),PermissionError('secret'),subprocess.TimeoutExpired(['secret'],5),ValueError('secret'),KeyboardInterrupt('secret')]:
   with patch.object(m,'diagnose',side_effect=error),patch.object(sys,'argv',['uat-diagnose.py']),contextlib.redirect_stdout(io.StringIO()) as out:
    self.assertEqual(m.main(),1);self.assertEqual(out.getvalue(),'CHECK_INCONCLUSIVE:'+('INTERRUPTED' if isinstance(error,KeyboardInterrupt) else 'INTERNAL_CHECK')+'\n')
 def test_missing_tool_prevents_sql(self):
  (self.bin/'psql').unlink();result,action=self.runcheck();self.assertEqual(result,'CHECK_INCONCLUSIVE');action.assert_not_called();self.assertEqual(self.reason,'EXECUTABLE_CHECK')
 def test_untrusted_permissions_prevent_sql(self):
  self.data.chmod(0o755)
  result,action=self.runcheck();self.assertEqual(result,'CHECK_INCONCLUSIVE');action.assert_not_called();self.assertEqual(self.reason,'PATH_CHECK')
 def test_state_symlink_prevents_sql(self):
  other=self.home/'original';self.state.rename(other);self.state.symlink_to(other,target_is_directory=True)
  result,action=self.runcheck();self.assertEqual(result,'CHECK_INCONCLUSIVE');action.assert_not_called();self.assertEqual(self.reason,'PATH_CHECK')
 def test_arguments_rejected_without_any_check(self):
  with patch.object(sys,'argv',['uat-diagnose.py','--stop']),patch.object(m,'diagnose') as call,contextlib.redirect_stdout(io.StringIO()) as out:
   self.assertEqual(m.main(),1);call.assert_not_called();self.assertEqual(out.getvalue(),'CHECK_INCONCLUSIVE:ARGUMENT_CHECK\n')
 def test_exact_server_mismatch_reasons(self):
  good=[str(self.data),str(self.sock),'55433',1700000000.25]
  for index,value,reason in [(0,'/other','DATA_DIRECTORY_CHECK'),(1,'/other','SQL_SOCKET_CHECK'),(2,'5432','SQL_SOCKET_CHECK'),(3,0,'START_TIME_CHECK'),(3,'NaN','SQL_RESULT_CHECK')]:
   bad=good.copy();bad[index]=value;self.runcheck(json.dumps(bad));self.assertEqual(self.reason,reason)
 def test_sql_connect_failures_classified_without_error_payload(self):
  for error in [subprocess.TimeoutExpired(['PRIVATE_ARG'],5),subprocess.CalledProcessError(2,['PRIVATE_ARG'],stderr='PRIVATE_SQL_ERROR'),PermissionError('PRIVATE_PATH')]:
   self.runcheck(sql_error=error);self.assertEqual(self.reason,'SQL_CONNECT')
 def test_bad_sql_result_and_disappearing_process(self):
  self.runcheck(sql='not json');self.assertEqual(self.reason,'SQL_RESULT_CHECK')
  self.runcheck(identity=[FileNotFoundError('private')]);self.assertEqual(self.reason,'PROCESS_CHECK')
  self.runcheck(identity=[(self.postgres,42),FileNotFoundError('private')]);self.assertEqual(self.reason,'SNAPSHOT_CHANGED')
 def test_namespace_uid_and_state_reasons(self):
  uid=str(os.getuid());good=f'NSpid: 123\nUid: {uid} {uid} {uid} {uid}\n'
  for status,reason in [('NSpid: 999 123\n','PROCESS_NAMESPACE_CHECK'),('NSpid: 123\nUid: other\n','PROCESS_UID_CHECK')]:
   with patch.object(pathlib.Path,'read_text',return_value=status):
    with self.assertRaises(m.Inconclusive) as raised:m.process_identity(123)
    self.assertEqual(raised.exception.reason,reason)
  with patch.object(pathlib.Path,'read_text',side_effect=[good,'123 (postgres) Z']):
   with self.assertRaises(m.Inconclusive) as raised:m.process_identity(123)
   self.assertEqual(raised.exception.reason,'PROCESS_STATE_CHECK')
 def test_only_allowlisted_output_for_every_reason(self):
  for reason in m.REASONS|{'PRIVATE_UNKNOWN_REASON'}:
   with patch.object(m,'diagnose',side_effect=m.Inconclusive(reason)),patch.object(sys,'argv',['uat-diagnose.py']),contextlib.redirect_stdout(io.StringIO()) as out:
    self.assertEqual(m.main(),1);self.assertEqual(out.getvalue(),'CHECK_INCONCLUSIVE:'+(reason if reason in m.REASONS else 'INTERNAL_CHECK')+'\n')

 def test_distinct_startup_phases_do_not_false_reject_same_kernel_peer(self):
  for stamp in [1700000000.25,1700000010,1700000300,1699999990]:
   self.assertEqual(self.runcheck(json.dumps([str(self.data),str(self.sock),'55433',stamp]))[0],'TASK_DB_IDENTITY_MATCH')
 def test_peer_pid_uid_mismatch_and_unavailable_fail_closed(self):
  for peer in [[(456,os.getuid())],[(123,os.getuid()+1)],[PermissionError('private')]]:
   result,action=self.runcheck(peer=peer);self.assertEqual(result,'CHECK_INCONCLUSIVE');self.assertEqual(self.reason,'SOCKET_PEER_CHECK');action.assert_not_called()
 def test_peer_change_after_sql_rejects_match(self):
  self.runcheck(peer=[(123,os.getuid()),(456,os.getuid())]);self.assertEqual(self.reason,'SNAPSHOT_CHANGED')
 def test_real_linux_peer_credentials_on_task_owned_socket(self):
  self.socket.listen(2)
  self.assertEqual(m.socket_peer(self.sock/'.s.PGSQL.55433'),(os.getpid(),os.getuid()))

if __name__=='__main__':unittest.main()
