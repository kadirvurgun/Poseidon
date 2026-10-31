"""Explicit task/model routing. No credential access or silent expensive fallback."""
import json
from pathlib import Path


class ModelPolicyError(ValueError):
    pass


class ModelPolicy:
    def __init__(self, root):
        try:
            self.value = json.loads((Path(root) / 'automation/model-policy.json').read_text(encoding='utf-8'))
            if (self.value['schemaVersion'] != 1 or self.value['speed'] != 'standard'
                    or self.value['automaticEscalation'] is not False):
                raise ValueError()
            roles = self.value['roles']
            if set(roles) != {'routine', 'test-writing', 'independent_qa', 'development', 'security_qa'}:
                raise ValueError()
            for choice in roles.values():
                if (set(choice) != {'model', 'effort'}
                        or choice['model'] not in {'gpt-6-luna', 'gpt-6.1-sol'}
                        or choice['effort'] not in {'low', 'medium'}):
                    raise ValueError()
            tasks = self.value['taskRoles']
            if not isinstance(tasks, dict) or any(role not in roles for role in tasks.values()):
                raise ValueError()
            if (not isinstance(self.value['criticalReviewTasks'], list)
                    or any(task not in tasks for task in self.value['criticalReviewTasks'])):
                raise ValueError()
        except (OSError, ValueError, KeyError, TypeError, AttributeError):
            raise ModelPolicyError('invalid_or_missing_model_policy') from None

    def select(self, task_id, phase):
        if phase not in {'development', 'independent_qa'}:
            raise ModelPolicyError('unknown_model_phase')
        role = self.value['taskRoles'].get(task_id, 'development')
        if phase == 'independent_qa':
            # Unclassified/new tasks retain the stronger review until deliberately classified.
            role = ('security_qa' if task_id in self.value['criticalReviewTasks']
                    or task_id not in self.value['taskRoles'] else 'independent_qa')
        return {'role': role, **self.value['roles'][role]}

    @staticmethod
    def arguments(choice):
        return ['--model', choice['model'], '-c', 'model_reasoning_effort="' + choice['effort'] + '"',
                '-c', 'service_tier="default"']

    @staticmethod
    def advertised(home, choice):
        """Only read public model identifiers/capabilities, never auth or identity fields.

        An advertised model still needs a successful live invocation. Missing catalog
        means unknown; neither unknown nor unsupported authorizes a larger model.
        """
        try:
            models = json.loads((Path(home) / 'models_cache.json').read_text(encoding='utf-8'))['models']
            for model in models:
                if model.get('slug') == choice['model'] and model.get('visibility') == 'list':
                    return choice['effort'] in [item.get('effort') for item in
                                               model.get('supported_reasoning_levels', [])]
        except (OSError, ValueError, KeyError, TypeError, AttributeError):
            pass
        return False
