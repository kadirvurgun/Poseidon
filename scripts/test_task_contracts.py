import copy
import json
from pathlib import Path
import unittest
from task_contracts import validate

class TaskContractTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(__file__).resolve().parents[1]
        self.register = json.loads((self.root / "contracts/tasks/current-authoring.v1.json").read_text(encoding="utf-8"))
        self.register["tasks"] = [next(t for t in self.register["tasks"] if t["id"] == "DEV-009")]
        self.task = self.register["tasks"][0]

    def errors(self): return validate(self.register, self.root)["errors"]
    def has(self, suffix): self.assertTrue(any(e.endswith(":" + suffix) for e in self.errors()), suffix)

    def test_deferred_draft_is_valid_definition_without_acceptance(self):
        result = validate(self.register, self.root)
        self.assertEqual([], result["errors"])
        self.assertTrue(result["pendingGates"])
        self.assertFalse(result["accepted"])

    def test_every_impact_is_required_even_when_no_runtime_mutation(self):
        del self.task["impacts"]["audit"]
        self.has("missing_impact_assessment")

    def test_empty_not_applicable_reason_does_not_hide_impact(self):
        self.task["impacts"]["offline"] = {"disposition": "not_applicable", "reason": "", "references": []}
        self.has("incomplete_impact_assessment")

    def test_duplicate_ids_are_rejected(self):
        self.register["tasks"].append(copy.deepcopy(self.task))
        self.assertIn("duplicate_or_missing_task_id", self.errors())

    def test_broken_source_and_traversal_code_reference_are_rejected(self):
        self.task["source"]["locator"] = "sheet:Backlog/row:99999"
        self.task["references"]["code"] = ["../outside.py"]
        self.has("broken_source_reference"); self.has("broken_reference")

    def test_acceptance_dependencies_cannot_be_silently_dropped(self):
        self.task["dependencies"] = []
        self.has("dependency_inventory")

    def test_source_trace_and_ac_cannot_be_rewritten(self):
        self.task["traceId"] = "T-invented"
        self.task["acceptanceCriteria"] = "all accepted"
        self.has("source_trace_mismatch"); self.has("source_mismatch")

    def test_accepted_with_deferred_critical_gap_and_no_independent_evidence_is_rejected(self):
        self.task["acceptanceState"] = "ACCEPTED"
        self.task["unresolvedSoftware"] = [{"severity": "critical", "description": "Required selected adapter missing"}]
        for error in ("accepted_with_unexecuted_checks", "accepted_with_critical_software", "missing_independent_evidence", "accepted_with_pending_decisions"):
            self.has(error)

    def test_authored_is_not_run_or_pass(self):
        check = self.task["checks"]["unit"]
        check.update(state="PASS", authored=True, runCount=0, passedCount=0)
        self.has("missing_pass_evidence")
        check.update(state="DEFERRED", runCount=1, passedCount=1)
        self.has("deferred_is_not_execution")

    def test_impossible_counts_and_unjustified_exclusion_are_rejected(self):
        self.task["checks"]["unit"].update(runCount=1, passedCount=2)
        self.task["checks"]["integration"].update(state="NOT_APPLICABLE")
        self.has("contradictory_execution_counts"); self.has("unjustified_exclusion")

    def test_missing_check_cannot_silently_reduce_release_scope(self):
        del self.task["checks"]["security"]
        self.has("missing_check_assessment")

    def test_author_file_is_not_independent_evidence_or_exact_target_proof(self):
        self.task["acceptanceState"] = "ACCEPTED"
        self.task["independentEvidence"] = [{"path": "docs/tasks/s0-contract-closure-01.md", "role": "author", "targetCommit": "0" * 40}]
        self.has("invalid_independent_evidence"); self.has("target_commit_mismatch")

if __name__ == "__main__": unittest.main()
