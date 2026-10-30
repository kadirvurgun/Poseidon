import contextlib,importlib.util,io,pathlib,sys,tempfile,unittest
from unittest.mock import patch
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('login_status',ROOT/'scripts/uat-login-status.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class LoginStatusTests(unittest.TestCase):
 def test_latest_allowlisted_code_only(self):
  self.assertEqual(m.classify('secret cookie=PRIVATE\n      POSEIDON_UAT_REJECT LOGIN ORIGIN_NULL\n      POSEIDON_UAT_REJECT LOGIN ORIGIN_LOCAL_HTTP_8080\n'),'LOGIN_REJECT:ORIGIN_LOCAL_HTTP_8080')
 def test_unknown_or_embedded_payload_never_echoed(self):
  for text in ['POSEIDON_UAT_REJECT LOGIN SECRET_VALUE','prefix POSEIDON_UAT_REJECT LOGIN ORIGIN_NULL','POSEIDON_UAT_REJECT LOGIN ORIGIN_NULL PRIVATE_TOKEN','POSEIDON_UAT_REJECT OTHER_REQUEST ORIGIN_NULL']:
   self.assertEqual(m.classify(text),'NO_LOGIN_REJECTION_OBSERVED')
 def test_every_fixed_code(self):
  for code in ['PEER_NOT_LOOPBACK','HOST_NOT_ALLOWED','ORIGIN_MISSING','ORIGIN_NULL','ORIGIN_LOCAL_HTTP_8080','ORIGIN_LOCAL_HTTPS_8080','ORIGIN_MULTIPLE','ORIGIN_OTHER']:
   self.assertEqual(m.classify('      POSEIDON_UAT_REJECT LOGIN '+code),'LOGIN_REJECT:'+code)
 def bounded(self,payload):
  with tempfile.TemporaryDirectory() as td:
   home=pathlib.Path(td);state=home/'.poseidon-synthetic-uat';state.mkdir(mode=0o700);(state/'app.log').write_bytes(payload)
   with patch.object(m.pathlib.Path,'home',return_value=home),patch.object(sys,'argv',['uat-login-status.py']),contextlib.redirect_stdout(io.StringIO()) as out:
    self.assertEqual(m.main(),0)
   return out.getvalue().strip()
 def test_tail_cut_does_not_promote_nonmatching_line_suffix(self):
  code=b'POSEIDON_UAT_REJECT LOGIN ORIGIN_OTHER\n'
  tail=code+b'x'*(131072-len(code))
  payload=b'nonmatching-prefix '+tail
  self.assertEqual(m.classify(payload.decode()),'NO_LOGIN_REJECTION_OBSERVED')
  self.assertEqual(self.bounded(payload),'NO_LOGIN_REJECTION_OBSERVED')
 def test_exact_tail_boundary_is_conservative_but_later_complete_record_survives(self):
  first=b'POSEIDON_UAT_REJECT LOGIN ORIGIN_OTHER\n'
  later=b'POSEIDON_UAT_REJECT LOGIN ORIGIN_NULL\n'
  padding=b'x'*(131072-len(first)-len(later)-1)+b'\n'
  self.assertEqual(self.bounded(b'prefix\n'+first+padding+later),'LOGIN_REJECT:ORIGIN_NULL')
 def test_zero_offset_keeps_first_record_and_cut_without_newline_is_empty(self):
  self.assertEqual(self.bounded(b'POSEIDON_UAT_REJECT LOGIN ORIGIN_NULL\n'),'LOGIN_REJECT:ORIGIN_NULL')
  self.assertEqual(self.bounded(b'x'*131073),'NO_LOGIN_REJECTION_OBSERVED')

if __name__=='__main__':unittest.main()
