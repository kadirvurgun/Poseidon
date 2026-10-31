import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

from model_policy import ModelPolicy, ModelPolicyError
from nightly_coordinator import Blocked, Coordinator, SubscriptionWorkers, review_links
from codex_accounts import Workers
from test_nightly_coordinator import FakeWorkers, metadata
from codex_accounts import exclusive


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

    def test_dispatch_receipt_pins_the_actual_model_and_unknown_access_spends_no_call(self):
        workers = SubscriptionWorkers.__new__(SubscriptionWorkers)
        workers.root = self.root
        workers.home_root = self.root / 'profiles'
        workers.profiles = {'esra-pro': {}}
        home = workers.home('esra-pro')
        home.mkdir(parents=True)
        coordinator = Coordinator.__new__(Coordinator)
        coordinator.root, coordinator.workers = self.root, workers
        coordinator.budget, coordinator.save = Mock(), Mock()
        coordinator.wall = lambda: 1000
        coordinator.state, coordinator.job = {'model_calls': 0}, {'id': 'S0-ADR-01'}
        with self.assertRaisesRegex(Blocked, 'model_not_advertised'):
            coordinator.call_receipt('esra-pro', 'development')
        self.assertEqual(coordinator.state['model_calls'], 0)
        self.assertNotIn('calls', coordinator.job)
        (home / 'models_cache.json').write_text(json.dumps({'models': [{
            'slug': 'gpt-6-luna', 'visibility': 'list', 'supported_reasoning_levels': [{'effort': 'medium'}]}]}))
        coordinator.call_receipt('esra-pro', 'development')
        self.assertEqual(coordinator.job['calls'][0]['model'], 'gpt-6-luna')
        self.assertEqual(coordinator.job['calls'][0]['effort'], 'medium')
        with patch.object(Workers, 'command', return_value=['fake-codex']):
            command = workers.command()
        self.assertIn('--model', command)
        self.assertIn('gpt-6-luna', command)

    def test_review_links_covers_unchanged_targets_and_rejects_outside_paths(self):
        docs = self.root / 'docs'
        docs.mkdir()
        (docs / 'unchanged.json').write_text('{}')
        (docs / 'task.md').write_text('[existing](unchanged.json) [missing](missing.json) [outside](../../outside.md)')
        links = review_links(self.root, ['docs/task.md'])
        self.assertEqual([True, False, False], [item['exists'] for item in links])
        self.assertFalse(links[-1]['withinWorktree'])

    def test_esra_first_even_for_qa_and_fallback_only_on_confirmed_quota(self):
        workers = FakeWorkers(self.root)
        workers.config['routing_policy'] = 'esra_until_quota'
        coordinator = Coordinator(self.root, workers=workers)
        with coordinator.account('kadir-plus') as selected:
            self.assertEqual(selected, 'esra-pro')
        with exclusive(workers.home('esra-pro') / 'worker.lock'):
            with coordinator.account('kadir-plus') as selected:
                self.assertIsNone(selected)
        workers.status['esra-pro']['windows'][0]['usedPercent'] = 85
        workers.status['esra-pro']['ready'] = False
        with coordinator.account('esra-pro') as selected:
            self.assertEqual(selected, 'kadir-plus')
        workers.status['esra-pro']['limits_known'] = False
        with coordinator.account('kadir-plus') as selected:
            self.assertIsNone(selected)


if __name__ == '__main__':
    unittest.main()
