"""Real installed npm; network-free fixture installs, no credentials or external downloads."""
import importlib.util,json,os,pathlib,shutil,subprocess,tempfile,unittest
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('uat_npm',ROOT/'scripts/uat.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
@unittest.skipUnless(shutil.which('npm'),'Installed official npm required; no automatic install')
class NpmConfigurationTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory(prefix='poseidon-npm-config-');self.root=pathlib.Path(self.temp.name)
  self.home=self.root/'home';self.home.mkdir();self.project=self.root/'project';self.project.mkdir()
  # Poison only synthetic fixture config: never read/modify the user's actual npmrc.
  (self.home/'.npmrc').write_text('strict-ssl=false\naudit=false\nignore-scripts=false\nregistry=https://must-not-contact.invalid/\n')
  poison=self.root/'inherited-global.npmrc';poison.write_text('strict-ssl=false\naudit=false\n')
  self.env=m.clean_env({'PATH':os.environ['PATH'],'HOME':str(self.home),'NPM_CONFIG_USERCONFIG':str(self.home/'.npmrc'),'NPM_CONFIG_GLOBALCONFIG':str(poison),'NPM_CONFIG_STRICT_SSL':'false','NPM_CONFIG_AUDIT':'false','GITHUB_TOKEN':'synthetic-never-export'})
 def tearDown(self):self.temp.cleanup()
 def npm(self,*args,env=None):
  return subprocess.run(['npm',*args],env=env or self.env,cwd=self.project,capture_output=True,text=True,timeout=60)
 def test_baseline_duplicate_path_reproduces_owner_error(self):
  result=self.npm('config','get','strict-ssl',env=self.env|{'NPM_CONFIG_USERCONFIG':'/dev/null','NPM_CONFIG_GLOBALCONFIG':'/dev/null'})
  self.assertNotEqual(result.returncode,0);self.assertIn('double-loading config',result.stderr)
 def test_actual_resolution_ignores_poisoned_inherited_configs(self):
  expected={'strict-ssl':'true','audit':'true','registry':'https://registry.npmjs.org/','userconfig':self.env['NPM_CONFIG_USERCONFIG'],'globalconfig':self.env['NPM_CONFIG_GLOBALCONFIG']}
  for key,value in expected.items():
   with self.subTest(key=key):
    result=self.npm('config','get',key);self.assertEqual(result.returncode,0,result.stderr);self.assertEqual(result.stdout.strip(),value)
  self.assertNotEqual(expected['userconfig'],expected['globalconfig'])
 def test_actual_ci_installs_fixture_without_lifecycle_or_network(self):
  dep=self.project/'dep';dep.mkdir()
  (dep/'package.json').write_text(json.dumps({'name':'poseidon-synthetic-config-fixture','version':'1.0.0','scripts':{'install':'node -e "require(\'fs\').writeFileSync(\'unexpected-script\',\'bad\')"'}}))
  (self.project/'package.json').write_text(json.dumps({'name':'poseidon-synthetic-ci','version':'1.0.0','dependencies':{'poseidon-synthetic-config-fixture':'file:./dep'},'scripts':{'postinstall':'node -e "require(\'fs\').writeFileSync(\'unexpected-script\',\'bad\')"'}}))
  lock=self.npm('install','--package-lock-only','--ignore-scripts','--offline');self.assertEqual(lock.returncode,0,lock.stderr)
  result=self.npm('ci','--ignore-scripts','--offline');self.assertEqual(result.returncode,0,result.stderr)
  self.assertTrue((self.project/'node_modules/poseidon-synthetic-config-fixture/package.json').is_file())
  self.assertFalse(list(self.project.rglob('unexpected-script')))
if __name__=='__main__':unittest.main()
