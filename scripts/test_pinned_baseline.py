"""Historical baseline pins resolve only through the recorded immutable prior manifest."""
import json
from pathlib import Path
import tempfile
import unittest

from pinned_baseline import PinnedBaselineError, pinned_manifest
import pilot_source_ownership
import pilot_trace
from s0_release_gate import source_scope

PIN = "poseidon-v46.3-20261030"
ROOT_NAME = "analiz/POSEIDON GUNCEL 30.10.2026"
PRIOR = "docs/sources/2026-10-30/manifest.json"
REPO = Path(__file__).resolve().parents[1]


class PinnedBaselineTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="poseidon-pin-tests-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "repository"
        self.root.mkdir()

    def write(self, name, value):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding="utf-8")
        return path

    def active(self, **extra):
        value = {"baseline_id": "poseidon-v46.3-20261115", "source_root": ROOT_NAME,
                 "manifest": "docs/sources/2026-11-15/manifest.json"}
        value.update(extra)
        for key in [k for k, v in value.items() if v is None]:
            value.pop(key)
        self.write("docs/sources/active.json", value)

    def test_active_baseline_equal_to_pin_uses_active_manifest(self):
        self.active(baseline_id=PIN, manifest="docs/sources/current/manifest.json")
        self.assertEqual("docs/sources/current/manifest.json", pinned_manifest(self.root, PIN, ROOT_NAME))

    def test_moved_active_baseline_uses_matching_prior_manifest(self):
        self.write(PRIOR, {"baseline_id": PIN, "files": []})
        self.active(prior_manifest=PRIOR)
        self.assertEqual(PRIOR, pinned_manifest(self.root, PIN, ROOT_NAME))

    def test_prior_manifest_with_other_baseline_or_outside_root_is_rejected(self):
        self.write(PRIOR, {"baseline_id": "poseidon-v46.3-20260101", "files": []})
        outside = Path(self.temp.name) / "outside.json"
        outside.write_text(json.dumps({"baseline_id": PIN, "files": []}), encoding="utf-8")
        for prior in (PRIOR, "../outside.json", str(outside).replace("\\", "/"), "docs/sources/missing.json"):
            with self.subTest(prior=prior):
                self.active(prior_manifest=prior)
                with self.assertRaises((PinnedBaselineError, OSError)):
                    pinned_manifest(self.root, PIN, ROOT_NAME)

    def test_missing_prior_manifest_or_changed_source_root_is_rejected(self):
        self.active()
        with self.assertRaises(PinnedBaselineError):
            pinned_manifest(self.root, PIN, ROOT_NAME)
        self.write(PRIOR, {"baseline_id": PIN, "files": []})
        self.active(prior_manifest=PRIOR, source_root="analiz/other")
        with self.assertRaises(PinnedBaselineError):
            pinned_manifest(self.root, PIN, ROOT_NAME)

    def test_oversized_prior_manifest_is_rejected(self):
        self.write(PRIOR, {"baseline_id": PIN, "files": [], "padding": "x" * 2048})
        self.active(prior_manifest=PRIOR)
        with self.assertRaises(PinnedBaselineError):
            pinned_manifest(self.root, PIN, ROOT_NAME, limit=1024)

    def test_repository_active_record_resolves_pin_through_prior_manifest(self):
        active = json.loads((REPO / "docs/sources/active.json").read_text(encoding="utf-8"))
        expected = active["manifest"] if active["baseline_id"] == PIN else active["prior_manifest"]
        self.assertEqual(expected, pinned_manifest(REPO, PIN, ROOT_NAME))

    def test_s0_source_scope_keeps_original_rejection_codes(self):
        policy = json.loads((REPO / "contracts/s0/exit-policy.v1.json").read_text(encoding="utf-8"))
        self.active()
        with self.assertRaisesRegex(ValueError, "^inactive_source_baseline$"):
            source_scope(policy, self.root)
        self.write(PRIOR, {"baseline_id": "poseidon-v46.3-20260101", "files": []})
        self.active(prior_manifest=PRIOR)
        with self.assertRaisesRegex(ValueError, "^inactive_source_baseline$"):
            source_scope(policy, self.root)
        self.active(prior_manifest="../outside.json")
        with self.assertRaisesRegex(ValueError, "^inactive_source_baseline$"):
            source_scope(policy, self.root)
        # Accepted prior path still re-checks per-file SHA256 pins against the manifest.
        self.write(PRIOR, {"baseline_id": PIN, "files": []})
        self.active(prior_manifest=PRIOR)
        with self.assertRaisesRegex(ValueError, "^source_manifest_mismatch$"):
            source_scope(policy, self.root)

    def test_pilot_trace_rejects_unresolvable_pin_as_source_baseline(self):
        package = {"schemaVersion": pilot_trace.SCHEMA, "sources": {}, "capabilities": []}
        self.active()
        self.assertEqual(["source_baseline"], pilot_trace.validate(package, self.root)["structureErrors"])
        self.write(PRIOR, {"baseline_id": "poseidon-v46.3-20260101", "files": []})
        self.active(prior_manifest=PRIOR)
        self.assertEqual(["source_baseline"], pilot_trace.validate(package, self.root)["structureErrors"])

    def test_pilot_source_ownership_accepts_prior_pin_and_rejects_bad_prior(self):
        plan = pilot_source_ownership.load(REPO / "contracts/sources/pilot-ownership.v1.json")
        for source in pilot_source_ownership.SOURCES.values():
            target = self.root / source["path"]
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((REPO / source["path"]).read_bytes())
        self.write(PRIOR, {"baseline_id": PIN, "files": []})
        self.active(prior_manifest=PRIOR)
        self.assertNotIn("active_baseline", pilot_source_ownership.validate(plan, self.root)["errors"])
        for prior in (None, "../outside.json"):
            with self.subTest(prior=prior):
                self.active(prior_manifest=prior)
                result = pilot_source_ownership.validate(plan, self.root)
                self.assertEqual(["active_baseline"], result["errors"])
                self.assertFalse(result["accepted"])
        self.write(PRIOR, {"baseline_id": "poseidon-v46.3-20260101", "files": []})
        self.active(prior_manifest=PRIOR)
        self.assertEqual(["active_baseline"], pilot_source_ownership.validate(plan, self.root)["errors"])


if __name__ == "__main__":
    unittest.main()
