"""Cross-language audit ABI drift guard; definitions are not runtime coverage."""
import json
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]


class S1AuditContractTests(unittest.TestCase):
    def test_client_command_purposes_exactly_match_the_embedded_server_contract(self):
        """A new/renamed command must not silently disappear from audit rendering.

        The small client projection intentionally contains no server route metadata
        and requires no Vite access to repository-private directories.
        """
        server = json.loads((ROOT / 'contracts/security/s1-current-scope.v1.json').read_text(encoding='utf-8'))
        client = json.loads((ROOT / 'frontend/src/product/audit-command-vocabulary.json').read_text(encoding='utf-8'))
        expected = {}
        for operation in server['operations']:
            if operation['kind'] != 'CriticalWrite':
                continue
            for operation_id in ([v['operationId'] for v in operation['variants']] or [operation['operationId']]):
                self.assertNotIn(operation_id, expected)
                expected[operation_id] = operation['reasonCode']
        self.assertEqual('poseidon.s1-audit-vocabulary.v1', client['schemaVersion'])
        self.assertEqual({'schemaVersion', 'operationReasons', 'reasonCodes', 'scopeKinds'}, set(client))
        self.assertEqual(expected, client['operationReasons'])
        self.assertEqual(server['criticalOperations'], len(expected))
        self.assertEqual(server['reasonCodes'], client['reasonCodes'])
        self.assertEqual(server['scopeKinds'], client['scopeKinds'])


if __name__ == '__main__':
    unittest.main()
