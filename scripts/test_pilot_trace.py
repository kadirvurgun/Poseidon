import copy
from contextlib import redirect_stdout
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from pilot_trace import _validate_legacy_graph, main, validate


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
        result = _validate_legacy_graph(self.package, self.root)
        self.assertEqual([], result["structureErrors"])
        self.assertEqual([], result["criticalGaps"])
        self.assertEqual(10, len(result["executionGaps"]))
        self.assertTrue(all("no-passing" in gap for gap in result["executionGaps"]))
        self.assertFalse(result["accepted"])

    def test_legacy_fixture_graph_is_not_a_supported_public_contract(self):
        result = validate(self.package, self.root)
        self.assertEqual(["schema_version"], result["structureErrors"])
        self.assertFalse(result["accepted"])

    def test_duplicate_capability_node_missing_authority_and_broken_reference_are_rejected(self):
        p = copy.deepcopy(self.package)
        p["capabilities"][1]["number"] = 1
        c = p["capabilities"][0]; c["nodes"].append(c["nodes"][0].copy())
        c["nodes"][0]["source"] = "author-comment"
        c["edges"][0][1] = "absent-screen"
        errors = _validate_legacy_graph(p, self.root)["structureErrors"]
        for error in ("capability_inventory", "duplicate_id", "node_source_locator", "broken_trace_reference"):
            self.assertIn(error, errors)

    def test_independent_edges_do_not_hide_disconnected_critical_chain(self):
        p = copy.deepcopy(self.package); c = p["capabilities"][0]
        replacement = {**c["nodes"][1], "id": "unrelated-screen"}; c["nodes"].append(replacement)
        c["edges"][1][0] = "unrelated-screen"
        self.assertIn("capability-1:disconnected-critical-chain", _validate_legacy_graph(p, self.root)["criticalGaps"])

    def test_missing_implementation_and_execution_receipt_cannot_be_claimed_as_pass(self):
        p = copy.deepcopy(self.package); node = p["capabilities"][0]["nodes"][-1]
        node["state"] = "PASS"; node["files"] = ["missing/test.cs"]
        result = _validate_legacy_graph(p, self.root)
        self.assertIn("broken_code_reference", result["structureErrors"])
        self.assertIn("missing_execution_evidence", result["structureErrors"])

    def test_changed_source_bytes_and_traversal_are_not_authoritative_sources(self):
        p = copy.deepcopy(self.package)
        source = p["sources"]["trace"]
        (self.root / source["path"]).write_bytes(b"changed")
        self.assertIn("source_bytes", _validate_legacy_graph(p, self.root)["structureErrors"])
        source["path"] = "../outside.txt"
        self.assertIn("source_identity", _validate_legacy_graph(p, self.root)["structureErrors"])

    def test_boolean_or_noninteger_capability_number_cannot_alias_integer_identity(self):
        for identity in (True, 1.0, "1", None):
            with self.subTest(identity=identity):
                p = copy.deepcopy(self.package)
                p["capabilities"][0]["number"] = identity
                self.assertIn("capability_inventory", _validate_legacy_graph(p, self.root)["structureErrors"])




class CurrentSourceTraceTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(__file__).resolve().parents[1]
        self.package = json.loads((self.root / "contracts/trace/pilot-capabilities.v1.json").read_text(encoding="utf-8"))

    def test_current_inventory_defines_ten_without_claiming_current_passing_journeys(self):
        result = validate(self.package, self.root)
        self.assertEqual([], result["structureErrors"])
        self.assertEqual([], result["criticalGaps"])
        self.assertEqual(10, len(self.package["capabilities"]))
        self.assertEqual(149, sum(len(c.get("definitionChains", [])) for c in self.package["capabilities"]))
        self.assertEqual(10, len(result["executionGaps"]))
        self.assertTrue(result["implementationGaps"])
        self.assertFalse(result["accepted"])

    def test_missing_unknown_or_nonstring_schema_cannot_fall_back_to_weak_planned_graph(self):
        original = copy.deepcopy(self.package)
        for schema in (None, "poseidon.pilot-trace.v0", "unknown", True, {}):
            with self.subTest(schema=schema):
                self.package = copy.deepcopy(original)
                if schema is None:
                    self.package.pop("schemaVersion")
                else:
                    self.package["schemaVersion"] = schema
                for capability in self.package["capabilities"]:
                    capability.pop("sourceIdentity")
                    capability["definitionChains"] = []
                    number = capability["number"]
                    capability["nodes"] = [{"id": f"{number}:{kind}", "kind": kind, "source": "trace", "locator": "paragraph:1",
                                            "state": "PLANNED", "files": []}
                                           for kind in ("requirement", "screen", "api", "entity", "backlog", "test")]
                    capability["edges"] = [[a["id"], b["id"]] for a, b in zip(capability["nodes"], capability["nodes"][1:])]
                result = validate(self.package, self.root)
                self.assertEqual(["schema_version"], result["structureErrors"])
                self.assertFalse(result["accepted"])

    def test_exact_v1_still_requires_every_source_identity_after_generic_graph_replacement(self):
        for capability in self.package["capabilities"]:
            capability.pop("sourceIdentity")
            capability["definitionChains"] = []
        result = validate(self.package, self.root)
        self.assertIn("missing_source_definition_inventory", result["structureErrors"])
        self.assertFalse(result["accepted"])

    def test_current_contract_sources_are_bound_to_the_active_manifest_pins(self):
        source = self.package["sources"]["trace"]
        source["sha256"] = "0" * 64
        result = validate(self.package, self.root)
        self.assertEqual(["source_baseline"], result["structureErrors"])
        self.assertFalse(result["accepted"])

    def test_cli_rejects_unknown_schema_instead_of_selecting_legacy_graphs(self):
        self.package["schemaVersion"] = "unknown"
        for capability in self.package["capabilities"]:
            capability.pop("sourceIdentity")
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "trace.json"
            path.write_text(json.dumps(self.package), encoding="utf-8")
            output = io.StringIO()
            with patch("sys.argv", ["pilot_trace.py", str(path)]), redirect_stdout(output):
                code = main()
            self.assertEqual(1, code)
            self.assertEqual(["schema_version"], json.loads(output.getvalue())["structureErrors"])
            self.assertFalse(json.loads(output.getvalue())["accepted"])

    def test_safety_capture_maps_real_source_fields_to_its_backlog_not_the_empty_inbox(self):
        from source_contracts import SourceRecords
        records = SourceRecords(self.package["sources"], self.root)
        capability = self.package["capabilities"][8]
        self.assertEqual("events-learning", capability["definitionScreen"]["value"])
        records.resolve(capability["definitionScreen"])
        backlog = records.resolve({"source": "backlog", "locator": "sheet:Backlog/row:61"})
        self.assertIn("H61: events-learning / /api/v1/safety-events", backlog)
        self.assertIn("M61: T-S9-003", backlog)
        chains = capability["definitionChains"]
        self.assertEqual(19, len(chains))
        self.assertEqual({f"sheet:2316 Alan v46.3/row:{n}" for n in range(424, 443)},
                         {chain["requirement"]["locator"] for chain in chains})
        self.assertTrue(all(c["state"] == "DEFINED" and c["backlog"]["value"] == "DEV-060" for c in chains))
        result = validate(self.package, self.root)
        self.assertEqual([], result["structureErrors"])
        self.assertNotIn("capability-9:source-definition-gap", result["criticalGaps"])
        self.assertNotIn("capability-9:missing-critical-source-fields", result["criticalGaps"])
        self.assertIn("capability-9:declared-implementation-gap", result["implementationGaps"])
        self.assertIn("capability-9:no-passing-current-journey", result["executionGaps"])
        self.assertFalse(result["accepted"])

    def test_removing_a_safety_capture_field_is_still_a_critical_orphan(self):
        self.package["capabilities"][8]["definitionChains"].pop()
        self.assertIn("capability-9:missing-critical-source-fields", validate(self.package, self.root)["criticalGaps"])

    def test_real_but_wrong_zero_field_inbox_cannot_replace_the_capture_screen(self):
        self.package["capabilities"][8]["definitionScreen"] = {
            "source": "trace", "locator": "sheet:196 Ekran v46.3/row:2", "column": "A", "value": "safety-inbox"}
        result = validate(self.package, self.root)
        self.assertIn("source_mismatched_capability_screen", result["structureErrors"])
        self.assertIn("capability-9:missing-critical-source-fields", result["criticalGaps"])

    def test_zero_field_today_uses_existing_source_entities_and_original_backlog_test(self):
        from source_contracts import SourceRecords
        records = SourceRecords(self.package["sources"], self.root)
        capability = self.package["capabilities"][2]
        projection = capability["projectionDefinition"]
        self.assertEqual([], capability["definitionChains"])
        self.assertEqual("0", projection["fieldCount"]["value"])
        records.resolve(projection["fieldCount"])
        self.assertEqual({"Shared": "Vessel", "Ship": "VoyageContext", "Tug": "TugJob"},
                         {entity["mode"]: entity["token"] for entity in projection["entities"]})
        for entity in projection["entities"]:
            self.assertIn(entity["token"], records.resolve(entity["ref"]).split(", "))
        self.assertEqual("DEV-023", projection["backlog"]["value"])
        self.assertEqual("T-S2-004", projection["backlogTest"]["value"])
        records.resolve(projection["backlogTest"])

    def test_projection_cannot_be_used_to_skip_a_field_bearing_screen(self):
        capability = self.package["capabilities"][0]
        capability["projectionDefinition"] = copy.deepcopy(self.package["capabilities"][2]["projectionDefinition"])
        capability["definitionChains"] = []
        result = validate(self.package, self.root)
        self.assertIn("source_projection_or_composition_mismatch", result["structureErrors"])
        self.assertIn("capability-1:missing-critical-source-fields", result["criticalGaps"])

    def test_today_missing_tug_entity_or_wrong_original_mode_source_is_rejected(self):
        baseline = copy.deepcopy(self.package)
        del self.package["capabilities"][2]["projectionDefinition"]["entities"][2]
        self.assertIn("source_projection_or_composition_mismatch", validate(self.package, self.root)["structureErrors"])
        self.package = baseline
        entities = self.package["capabilities"][2]["projectionDefinition"]["entities"]
        entities[1]["ref"] = copy.deepcopy(entities[2]["ref"])
        self.assertIn("source_projection_or_composition_mismatch", validate(self.package, self.root)["structureErrors"])

    def test_today_wrong_backlog_test_or_real_but_wrong_screen_is_rejected(self):
        baseline = copy.deepcopy(self.package)
        self.package["capabilities"][2]["projectionDefinition"]["backlogTest"] = {
            "source": "backlog", "locator": "sheet:Backlog/row:25", "column": "M", "value": "T-S2-005"}
        self.assertIn("source_projection_or_composition_mismatch", validate(self.package, self.root)["structureErrors"])
        self.package = baseline
        self.package["capabilities"][2]["projectionDefinition"]["screen"] = {
            "source": "trace", "locator": "sheet:196 Ekran v46.3/row:2", "column": "A", "value": "safety-inbox"}
        self.assertIn("source_projection_or_composition_mismatch", validate(self.package, self.root)["structureErrors"])

    def test_mixed_fleet_requires_every_original_ship_and_tug_uat_definition_and_value(self):
        baseline = copy.deepcopy(self.package)
        composition = self.package["capabilities"][9]["compositionDefinition"]
        self.assertEqual(16, len(composition["mandatoryUat"]))
        composition["mandatoryUat"] = [ref for ref in composition["mandatoryUat"] if ref["value"].startswith("S-UAT-")]
        self.assertIn("source_projection_or_composition_mismatch", validate(self.package, self.root)["structureErrors"])
        self.package = baseline
        self.package["capabilities"][9]["compositionDefinition"]["valueProof"]["value"] = "Both modes passed"
        self.assertIn("source_projection_or_composition_mismatch", validate(self.package, self.root)["structureErrors"])

    def test_composite_cannot_hide_a_missing_safety_capture_field(self):
        self.package["capabilities"][8]["definitionChains"].pop()
        result = validate(self.package, self.root)
        self.assertIn("capability-9:missing-critical-source-fields", result["criticalGaps"])
        self.assertIn("capability-10:incomplete-composed-definition", result["criticalGaps"])

    def test_composite_self_cycle_duplicate_or_boolean_identity_cannot_shrink_scope(self):
        baseline = copy.deepcopy(self.package)
        for invalid in ([1, 2, 3, 4, 5, 6, 7, 8, 10], [1] * 9, [True, 2, 3, 4, 5, 6, 7, 8, 9]):
            with self.subTest(children=invalid):
                self.package = copy.deepcopy(baseline)
                self.package["capabilities"][9]["compositionDefinition"]["capabilities"] = invalid
                self.assertIn("source_projection_or_composition_mismatch", validate(self.package, self.root)["structureErrors"])

    def test_source_definition_forms_cannot_claim_a_passed_journey(self):
        baseline = copy.deepcopy(self.package)
        for number, field in ((3, "projectionDefinition"), (10, "compositionDefinition")):
            self.package = copy.deepcopy(baseline)
            self.package["capabilities"][number - 1][field]["state"] = "PASS"
            result = validate(self.package, self.root)
            self.assertIn("source_projection_or_composition_mismatch", result["structureErrors"])
            self.assertEqual(10, len(result["executionGaps"]))
            self.assertFalse(result["accepted"])

    def test_invented_requirement_is_not_resolved_by_existing_file(self):
        self.package["capabilities"][0]["definitionChains"][0]["requirement"]["value"] = "FR-invented"
        self.assertIn("source_definition_mismatch", validate(self.package, self.root)["structureErrors"])

    def test_one_complete_chain_does_not_hide_removed_critical_field(self):
        self.package["capabilities"][0]["definitionChains"].pop()
        self.assertIn("capability-1:missing-critical-source-fields", validate(self.package, self.root)["criticalGaps"])

    def test_duplicate_and_cross_field_source_chain_are_rejected(self):
        chains = self.package["capabilities"][0]["definitionChains"]
        chains.append(copy.deepcopy(chains[0]))
        chains[1]["test"] = chains[0]["test"].copy()
        result = validate(self.package, self.root)
        self.assertIn("duplicate_definition_id", result["structureErrors"])
        self.assertIn("source_mismatched_chain", result["structureErrors"])

    def test_deleting_canonical_inventory_does_not_fall_back_to_partial_nodes(self):
        del self.package["capabilities"][0]["sourceIdentity"]
        self.assertIn("missing_source_definition_inventory", validate(self.package, self.root)["structureErrors"])

    def test_wrong_source_column_cannot_relabel_api_as_requirement(self):
        chain = self.package["capabilities"][0]["definitionChains"][0]
        chain["requirement"] = chain["api"].copy()
        self.assertIn("source_definition_mismatch", validate(self.package, self.root)["structureErrors"])

    def test_extra_unlinked_critical_node_is_reported(self):
        cap = self.package["capabilities"][0]
        cap["nodes"].append({**cap["nodes"][0], "id": "unlinked-critical"})
        self.assertIn("capability-1:orphan-critical-node", validate(self.package, self.root)["criticalGaps"])

    def test_real_but_different_backlog_test_cannot_replace_the_owning_dev_test(self):
        chain = self.package["capabilities"][8]["definitionChains"][0]
        chain["backlogTest"] = copy.deepcopy(self.package["capabilities"][0]["definitionChains"][0]["backlogTest"])
        result = validate(self.package, self.root)
        self.assertIn("source_mismatched_backlog_chain", result["structureErrors"])
        self.assertFalse(result["accepted"])

    def test_actual_field_source_cannot_be_relabelled_as_another_field(self):
        self.package["capabilities"][0]["definitionChains"][0]["field"] = "not-the-source-field"
        result = validate(self.package, self.root)
        self.assertIn("source_field_identity_mismatch", result["structureErrors"])
        self.assertFalse(result["accepted"])

    def test_capability_exit_proof_must_match_its_original_pilot_scope_row(self):
        self.package["capabilities"][8]["exitProof"] = "Approved offline safety runtime"
        result = validate(self.package, self.root)
        self.assertIn("source_definition_mismatch", result["structureErrors"])
        self.assertIn("capability-9:declared-implementation-gap", result["implementationGaps"])
        self.assertIn("capability-9:no-passing-current-journey", result["executionGaps"])
        self.assertFalse(result["accepted"])

    def test_boolean_identity_is_rejected_even_with_real_source_identity_and_chains(self):
        self.package["capabilities"][0]["number"] = True
        result = validate(self.package, self.root)
        self.assertIn("capability_inventory", result["structureErrors"])
        self.assertFalse(result["accepted"])

if __name__ == "__main__":
    unittest.main()
