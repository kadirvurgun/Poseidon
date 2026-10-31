import json
from pathlib import Path
import tempfile
import unittest

from model_policy import ModelPolicy, ModelPolicyError


class ModelPolicyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'automation').mkdir()
        self.path = self.root / 'automation/model-policy.json'
        source = Path(__file__).resolve().parents[1] / 'automation/model-policy.json'
        self.path.write_text(source.read_text(encoding='utf-8'), encoding='utf-8')

    def test_routine_uses_luna_and_security_review_uses_sol(self):
        policy = ModelPolicy(self.root)
        self.assertEqual('gpt-6-luna', policy.select('S0-ADR-01', 'development')['model'])
        self.assertEqual('gpt-6-luna', policy.select('UI-REQUIRED-DATA-01', 'independent_qa')['model'])
        self.assertEqual('gpt-6.1-sol', policy.select('S0-THREATS-01', 'independent_qa')['model'])
        self.assertEqual('gpt-6.1-sol', policy.select('UNCLASSIFIED', 'independent_qa')['model'])

    def test_invalid_policy_cannot_silently_upgrade(self):
        data = json.loads(self.path.read_text())
        data['roles']['routine']['model'] = 'gpt-6-astra'
        self.path.write_text(json.dumps(data))
        with self.assertRaises(ModelPolicyError):
            ModelPolicy(self.root)

    def test_explicit_standard_speed_model_and_effort(self):
        choice = ModelPolicy(self.root).select('S0-ADR-01', 'development')
        arguments = ModelPolicy.arguments(choice)
        self.assertEqual(['--model', 'gpt-6-luna'], arguments[:2])
        self.assertIn('model_reasoning_effort="medium"', arguments)
        self.assertIn('service_tier="default"', arguments)

    def test_catalog_rejects_missing_hidden_or_unsupported_without_fallback(self):
        choice = ModelPolicy(self.root).select('S0-ADR-01', 'development')
        self.assertFalse(ModelPolicy.advertised(self.root, choice))
        path = self.root / 'models_cache.json'
        model = {'slug': 'gpt-6-luna', 'visibility': 'hidden',
                 'supported_reasoning_levels': [{'effort': 'medium'}]}
        path.write_text(json.dumps({'models': [model]}))
        self.assertFalse(ModelPolicy.advertised(self.root, choice))
        model['visibility'] = 'list'
        path.write_text(json.dumps({'models': [model]}))
        self.assertTrue(ModelPolicy.advertised(self.root, choice))
        model['supported_reasoning_levels'] = [{'effort': 'high'}]
        path.write_text(json.dumps({'models': [model]}))
        self.assertFalse(ModelPolicy.advertised(self.root, choice))


if __name__ == '__main__':
    unittest.main()
