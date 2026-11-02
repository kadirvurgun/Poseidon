import json
import unittest
from unittest.mock import patch
import clamd_gate


class OwnedScannerTests(unittest.TestCase):
    def test_cleanup_requires_exact_id_name_and_label(self):
        state = {'id': 'a' * 64, 'name': 'poseidon-audit-files-owned', 'owner': 'owned'}
        record = {'Id': state['id'], 'Name': '/' + state['name'], 'Config': {'Labels': {clamd_gate.LABEL: 'owned'}}}
        with patch('clamd_gate.docker', side_effect=[json.dumps([record]), '']) as command:
            clamd_gate.cleanup(state)
            self.assertEqual(('rm', '--force', state['id']), command.call_args_list[1].args)
        for field, value in [('Id', 'unrelated'), ('Name', '/unrelated'), ('Config', {'Labels': {}})]:
            with self.subTest(field=field), patch('clamd_gate.docker', return_value=json.dumps([{**record, field: value}])) as command:
                with self.assertRaisesRegex(RuntimeError, 'ownership_mismatch'):
                    clamd_gate.cleanup(state)
                self.assertEqual(1, command.call_count)
