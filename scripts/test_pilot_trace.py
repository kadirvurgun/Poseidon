import copy
import hashlib
from pathlib import Path
import tempfile
import unittest

from pilot_trace import validate


class PilotTraceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        source = "analiz/POSEIDON GUNCEL 30.10.2026/synthetic.txt"
        path = self.root / source; path.parent.mkdir(parents=True); path.write_bytes(b"synthetic fixture only")
        self.package = {"sources": {"trace": {"path": source, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}}, "capabilities": []}
        for number in range(1, 11):
            nodes = [{"id": f"{number}:{kind}", "kind": kind, "source": "trace", "locator": "paragraph:1", "state": "DEFINED", "files": []}
                     for kind in ("requirement", "screen", "api", "entity", "backlog", "test")]
            self.package["capabilities"].append({"number": number, "source": "trace", "locator": f"sheet:Pilot Scope v46.2/row:{number + 1}",
                                                  "nodes": nodes, "edges": [[a["id"], b["id"]] for a, b in zip(nodes, nodes[1:])], "remaining": []})

    def test_defined_complete_chain_is_not_passing_evidence(self):
        result = validate(self.package, self.root)
        self.assertEqual([], result["structureErrors"])
        self.assertEqual([], result["criticalGaps"])
        self.assertEqual(10, len(result["executionGaps"]))
        self.assertTrue(all("no-passing" in gap for gap in result["executionGaps"]))
        self.assertFalse(result["accepted"])

    def test_duplicate_capability_node_missing_authority_and_broken_reference_are_rejected(self):
        p = copy.deepcopy(self.package)
        p["capabilities"][1]["number"] = 1
        c = p["capabilities"][0]; c["nodes"].append(c["nodes"][0].copy())
        c["nodes"][0]["source"] = "author-comment"
        c["edges"][0][1] = "absent-screen"
        errors = validate(p, self.root)["structureErrors"]
        for error in ("capability_inventory", "duplicate_id", "node_source_locator", "broken_trace_reference"):
            self.assertIn(error, errors)

    def test_independent_edges_do_not_hide_disconnected_critical_chain(self):
        p = copy.deepcopy(self.package); c = p["capabilities"][0]
        replacement = {**c["nodes"][1], "id": "unrelated-screen"}; c["nodes"].append(replacement)
        c["edges"][1][0] = "unrelated-screen"
        self.assertIn("capability-1:disconnected-critical-chain", validate(p, self.root)["criticalGaps"])

    def test_missing_implementation_and_execution_receipt_cannot_be_claimed_as_pass(self):
        p = copy.deepcopy(self.package); node = p["capabilities"][0]["nodes"][-1]
        node["state"] = "PASS"; node["files"] = ["missing/test.cs"]
        result = validate(p, self.root)
        self.assertIn("broken_code_reference", result["structureErrors"])
        self.assertIn("missing_execution_evidence", result["structureErrors"])

    def test_changed_source_bytes_and_traversal_are_not_authoritative_sources(self):
        p = copy.deepcopy(self.package)
        source = p["sources"]["trace"]
        (self.root / source["path"]).write_bytes(b"changed")
        self.assertIn("source_bytes", validate(p, self.root)["structureErrors"])
        source["path"] = "../outside.txt"
        self.assertIn("source_identity", validate(p, self.root)["structureErrors"])


if __name__ == "__main__":
    unittest.main()
