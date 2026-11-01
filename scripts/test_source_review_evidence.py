import json
import tempfile
import unittest
from pathlib import Path

import classify_file_findings
import secret_scan
import source_review_evidence


class SafeSourceEvidenceTests(unittest.TestCase):
    def test_ast_keeps_assignment_reference_call_and_consumer_without_literals(self):
        literal = 'synthetic-' + 'withheld-value'
        text = ('import secrets\npassword = secrets.token_urlsafe(32)\n'
                'consumer(password)\nclient_secret = "' + literal + '"\n')
        target = next(secret_scan.findings(text, 'fixture.py'))
        evidence = source_review_evidence.context_evidence(text, target)
        encoded = json.dumps(evidence)
        for shape in ('Assign', 'Call', 'Attribute', 'Name', 'Load', 'Store', 'Constant'):
            self.assertIn(shape, encoded)
        self.assertIn('token_urlsafe', encoded)
        self.assertNotIn(literal, encoded)
        self.assertNotIn('consumer', encoded.replace('generation_consumer_provenance', ''))
        symbol = secret_scan.digest(b'password')
        occurrences = [x for x in evidence['symbol_occurrences'] if x.get('id', {}).get('symbol_sha256') == symbol]
        self.assertEqual({2, 3}, {x['lineno'] for x in occurrences})
        calls = [x for x in evidence['generation_consumer_provenance'] if x['kind'] == 'Call']
        self.assertTrue(any(x['lineno'] == 3 and x['func']['id']['symbol_sha256'] == secret_scan.digest(b'consumer') for x in calls))
        self.assertEqual(target['immutable_source_revision'], evidence['source']['immutable_source_revision'])

    def test_hostile_bare_identifiers_and_quoted_literals_never_render(self):
        marker = 'gh' + 'p_' + 'r' * 36
        for line in ('password = "' + marker + '"', 'password = ' + marker):
            result = source_review_evidence.syntax_tokens(line)
            self.assertNotIn(marker, json.dumps(result))
            self.assertIn('operator', json.dumps(result))
        literal = source_review_evidence.syntax_tokens('password = "withheld-literal"')
        reference = source_review_evidence.syntax_tokens('password = another_symbol')
        self.assertIn('string', [x['kind'] for x in literal['tokens']])
        self.assertEqual('identifier', reference['tokens'][-1]['kind'])

    def test_same_line_occurrences_and_distinct_contexts_are_source_bound(self):
        marker = 'gh' + 'p_' + 'x' * 36
        text = '<p>' + marker + '</p><p>' + marker + '</p>'
        target = next(secret_scan.findings(text, 'a.zip!document.xml'))
        evidence = source_review_evidence.context_evidence(text, target)
        self.assertEqual(2, len(evidence['occurrences']))
        self.assertEqual(2, len({x['occurrence'] for x in evidence['occurrences']}))
        changed = next(secret_scan.findings(text + ' ', 'a.zip!document.xml'))
        other = source_review_evidence.context_evidence(text + ' ', changed)
        self.assertNotEqual(secret_scan.object_hash(evidence), secret_scan.object_hash(other))
        self.assertIn('unresolved', evidence['ast_status'])

    def test_targeted_inventory_shares_context_and_binds_each_exact_proposal(self):
        marker = 'gh' + 'p_' + 'x' * 36
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); (root / 'docs/phases').mkdir(parents=True)
            (root / 'fixture.txt').write_text(marker + ' ' + marker, encoding='utf-8')
            archived = {'findings': [{'path': 'fixture.txt', 'rule': 'github-token', 'line': 1}]}
            (root / 'docs/phases/AUDIT-FILES-BUNDLE-01.evidence.json').write_text(json.dumps({
                'repository_scan': archived, 'final_changed_input_scan': archived}))
            ledger, evidence = classify_file_findings.inventory(root)
            self.assertEqual(2, len(ledger['entries']))
            self.assertEqual(1, len(evidence['contexts']))
            self.assertEqual(secret_scan.object_hash(ledger), evidence['pending_ledger_sha256'])
            for entry in ledger['entries']:
                proposal = {k: entry[k] for k in ('target', 'classification', 'rationale', 'evidence_sha256', 'author_reference')}
                self.assertEqual(secret_scan.object_hash(proposal), entry['proposal_sha256'])
                self.assertEqual(secret_scan.object_hash(evidence['contexts'][entry['context_evidence_id']]), entry['evidence_sha256'])
                self.assertEqual(2, len(entry['origins']))
                self.assertEqual('unresolved', entry['classification'])
            self.assertNotIn(marker, json.dumps(evidence))

    def test_required_ci_jobs_scan_repository_and_actual_postbuild_client_directly(self):
        # Static wiring regression: execution remains the coordinator combined gate.
        workflow = (Path(__file__).resolve().parents[1] / '.github/workflows/backend-foundation.yml').read_text()
        backend = workflow.split('  persistence-and-security:\n', 1)[1].split('\n  frontend-and-browser:', 1)[0]
        frontend = workflow.split('  frontend-and-browser:\n', 1)[1].split('\n  nightly-installer-construction:', 1)[0]
        for job in (backend, frontend):
            self.assertNotIn('needs:', job)
            self.assertNotIn('continue-on-error:', job)
        self.assertIn('run: python3 scripts/secret_scan.py --mode repository', backend)
        command = 'run: python3 scripts/secret_scan.py --mode client --bundle frontend/dist'
        self.assertIn(command, frontend)
        self.assertLess(frontend.index('npm run build --prefix frontend'), frontend.index(command))
        for job, command in ((backend, '--mode repository'), (frontend, '--mode client')):
            step = job[:job.index(command)].rsplit('      - name:', 1)[1]
            self.assertNotIn('if:', step)
            self.assertNotIn('--exceptions', job)
