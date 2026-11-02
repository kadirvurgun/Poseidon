"""Synthetic setup tests only; never evidence of a real provider session."""
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import unittest
import json
from pathlib import Path
import tempfile
from unittest.mock import patch

import oidc_lab as lab
from oidc_lab_humans import catalog_membership, SOURCE


class HumanFixtureTests(unittest.TestCase):
    def test_private_fixture_update_failure_preserves_previous_document_and_cleans_only_owned_temporary(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(lab, 'PRIVATE', Path(directory)):
            path = Path(directory) / 'appsettings.json'
            path.write_text(json.dumps({'synthetic': 'previous'}))
            with patch.object(lab.os, 'replace', side_effect=OSError('Synthetic write fault')), self.assertRaises(OSError):
                lab.atomic_private_json(path, {'synthetic': 'changed'})
            self.assertEqual({'synthetic': 'previous'}, json.loads(path.read_text()))
            self.assertEqual(['appsettings.json'], [p.name for p in Path(directory).iterdir()])
            lab.atomic_private_json(path, {'synthetic': 'changed'})
            self.assertEqual({'synthetic': 'changed'}, json.loads(path.read_text()))

    def test_two_distinct_humans_get_only_explicit_catalog_scope_and_restart_preserves_appointments(self):
        realm, credentials, _, config = lab.make_configuration()
        original = deepcopy(config)
        now = datetime(2026, 11, 2, tzinfo=timezone.utc)
        candidate = catalog_membership(config, realm, lab.AUTHORITY, lab.PROJECT, now)
        self.assertEqual(original, config)
        self.assertEqual(original['Authentication']['Oidc']['Memberships'][1], candidate['Authentication']['Oidc']['Memberships'][1])
        reviewer = candidate['Authentication']['Oidc']['Memberships'][2]
        self.assertFalse(reviewer['TenantWideRead']); self.assertFalse(reviewer['TenantWideManage'])
        self.assertEqual(['lab-manager', 'lab-restricted', 'lab-reviewer'], list(credentials))
        for member in (candidate['Authentication']['Oidc']['Memberships'][0], reviewer):
            self.assertEqual('Human', member['ActorKind'])
            self.assertIn('crew.catalog.govern', member['Permissions'])
            self.assertNotIn('regulatory.qualify', member['Permissions'])
            self.assertNotIn('human.verify', member['Permissions'])
            self.assertEqual(SOURCE, member['CrewCatalogAuthorities'][0]['SourceReference'])
        repeated = catalog_membership(candidate, realm, lab.AUTHORITY, lab.PROJECT, now + timedelta(minutes=5))
        self.assertEqual(candidate, repeated)

    def test_missing_or_colliding_real_provider_identity_never_substitutes_restricted_actor(self):
        for fault in ('missing', 'same_actor', 'foreign_provider'):
            realm, _, _, config = lab.make_configuration()
            if fault == 'missing':
                realm['users'] = realm['users'][:2]
            elif fault == 'same_actor':
                config['Authentication']['Oidc']['Memberships'][2]['ActorId'] = config['Authentication']['Oidc']['Memberships'][0]['ActorId']
            else:
                config['Authentication']['Oidc']['Issuer'] = 'https://untrusted.example.invalid'
            before = deepcopy(config)
            with self.subTest(fault=fault), self.assertRaises(RuntimeError):
                catalog_membership(config, realm, lab.AUTHORITY, lab.PROJECT)
            self.assertEqual(before, config)

    def test_operator_authority_conflict_and_expiry_are_preserved_without_automatic_renewal(self):
        realm, _, _, config = lab.make_configuration()
        now = datetime(2026, 11, 2, tzinfo=timezone.utc)
        candidate = catalog_membership(config, realm, lab.AUTHORITY, lab.PROJECT, now)
        for fault in ('source', 'expiry'):
            changed = deepcopy(candidate)
            authority = changed['Authentication']['Oidc']['Memberships'][0]['CrewCatalogAuthorities'][0]
            if fault == 'source':
                authority['SourceReference'] = 'Operator preserved'
            else:
                authority['Until'] = (now - timedelta(seconds=1)).isoformat()
            before = deepcopy(changed)
            with self.subTest(fault=fault), self.assertRaises(RuntimeError):
                catalog_membership(changed, realm, lab.AUTHORITY, lab.PROJECT, now)
            self.assertEqual(before, changed)


if __name__ == '__main__':
    unittest.main()
