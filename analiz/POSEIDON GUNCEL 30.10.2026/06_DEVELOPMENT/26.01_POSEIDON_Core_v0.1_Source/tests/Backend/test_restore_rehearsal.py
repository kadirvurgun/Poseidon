"""Offline safety regressions: no database/network operations."""
import importlib.util, pathlib, unittest
from unittest.mock import patch, Mock
spec=importlib.util.spec_from_file_location('rehearsal',pathlib.Path(__file__).with_name('restore-rehearsal.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
BASE='Host=127.0.0.1;Port=55432;Username=synthetic;Database=poseidon_test'
class SafetyTests(unittest.TestCase):
 def rejected(self,connection,extra=None):
  with patch.dict(m.os.environ,{'POSEIDON_TEST_POSTGRES':connection,**(extra or {})},clear=True),patch.object(m.subprocess,'run') as run,patch.object(m.subprocess,'Popen') as popen,patch.object(m.subprocess,'check_output') as check,patch.object(m.tempfile,'mkdtemp') as temp:
   with self.assertRaises((RuntimeError,OSError)):m.Rehearsal()
   for call in [run,popen,check,temp]:call.assert_not_called()
 def test_invalid_endpoints_before_any_action(self):
  for host in ['192.0.2.1','localhost','127.0.0.1,192.0.2.1','postgres://127.0.0.1','', '/nonexistent/socket']:
   with self.subTest(host=host):self.rejected(BASE.replace('127.0.0.1',host))
  for value in [BASE.replace('Host=127.0.0.1;',''),BASE.replace('Port=55432;',''),BASE+';Host=127.0.0.1',BASE.replace('55432','0'),BASE.replace('55432','65536')]:self.rejected(value)
 def test_conflicting_environment_before_any_action(self):
  for key,value in {'PGHOST':'192.0.2.1','PGPORT':'5432','PGHOSTADDR':'192.0.2.1','PGSERVICE':'remote','PGSERVICEFILE':'/tmp/service','PGOPTIONS':'-c search_path=bad','PGDATABASE':'other','PGUSER':'other','PGCLUSTER':'remote'}.items():
   with self.subTest(key=key):self.rejected(BASE,{key:value})
 def test_explicit_loopback_sanitized(self):
  for host in ['127.0.0.1','::1']:
   cfg,env,local=m.validated_connection(BASE.replace('127.0.0.1',host),{'PGHOSTADDR':'','PGSERVICE':'','PATH':'unchanged'})
   self.assertNotIn('PGHOSTADDR',env);self.assertNotIn('PGSERVICE',env);self.assertEqual(env['PGHOST'],host);self.assertEqual(local['transport'],'LITERAL_LOOPBACK')
 def test_socket_path_verification_before_actions(self):
  from types import SimpleNamespace
  import stat
  connection=BASE.replace('127.0.0.1','/verified/socket')
  def good(path):
   mode=stat.S_IFSOCK|0o777 if path.name.startswith('.s.PGSQL.') else stat.S_IFDIR|0o755
   return SimpleNamespace(st_mode=mode,st_uid=m.os.getuid())
  with patch.object(m.pathlib.Path,'lstat',good):
   self.assertEqual(m.validated_connection(connection,{})[2]['transport'],'LOCAL_UNIX_SOCKET')
  for mode in [stat.S_IFLNK|0o777,stat.S_IFDIR|0o777,stat.S_IFREG|0o600]:
   def bad(path):
    return SimpleNamespace(st_mode=mode,st_uid=m.os.getuid()) if str(path)=='/verified/socket' else good(path)
   with patch.object(m.pathlib.Path,'lstat',bad):self.rejected(connection)
 def test_remote_http_never_opens(self):
  with patch.object(m.urllib.request,'build_opener') as opener:
   for url in ['http://192.0.2.1:80/','http://localhost:80/','https://127.0.0.1:80/','http://user@127.0.0.1:80/','http://127.0.0.1/']:
    with self.assertRaises(RuntimeError):m.loopback_request(url)
   opener.assert_not_called()
 def test_proxy_overrides_disabled(self):
  with patch.dict(m.os.environ,{'HTTP_PROXY':'http://192.0.2.1:80','http_proxy':'http://192.0.2.1:80','ALL_PROXY':'http://192.0.2.2:80'},clear=True),patch.object(m.urllib.request,'build_opener') as build:
   m.loopback_request('http://127.0.0.1:12345/test')
   handlers=build.call_args.args
   self.assertEqual(handlers[0].proxies,{})
   self.assertIsInstance(handlers[1],m.NoRedirect)
 def test_redirect_never_follows(self):
  with patch.object(m.urllib.request,'urlopen') as send:
   for url in ['http://192.0.2.1/','http://127.0.0.1:9999/']:
    with self.assertRaises(RuntimeError):m.NoRedirect().redirect_request(Mock(),None,302,'Found',{},url)
   send.assert_not_called()
if __name__=='__main__':unittest.main()
