import importlib.util,pathlib,unittest
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('uat',ROOT/'scripts/uat.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class SupervisorTests(unittest.TestCase):
 def test_no_secret_or_configuration_inheritance(self):
  env=m.clean_env({'PATH':'/usr/bin','HOME':'/synthetic','GITHUB_TOKEN':'secret','GH_TOKEN':'secret','ConnectionStrings__Poseidon':'secret','PGHOST':'remote','VITE_ENABLE_DEV_SESSION':'true','HTTP_PROXY':'remote','LD_PRELOAD':'evil','NODE_OPTIONS':'evil','NPM_CONFIG_REGISTRY':'evil'})
  self.assertNotIn('secret',str(env));self.assertNotIn('remote',str(env));self.assertNotIn('evil',str(env));self.assertEqual(env['NPM_CONFIG_USERCONFIG'],str(ROOT/'.devcontainer/npm-user.npmrc'));self.assertEqual(env['NPM_CONFIG_GLOBALCONFIG'],str(ROOT/'.devcontainer/npm-global.npmrc'));self.assertNotEqual(env['NPM_CONFIG_USERCONFIG'],env['NPM_CONFIG_GLOBALCONFIG'])
 def test_exact_private_port_and_owner(self):
  metadata={'name':'synthetic','owner':{'login':'emgt41'},'repository':{'full_name':'emgt41/Poseidon','private':True}}
  ports=[{'sourcePort':8080,'visibility':'private','browseUrl':'https://synthetic-8080.app.github.dev'}]
  m.verify_visibility(metadata,ports,'synthetic','https://synthetic-8080.app.github.dev')
  for visibility in ['public','org','',None]:
   with self.assertRaises(RuntimeError):m.verify_visibility(metadata,[ports[0]|{'visibility':visibility}],'synthetic','https://synthetic-8080.app.github.dev')
  for p in [[],ports+ports,[ports[0]|{'sourcePort':5432}],[ports[0]|{'browseUrl':'https://evil.invalid'}]]:
   with self.assertRaises(RuntimeError):m.verify_visibility(metadata,p,'synthetic','https://synthetic-8080.app.github.dev')
  for d in [metadata|{'owner':{'login':'other'}},metadata|{'repository':{'full_name':'emgt41/Poseidon','private':False}},metadata|{'name':'other'}]:
   with self.assertRaises(RuntimeError):m.verify_visibility(d,ports,'synthetic','https://synthetic-8080.app.github.dev')
if __name__=='__main__':unittest.main()
