"""DEV-117 / DEV-118: roadmap guard over the real documents and synthetic violations."""
import copy
import json
from pathlib import Path
import tempfile
import unittest

from validate_roadmaps import DISCLAIMER, DIRECTORY, ROOT, claims, validate, validate_document


class ValidateRoadmapsTests(unittest.TestCase):
    def setUp(self):
        self.data = json.loads((DIRECTORY / 'pms-type-approval-roadmap.json').read_text(encoding='utf-8'))
        self.markdown = (DIRECTORY / 'pms-type-approval-roadmap.md').read_text(encoding='utf-8')

    def test_committed_roadmaps_are_valid_drafts_without_claims(self):
        result = validate()
        self.assertEqual({'DEV-117': [], 'DEV-118': []}, result['errors'])
        for stem in ('pms-type-approval-roadmap', 'iso27001-27701-readiness-roadmap'):
            data = json.loads((DIRECTORY / f'{stem}.json').read_text(encoding='utf-8'))
            self.assertEqual(('DRAFT', None, None), (data['state'], data['approval']['approvedBy'], data['approval']['approvedAt']))
            self.assertTrue(all(g['ownerRole'] for g in data['gaps']))

    def test_claim_language_is_refused(self):
        for text in ('POSEIDON is DNV type approved', 'PMS approved by class', 'ISO 27001 certified platform', 'Ürün sertifikalıdır',
                     'Biz sertifikalıyız', 'fully compliant with ISO 27701'):
            self.assertTrue(claims(text), text)
        self.assertEqual([], claims(DISCLAIMER + ' Certificate only after formal audit; certification roadmap.'))
        errors = validate_document(self.data, self.markdown + '\nPOSEIDON PMS is type-approved by DNV.', ROOT)
        self.assertIn('claim_language:type-approved', errors)

    def test_every_gap_needs_an_owner_a_real_evidence_path_and_a_valid_date(self):
        data = copy.deepcopy(self.data)
        data['gaps'][0]['ownerRole'] = ' '
        data['gaps'][1]['evidence'] = ['backend/does-not-exist.cs']
        data['gaps'][2]['targetDate'] = 'next quarter'
        data['gaps'][3]['evidence'] = []
        errors = validate_document(data, self.markdown, ROOT)
        self.assertIn('PMS-GAP-01:owner_missing', errors)
        self.assertIn('PMS-GAP-02:evidence_unresolved', errors)
        self.assertIn('PMS-GAP-03:target_date', errors)
        self.assertIn('PMS-GAP-04:evidence_missing', errors)

    def test_approval_is_a_recorded_human_field_and_the_disclaimer_is_mandatory(self):
        data = copy.deepcopy(self.data); data['state'] = 'APPROVED'
        self.assertIn('approval_unrecorded', validate_document(data, self.markdown, ROOT))
        data = copy.deepcopy(self.data); data['approval']['approvedBy'] = 'someone'
        self.assertIn('approval_state_mismatch', validate_document(data, self.markdown, ROOT))
        data = copy.deepcopy(self.data); data['disclaimer'] = 'plan'
        errors = validate_document(data, self.markdown.replace(DISCLAIMER, ''), ROOT)
        self.assertIn('json_disclaimer_missing', errors); self.assertIn('markdown_disclaimer_missing', errors)
        data = copy.deepcopy(self.data); data['gaps'][0]['status'] = 'CLOSED_HUMAN_VERIFIED'
        self.assertIn('PMS-GAP-01:closure_unrecorded', validate_document(data, self.markdown, ROOT))

    def test_missing_files_are_reported(self):
        with tempfile.TemporaryDirectory() as empty:
            self.assertEqual({'DEV-117': ['unreadable'], 'DEV-118': ['unreadable']}, validate(Path(empty))['errors'])


if __name__ == '__main__':
    unittest.main()
