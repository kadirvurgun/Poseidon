import json
import unittest
from unittest.mock import patch
import s3_gate


class OwnedS3GateTests(unittest.TestCase):
    def test_mutable_images_and_missing_commands_cannot_start_a_provider(self):
        with patch('s3_gate.docker') as docker:
            for image, command in [('minio/minio:latest', ['test']), ('sha256:' + 'a' * 64, [])]:
                with self.assertRaises(RuntimeError):
                    s3_gate.run(image, command)
            docker.assert_not_called()

    def test_binding_must_be_single_owned_loopback_and_cleanup_still_checks_identity(self):
        state = {'Id': 'owned-id', 'Name': '/owned-name', 'Config': {'Labels': {s3_gate.LABEL: 'owner'}}, 'NetworkSettings': {'Ports': {'9000/tcp': [{'HostIp': '0.0.0.0', 'HostPort': '9000'}]}}}
        with patch('s3_gate.docker', return_value=json.dumps([state])):
            with self.assertRaises(RuntimeError): s3_gate.owned('owned-id', 'owner', 'owned-name')
            self.assertIsNone(s3_gate.owned('owned-id', 'owner', 'owned-name', require_binding=False))
            with self.assertRaises(RuntimeError): s3_gate.owned('owned-id', 'different-owner', 'owned-name', require_binding=False)
        state['NetworkSettings']['Ports']['9000/tcp'][0]['HostIp'] = '127.0.0.1'
        with patch('s3_gate.docker', return_value=json.dumps([state])):
            self.assertEqual(9000, s3_gate.owned('owned-id', 'owner', 'owned-name'))

    def test_unlabelled_local_images_are_not_silently_treated_as_a_real_owned_provider(self):
        image = 'sha256:' + 'b' * 64
        with patch('s3_gate.docker', return_value=json.dumps([{'Id': image, 'Config': {'Labels': {}}}])) as docker:
            with self.assertRaises(RuntimeError): s3_gate.run(image, ['test'])
            self.assertEqual(1, docker.call_count)

