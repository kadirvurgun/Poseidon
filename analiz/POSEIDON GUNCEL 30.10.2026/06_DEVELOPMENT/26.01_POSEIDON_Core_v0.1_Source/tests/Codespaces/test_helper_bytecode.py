"""Real helper process and git porcelain: no DB/runtime startup, no network."""
import importlib.util,marshal,os,pathlib,struct,subprocess,sys,tempfile,unittest
ROOT=pathlib.Path(__file__).resolve().parents[2]
class HelperBytecodeTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=pathlib.Path(self.temp.name);(self.root/'scripts').mkdir()
  for name in ['uat-stop-task-db.py','uat-diagnose.py']:(self.root/'scripts'/name).write_bytes((ROOT/'scripts'/name).read_bytes())
  (self.root/'.gitignore').write_bytes((ROOT/'.gitignore').read_bytes())
  self.env={'PATH':os.environ['PATH'],'HOME':str(self.root),'LANG':'C','GIT_CONFIG_NOSYSTEM':'1','GIT_CONFIG_GLOBAL':'/dev/null'}
  self.git('init','-q');self.git('add','.');self.git('-c','user.name=Synthetic Test','-c','user.email=synthetic@example.invalid','-c','commit.gpgsign=false','commit','-qm','Synthetic fixture')
 def git(self,*args):return subprocess.check_output(['git',*args],cwd=self.root,env=self.env,text=True)
 def guard(self):return not self.git('status','--porcelain').strip() # Exact launcher predicate.
 def helper(self):
  r=subprocess.run([sys.executable,'scripts/uat-stop-task-db.py'],cwd=self.root,env=self.env,capture_output=True,text=True,timeout=5)
  self.assertEqual(r.returncode,1);self.assertEqual(r.stdout,'STOP_REFUSED_ARGUMENTS\n');self.assertEqual(r.stderr,'')
 def test_actual_helper_import_leaves_source_clean_without_cache(self):
  self.helper();self.assertTrue(self.guard());self.assertFalse((self.root/'scripts/__pycache__').exists())
 def test_existing_conventional_cache_ignored_but_never_executed_or_deleted(self):
  source=self.root/'scripts/uat-diagnose.py';cache=pathlib.Path(importlib.util.cache_from_source(str(source)));cache.parent.mkdir()
  # Valid timestamp cache header for the real source, but deliberately hostile test code.
  code=compile("raise RuntimeError('CACHE_MUST_NOT_EXECUTE')",str(source),'exec')
  header=importlib.util.MAGIC_NUMBER+struct.pack('<III',0,int(source.stat().st_mtime),source.stat().st_size)
  payload=header+marshal.dumps(code);cache.write_bytes(payload)
  self.helper();self.assertTrue(self.guard());self.assertEqual(cache.read_bytes(),payload)
 def test_tracked_source_edits_remain_rejected(self):
  p=self.root/'scripts/uat-diagnose.py';p.write_text(p.read_text()+'\n# genuine local edit\n');self.helper();self.assertFalse(self.guard())
 def test_untracked_source_even_inside_cache_directory_remains_rejected(self):
  for name in ['scripts/new_source.py','scripts/__pycache__/meaningful.py','scripts/__pycache__/other.cpython-313.pyc']:
   p=self.root/name;p.parent.mkdir(exist_ok=True);p.write_text('synthetic');self.assertFalse(self.guard());p.unlink()
if __name__=='__main__':unittest.main()
