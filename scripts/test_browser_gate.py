import json
import unittest
from unittest.mock import patch
import browser_gate


class BrowserDatabaseOwnershipTests(unittest.TestCase):
    def test_cleanup_cannot_remove_an_unrelated_database(self):
        record = {'Id': 'owned-id', 'Name': '/owned-name', 'Config': {'Labels': {browser_gate.LABEL: 'owned-label'}}}
        with patch('browser_gate.docker', side_effect=[json.dumps([record]), '']) as command:
            browser_gate.cleanup('owned-id', 'owned-name', 'owned-label')
            self.assertEqual(('rm', '--force', 'owned-id'), command.call_args_list[1].args)
        for args in [('other-id', 'owned-name', 'owned-label'), ('owned-id', 'other-name', 'owned-label'), ('owned-id', 'owned-name', 'other-label')]:
            with self.subTest(args=args), patch('browser_gate.docker', return_value=json.dumps([record])) as command:
                with self.assertRaisesRegex(RuntimeError, 'ownership_mismatch'):
                    browser_gate.cleanup(*args)
                self.assertEqual(1, command.call_count)
