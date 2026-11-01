"""Authored fixture boundary tests; no provider or browser execution. NOT RUN."""
from copy import deepcopy
import unittest
import uuid

import oidc_lab as lab


class NavigationFixtureTests(unittest.TestCase):
    def test_only_explicit_writer_enrollment_preserves_real_identity_and_other_members(self):
        realm, _, _, config = lab.make_configuration()
        original = deepcopy(config)
        after = lab.navigation_membership(config, realm)
        self.assertEqual(original, config)
        before_members = original['Authentication']['Oidc']['Memberships']
        members = after['Authentication']['Oidc']['Memberships']
        self.assertEqual(before_members[1:], members[1:])
        self.assertEqual(original['ConnectionStrings'], after['ConnectionStrings'])
        self.assertEqual(original['Authentication']['Oidc']['ClientSecret'], after['Authentication']['Oidc']['ClientSecret'])
        for field in ('Issuer', 'Subject', 'ActorId', 'TenantId', 'TenantWideRead', 'TenantWideManage', 'VesselGrants'):
            self.assertEqual(before_members[0][field], members[0][field])
        added = set(members[0]['Permissions']) - set(before_members[0]['Permissions'])
        self.assertEqual({'crew.read', 'equipment.read', 'maintenance.read', 'import.read', 'mode.tug', 'mode.cross', 'tug.read'}, added)

    def test_two_real_server_references_bind_labels_and_both_modes_idempotently(self):
        realm, _, _, config = lab.make_configuration()
        after = lab.navigation_membership(config, realm)
        self.assertEqual(after, lab.navigation_membership(after, realm))
        refs = after['Authentication']['Oidc']['Memberships'][0]['RoleReferences']
        self.assertEqual(2, len(set(refs)))
        self.assertTrue(all(str(uuid.UUID(ref)) == ref for ref in refs))
        labels = after['WorkspaceContext']['RoleCatalog']['Roles']
        mappings = after['WorkspaceNavigation']['Mappings']
        self.assertEqual(set(refs), {row['Reference'] for row in labels})
        self.assertEqual(set(refs), {row['Reference'] for row in mappings})
        self.assertTrue(all(row['Modes'] == ['Fleet', 'Ship', 'Tug'] for row in mappings))
        self.assertTrue(any('crew' in row['Keys'] for row in mappings))
        self.assertTrue(all('Href' not in row and 'Permissions' not in row for row in mappings))

    def test_unknown_or_duplicate_role_attribution_is_preserved_and_rejected(self):
        realm, _, _, config = lab.make_configuration()
        known = str(uuid.uuid5(uuid.NAMESPACE_URL, lab.PROJECT + '/synthetic-writer-assignment'))
        for refs in ([str(uuid.uuid4())], [known, known], [known.upper()], ['invalid'], [str(uuid.UUID(int=0))]):
            with self.subTest(refs_kind=len(refs)):
                candidate = deepcopy(config)
                candidate['Authentication']['Oidc']['Memberships'][0]['RoleReferences'] = refs
                original = deepcopy(candidate)
                with self.assertRaises(RuntimeError):
                    lab.navigation_membership(candidate, realm)
                self.assertEqual(original, candidate)

    def test_conflicting_operator_labels_and_mappings_are_not_overwritten(self):
        realm, _, _, config = lab.make_configuration()
        after = lab.navigation_membership(config, realm)
        for section, key, field, value in (
            ('WorkspaceContext', 'Roles', 'Tr', 'Operator-maintained label'),
            ('WorkspaceNavigation', 'Mappings', 'Keys', ['audit']),
        ):
            candidate = deepcopy(after)
            catalog = candidate[section]['RoleCatalog'] if section == 'WorkspaceContext' else candidate[section]
            catalog[key][0][field] = value
            original = deepcopy(candidate)
            with self.assertRaisesRegex(RuntimeError, 'operator_catalog_preserved'):
                lab.navigation_membership(candidate, realm)
            self.assertEqual(original, candidate)

    def test_wrong_tenant_scope_duplicate_identity_and_non_lab_realm_fail_before_mutation(self):
        realm, _, _, config = lab.make_configuration()
        for change in ('tenant', 'scope', 'duplicate', 'realm'):
            with self.subTest(change=change):
                candidate, current_realm = deepcopy(config), deepcopy(realm)
                members = candidate['Authentication']['Oidc']['Memberships']
                if change == 'tenant': members[0]['TenantId'] = str(uuid.uuid4())
                if change == 'scope': members[0]['TenantWideManage'] = False
                if change == 'duplicate': members.append(deepcopy(members[0]))
                if change == 'realm': current_realm['realm'] = 'production'
                original = deepcopy(candidate)
                with self.assertRaises(RuntimeError):
                    lab.navigation_membership(candidate, current_realm)
                self.assertEqual(original, candidate)

    def test_unrelated_operator_catalog_entries_remain_and_duplicate_local_entries_fail(self):
        realm, _, _, config = lab.make_configuration()
        after = lab.navigation_membership(config, realm)
        foreign = {'TenantId': str(uuid.uuid4()), 'Reference': str(uuid.uuid4()),
                   'Tr': 'KURGUSAL başka tenant', 'En': 'FICTIONAL other tenant'}
        after['WorkspaceContext']['RoleCatalog']['Roles'].append(foreign)
        again = lab.navigation_membership(after, realm)
        self.assertEqual(foreign, again['WorkspaceContext']['RoleCatalog']['Roles'][-1])
        after['WorkspaceNavigation']['Mappings'].append(deepcopy(after['WorkspaceNavigation']['Mappings'][0]))
        original = deepcopy(after)
        with self.assertRaisesRegex(RuntimeError, 'operator_catalog_preserved'):
            lab.navigation_membership(after, realm)
        self.assertEqual(original, after)

    def test_invalid_or_over_budget_configuration_cannot_partially_assign_roles(self):
        realm, _, _, config = lab.make_configuration()
        for version, rows in (('', []), ('bad\nversion', []), ('v1', [None]),
                              ('v1', [{}] * 4096)):
            with self.subTest(version_valid=version == 'v1', rows=len(rows)):
                candidate = deepcopy(config)
                candidate['WorkspaceNavigation'] = {'Version': version, 'Mappings': rows}
                original = deepcopy(candidate)
                with self.assertRaises(RuntimeError):
                    lab.navigation_membership(candidate, realm)
                self.assertEqual(original, candidate)


if __name__ == '__main__':
    unittest.main()
