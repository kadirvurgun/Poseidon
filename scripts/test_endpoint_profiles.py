"""PLAN-1511-5803: unit tests for scripts/endpoint_profiles.py (no network, no model calls)."""
import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

import endpoint_profiles as ep


def fact(method, path, **overrides):
    value = {"method": method, "normalizedPath": path, "operationId": "Op" + method, "anonymous": False,
             "authorizationPolicies": ["DirectoryRead"], "requestContentTypes": [], "requestSchema": None,
             "concurrencyEvidence": "NONE_DECLARED", "securityOperationId": None, "owner": "Owner"}
    value.update(overrides)
    return value


def contract(*endpoints):
    return {"schemaVersion": "poseidon.endpoint-profiles.v1", "profiles": [{"profileId": "NATIVE-READ-1"}],
            "exclusions": [], "endpoints": list(endpoints)}


class EndpointProfileScriptTests(unittest.TestCase):
    def test_new_endpoint_is_marked_unassigned_never_auto_profiled(self):
        merged = ep.merge([fact("GET", "/api/v1/new")], contract())
        entry = merged["endpoints"][0]
        self.assertEqual(ep.UNASSIGNED, entry["profileId"])
        self.assertEqual([], entry["deviations"])
        self.assertEqual(["GET /api/v1/new"], ep.unassigned(merged))

    def test_curated_fields_are_preserved_and_observed_fields_refreshed(self):
        existing = dict(fact("GET", "/api/v1/a"), profileId="NATIVE-READ-1", deviations=["NON_JSON_ERROR_ENVELOPE"])
        merged = ep.merge([fact("GET", "/api/v1/a", operationId="Renamed", owner="NewOwner")], contract(existing))
        entry = merged["endpoints"][0]
        self.assertEqual("NATIVE-READ-1", entry["profileId"])
        self.assertEqual(["NON_JSON_ERROR_ENVELOPE"], entry["deviations"])
        self.assertEqual("Renamed", entry["operationId"])
        self.assertEqual("NewOwner", entry["owner"])
        self.assertEqual(ep.ENTRY_ORDER, list(entry))

    def test_removed_endpoints_are_dropped_and_entries_sorted(self):
        stale = dict(fact("GET", "/api/v1/gone"), profileId="NATIVE-READ-1", deviations=[])
        merged = ep.merge([fact("POST", "/api/v1/b"), fact("GET", "/api/v1/b"), fact("GET", "/api/v1/a")], contract(stale))
        self.assertEqual([("GET", "/api/v1/a"), ("GET", "/api/v1/b"), ("POST", "/api/v1/b")],
                         [(e["method"], e["normalizedPath"]) for e in merged["endpoints"]])

    def test_render_round_trips_committed_contract_byte_for_byte(self):
        text = ep.CONTRACT.read_text(encoding="utf-8")
        self.assertEqual(text, ep.render(json.loads(text)))

    def test_render_preserves_top_level_key_order(self):
        data = {"z": 1, "endpoints": [], "a": {"b": [1]}}
        self.assertEqual(["z", "endpoints", "a"], list(json.loads(ep.render(data))))

    def test_main_exit_code_is_one_when_unassigned_and_zero_otherwise(self):
        with tempfile.TemporaryDirectory() as tmp:
            target = Path(tmp) / "contract.json"
            observed = Path(tmp) / "observed.json"
            known = dict(fact("GET", "/api/v1/a"), profileId="NATIVE-READ-1", deviations=[])
            target.write_text(ep.render(contract(known)), encoding="utf-8")
            observed.write_text(json.dumps([fact("GET", "/api/v1/a")]), encoding="utf-8")
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, ep.main(["x", str(observed)], target))
            observed.write_text(json.dumps([fact("GET", "/api/v1/a"), fact("GET", "/api/v1/new")]), encoding="utf-8")
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                self.assertEqual(1, ep.main(["x", str(observed)], target))
            self.assertIn("UNASSIGNED GET /api/v1/new", out.getvalue())
            self.assertEqual(ep.UNASSIGNED, json.loads(target.read_text(encoding="utf-8"))["endpoints"][1]["profileId"])
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(2, ep.main(["x"], target))


if __name__ == "__main__":
    unittest.main()
