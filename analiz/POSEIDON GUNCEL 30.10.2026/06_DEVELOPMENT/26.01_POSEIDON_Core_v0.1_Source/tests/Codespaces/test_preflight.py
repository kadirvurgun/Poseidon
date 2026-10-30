import importlib.util, json, pathlib, unittest
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('preflight',ROOT/'scripts/codespaces-preflight.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class PreflightTests(unittest.TestCase):
 def setUp(self):
  self.env={'CODESPACES':'true','CODESPACE_NAME':'synthetic-example','GITHUB_REPOSITORY':'emgt41/Poseidon','GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN':'app.github.dev'}
  self.origin='https://synthetic-example-8080.app.github.dev'
 def result(self,**kw):return m.assess(kw.get('env',self.env),kw.get('origin',self.origin),kw.get('visibility','private'),kw.get('creator','emgt41'),kw.get('free_only',True))
 def test_matching_statements_do_not_unlock_runtime(self):
  r=self.result();self.assertEqual(r['configurationErrors'],[]);self.assertEqual(r['status'],'CONFIGURATION_VALID_UNVERIFIED');self.assertFalse(r['uatReady']);self.assertFalse(r['servicesStarted'])
 def test_unsafe_origins(self):
  for origin in ['http://synthetic-example-8080.app.github.dev',self.origin+'/',self.origin+'?x=1',self.origin+':443','https://evil.invalid','https://user@synthetic-example-8080.app.github.dev',self.origin+'.evil.invalid']:
   with self.subTest(origin=origin):self.assertIn('WRONG_EXTERNAL_ORIGIN',self.result(origin=origin)['configurationErrors'])
 def test_public_org_and_unknown_visibility_rejected(self):
  for value in ['public','org','', 'PRIVATE']:self.assertIn('VISIBILITY_NOT_CREATOR_PRIVATE',self.result(visibility=value)['configurationErrors'])
 def test_environment_repository_owner_and_free_checks(self):
  for key in self.env:
   env=self.env|{key:''};self.assertTrue(self.result(env=env)['configurationErrors'])
  self.assertIn('WRONG_CREATOR',self.result(creator='other')['configurationErrors'])
  self.assertIn('FREE_ONLY_NOT_CONFIRMED',self.result(free_only=False)['configurationErrors'])
 def test_secret_inheritance_is_not_consumed_or_returned(self):
  r=self.result(env=self.env|{'GITHUB_TOKEN':'never-echo-this','ConnectionStrings__Poseidon':'never-echo-this','HTTP_PROXY':'never-echo-this'})
  self.assertNotIn('never-echo-this',json.dumps(r));self.assertEqual(r,self.result())
 def test_passive_container_has_no_lifecycle_or_forwarding(self):
  c=json.loads((ROOT/'.devcontainer/devcontainer.json').read_text())
  for key in ['initializeCommand','onCreateCommand','updateContentCommand','postCreateCommand','postStartCommand','postAttachCommand','features','mounts','containerEnv','remoteEnv','runArgs','appPort']:self.assertNotIn(key,c)
  self.assertEqual(c['forwardPorts'],[8080]);self.assertEqual(c['otherPortsAttributes']['onAutoForward'],'ignore');self.assertFalse(c['customizations']['vscode']['settings']['remote.autoForwardPorts'])
if __name__=='__main__':unittest.main()
