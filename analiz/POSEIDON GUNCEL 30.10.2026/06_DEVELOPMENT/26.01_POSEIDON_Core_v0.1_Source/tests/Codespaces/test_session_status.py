import contextlib,importlib.util,io,pathlib,sys,tempfile,unittest
from unittest.mock import patch
ROOT=pathlib.Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('session_status',ROOT/'scripts/uat-session-status.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class SessionStatusTests(unittest.TestCase):
 def test_fixed_codes_and_no_raw_data(self):
  for code in m.CODES:
   value='2026-10-29T20:04:01.123Z '+code+' AGE_SECONDS=240 REMAINING_SECONDS=960'
   self.assertEqual(m.classify('secret=PRIVATE\n      POSEIDON_UAT_SESSION '+value),value)
 def test_malformed_untrusted_and_unknown_values(self):
  line='POSEIDON_UAT_SESSION 2026-10-29T20:04:01.123Z TICKET_CREATED AGE_SECONDS=0 REMAINING_SECONDS=1200'
  for value in ['prefix '+line,line+' SECRET',line.replace('TICKET_CREATED','SECRET'),line.replace('-10-29','-99-29'),line.replace('AGE_SECONDS=0','AGE_SECONDS=99999')]:
   self.assertEqual(m.classify(value),'NO_SESSION_DIAGNOSTICS_IN_RETAINED_TAIL')
 def output(self,payload,symlink=False):
  with tempfile.TemporaryDirectory() as td:
   home=pathlib.Path(td);state=home/'.poseidon-synthetic-uat';state.mkdir(mode=0o700)
   if symlink:(home/'data').write_bytes(payload);(state/'app.log').symlink_to(home/'data')
   else:(state/'app.log').write_bytes(payload)
   with patch.object(m.pathlib.Path,'home',return_value=home),patch.object(sys,'argv',['uat-session-status.py']),contextlib.redirect_stdout(io.StringIO()) as out:
    code=m.main()
   return code,out.getvalue().strip()
 def test_tail_cut_and_partial_final_line_cannot_promote_record(self):
  line=b'POSEIDON_UAT_SESSION 2026-10-29T20:04:01.123Z TICKET_CREATED AGE_SECONDS=0 REMAINING_SECONDS=1200'
  self.assertEqual(self.output(line)[1],'NO_SESSION_DIAGNOSTICS_IN_RETAINED_TAIL')
  payload=b'prefix '+line+b'\n'+b'x'*(131072-len(line)-1)
  self.assertEqual(self.output(payload)[1],'NO_SESSION_DIAGNOSTICS_IN_RETAINED_TAIL')
  self.assertIn('TICKET_CREATED',self.output(line+b'\n')[1])
 def test_symlink_fails_without_echo(self):
  self.assertEqual(self.output(b'PRIVATE',True),(1,'SESSION_CHECK_UNAVAILABLE'))
 def test_output_is_bounded(self):
  line='POSEIDON_UAT_SESSION 2026-10-29T20:04:01.123Z TICKET_MISSING AGE_SECONDS=-1 REMAINING_SECONDS=-86401'
  self.assertEqual(len(m.classify('\n'.join([line]*100)).splitlines()),40)
if __name__=='__main__':unittest.main()
