"""USER-GUIDE-01 UG-2 lab screenshot enrollment contract tests (no provider/browser execution)."""
import json
import unittest

import help_screenshots_lab as shots
import oidc_lab as lab


class ScreenshotEnrollmentTests(unittest.TestCase):
    def test_enrollment_adds_only_read_permissions_idempotently_and_preserves_others(self):
        realm, _, _, config = lab.make_configuration()
        original = json.loads(json.dumps(config))
        once = shots.screenshot_membership(config, realm)
        twice = shots.screenshot_membership(once, realm)
        self.assertEqual(config, original)  # Pure: the input configuration is never mutated.
        self.assertEqual(once, twice)
        manager = once['Authentication']['Oidc']['Memberships'][0]
        added = set(manager['Permissions']) - set(original['Authentication']['Oidc']['Memberships'][0]['Permissions'])
        self.assertTrue(added <= set(shots.SCREENSHOT_PERMISSIONS))
        self.assertTrue(all(p.endswith('.read') for p in shots.SCREENSHOT_READ_PERMISSIONS))
        # QA N2: visibility scopes are named separately and are the only non-.read entries.
        self.assertEqual({'mode.tug', 'mode.cross'}, set(shots.SCREENSHOT_VISIBILITY_SCOPES))
        self.assertEqual(set(shots.SCREENSHOT_PERMISSIONS), set(shots.SCREENSHOT_READ_PERMISSIONS) | set(shots.SCREENSHOT_VISIBILITY_SCOPES))
        self.assertFalse(any(p.endswith(('.write', '.manage', '.decide', '.approve', '.verify', '.sign')) for p in shots.SCREENSHOT_PERMISSIONS))
        # Page-read grants only: never a report, triage, decision, release or verification capability.
        for forbidden in ('hse.safety_event.report', 'hse.safety_event.triage', 'departure.decide', 'workorder.write',
                          'workorder.release', 'workorder.verify', 'certificate.write', 'daily.noon.write', 'membership.manage'):
            self.assertNotIn(forbidden, shots.SCREENSHOT_PERMISSIONS)
        self.assertEqual(original['Authentication']['Oidc']['Memberships'][1:], once['Authentication']['Oidc']['Memberships'][1:])
        self.assertEqual(original['ConnectionStrings'], once['ConnectionStrings'])
        self.assertEqual(original['Authentication']['Oidc']['ClientSecret'], once['Authentication']['Oidc']['ClientSecret'])

    def test_enrollment_fails_closed_without_the_unique_lab_manager(self):
        realm, _, _, config = lab.make_configuration()
        broken = json.loads(json.dumps(realm))
        broken['users'] = [u for u in broken['users'] if u['username'] != 'lab-manager']
        with self.assertRaises(RuntimeError):
            shots.screenshot_membership(config, broken)
        foreign = json.loads(json.dumps(realm))
        foreign['realm'] = 'other'
        with self.assertRaises(RuntimeError):
            shots.screenshot_membership(config, foreign)


if __name__ == '__main__':
    unittest.main()
