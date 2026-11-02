"""Authored DEV-008 regressions; execution belongs to the coordinated S0 gate."""
import copy
import json
from pathlib import Path
import tempfile
import unittest

from pilot_source_ownership import BASELINE, SCOPES, SOURCES, load, validate


class PilotSourceOwnershipTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(__file__).resolve().parents[1]
        self.path = self.root / "contracts/sources/pilot-ownership.v1.json"
        self.plan = load(self.path)

    def errors(self):
        return validate(self.plan, self.root)["errors"]

    def test_complete_source_pinned_seven_scope_candidate_is_never_approval(self):
        result = validate(self.plan, self.root)
        self.assertEqual([], result["errors"])
        self.assertTrue(result["definitionValid"])
        self.assertTrue(result["reviewRequired"])
        self.assertFalse(result["accepted"])
        self.assertFalse(result["activationAllowed"])
        self.assertEqual(set(SCOPES), {row["scope"] for row in self.plan["scopes"]})
        self.assertEqual(9, sum(len(row["candidateSources"]) for row in self.plan["scopes"]))
        self.assertTrue(all(candidate["lifecycleAssertion"]["value"] == "CURRENT"
                            for scope in self.plan["scopes"] for candidate in scope["candidateSources"]))

    def test_missing_or_duplicated_category_cannot_shrink_the_seven_scope_inventory(self):
        original = copy.deepcopy(self.plan)
        self.plan["scopes"].pop()
        self.assertIn("scope_inventory", self.errors())
        self.plan = original
        self.plan["scopes"][-1] = copy.deepcopy(self.plan["scopes"][0])
        self.assertIn("scope_inventory", self.errors())

    def test_all_three_class_candidates_are_required_and_cannot_be_duplicated(self):
        original = copy.deepcopy(self.plan)
        self.plan["scopes"][-1]["candidateSources"].pop()
        self.assertIn("candidate_inventory", self.errors())
        self.plan = original
        rows = self.plan["scopes"][-1]["candidateSources"]
        rows[2] = copy.deepcopy(rows[0])
        self.assertIn("source_reference", self.errors())

    def test_external_issuer_cannot_be_relabelled_as_internal_owner_or_reviewer(self):
        self.plan["scopes"][0]["ownerRole"]["role"] = "IMO"
        self.plan["scopes"][0]["reviewerRole"]["role"] = "IMO"
        self.assertIn("owner_role_plan", self.errors())
        self.assertIn("reviewer_role_plan", self.errors())

    def test_owner_and_reviewer_roles_have_distinct_exact_controlled_authorities(self):
        scope = self.plan["scopes"][0]
        scope["reviewerRole"]["role"] = scope["ownerRole"]["role"]
        scope["reviewerRole"]["source"] = copy.deepcopy(scope["ownerRole"]["source"])
        self.assertIn("reviewer_role_plan", self.errors())

    def test_version_metadata_is_required_even_when_the_edition_is_honestly_open(self):
        candidate = self.plan["scopes"][1]["candidateSources"][0]
        del candidate["version"]["declaration"]
        self.assertIn("candidate_version", self.errors())

    def test_wrong_real_source_declaration_cannot_be_substituted_for_version(self):
        candidate = self.plan["scopes"][1]["candidateSources"][0]
        candidate["version"]["declaration"] = copy.deepcopy(self.plan["scopes"][0]["candidateSources"][0]["instrument"])
        self.assertIn("candidate_version", self.errors())

    def test_candidate_snapshot_is_not_an_authoritative_legal_edition(self):
        version = self.plan["scopes"][0]["candidateSources"][0]["version"]
        version["authoritativeEdition"] = BASELINE
        version["resolution"] = "VERIFIED"
        self.assertIn("candidate_version", self.errors())

    def test_source_pin_path_or_hash_changes_and_version_pin_changes_are_rejected(self):
        original = copy.deepcopy(self.plan)
        for mutation in ("path", "sha256"):
            self.plan = copy.deepcopy(original)
            self.plan["sources"]["register"][mutation] = "../uncontrolled" if mutation == "path" else "0" * 64
            self.assertIn("source_pins", self.errors())
        self.plan = original
        self.plan["scopes"][0]["candidateSources"][0]["version"]["snapshotSha256"] = "0" * 64
        self.assertIn("candidate_version", self.errors())

    def test_changed_source_bytes_fail_before_any_role_or_law_approval(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            active = root / "docs/sources/active.json"; active.parent.mkdir(parents=True)
            active.write_text(json.dumps({"baseline_id": BASELINE, "source_root": "analiz/POSEIDON GUNCEL 30.10.2026"}), encoding="utf-8")
            for source in SOURCES.values():
                target = root / source["path"]; target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((self.root / source["path"]).read_bytes())
            path = root / SOURCES["register"]["path"]
            path.write_bytes(path.read_bytes() + b"changed candidate")
            result = validate(self.plan, root)
            self.assertIn("source_bytes", result["errors"])
            self.assertFalse(result["definitionValid"])
            self.assertFalse(result["accepted"])

    def test_unverified_unknown_or_current_lifecycle_cannot_become_validation_approval(self):
        original = copy.deepcopy(self.plan)
        for state in ("UNKNOWN", "CURRENT", "VERIFIED", "APPROVED", None):
            with self.subTest(state=state):
                self.plan = copy.deepcopy(original)
                self.plan["scopes"][0]["validation"]["state"] = state
                result = validate(self.plan, self.root)
                self.assertIn("validation_or_activation_claim", result["errors"])
                self.assertFalse(result["activationAllowed"])

    def test_no_plan_status_or_embedded_receipt_can_self_approve_this_artifact(self):
        self.plan["status"] = "ADOPTED"
        self.plan["review"] = {"state": "APPROVED", "decision": "PASS", "evidence": "a" * 64}
        self.assertIn("approval_or_activation_claim", self.errors())

    def test_numeric_truthy_flags_do_not_grant_operational_activation(self):
        self.plan["activationBlocked"] = 1
        self.plan["scopes"][0]["validation"]["activationBlocked"] = 1
        errors = self.errors()
        self.assertIn("approval_or_activation_claim", errors)
        self.assertIn("validation_or_activation_claim", errors)

    def test_pilot_flag_class_and_real_membership_cannot_be_invented_in_the_plan(self):
        self.plan["scopes"][-1]["selection"]["selectedOperationalContext"] = "unreviewed-class-pack"
        self.plan["scopes"][0]["ownerRole"]["actorId"] = "synthetic-person-not-an-appointment"
        self.assertIn("invented_operational_binding", self.errors())
        self.assertIn("owner_role_plan", self.errors())

    def test_extra_credentials_or_person_fields_are_rejected_and_never_echoed(self):
        marker = "SYNTHETIC_UNTRUSTED_SECRET_NEVER_ECHO"
        self.plan["credentials"] = {"password": marker}
        result = validate(self.plan, self.root)
        self.assertIn("plan_shape", result["errors"])
        self.assertNotIn(marker, json.dumps(result))

    def test_later_legal_refresh_and_open_work_cannot_be_removed(self):
        self.plan["laterGates"].pop()
        self.plan["scopes"][0]["openWork"]["items"].pop()
        self.assertIn("later_gate_inventory", self.errors())
        self.assertIn("open_work_inventory", self.errors())

    def test_loader_rejects_duplicate_keys_and_nonfinite_constants(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "plan.json"
            for text in ('{"status":"AUTHORED_UNVERIFIED","status":"ADOPTED"}', '{"value":NaN}'):
                path.write_text(text, encoding="utf-8")
                with self.assertRaises(ValueError):
                    load(path)


if __name__ == "__main__":
    unittest.main()
