import copy
import json
from pathlib import Path
import tempfile
import unittest
from golden_dataset import load, validate

class GoldenDatasetTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(__file__).resolve().parents[1]
        self.data = load(self.root / "contracts/golden/pilot-dataset.v1.json")

    def test_current_dataset_resolves_original_sources_but_confers_no_acceptance(self):
        result = validate(self.data, self.root)
        self.assertEqual([], result["errors"])
        self.assertFalse(result["accepted"])
        self.assertEqual("NOT_RUN", result["execution"])

    def test_source_expected_result_cannot_be_rewritten_as_ready(self):
        self.data["sourceCases"][2]["expectedApplicability"] = "READY"
        self.assertIn("source_expectation_mismatch", validate(self.data, self.root)["errors"])

    def test_source_locator_and_hash_changes_fail_closed(self):
        self.data["profiles"][0]["source"]["locator"] = "sheet:Golden Profiles/row:9999"
        self.data["sources"]["matrix"]["sha256"] = "0" * 64
        self.assertIn("source_reference", validate(self.data, self.root)["errors"])

    def test_removed_profile_duplicate_case_and_hidden_software_gap_are_rejected(self):
        self.data["profiles"].pop()
        self.data["consumerCases"].append(copy.deepcopy(self.data["consumerCases"][0]))
        self.data["productionAdapterGaps"] = []
        errors = validate(self.data, self.root)["errors"]
        for error in ("profile_inventory", "consumer_inventory", "hidden_adapter_gaps"):
            self.assertIn(error, errors)

    def test_adapter_or_execution_claim_cannot_be_manufactured(self):
        self.data["sourceCases"][0]["adapter"] = "IMPLEMENTED"
        self.data["consumerCases"][0]["execution"] = "PASS"
        errors = validate(self.data, self.root)["errors"]
        self.assertIn("fabricated_adapter_or_execution", errors)
        self.assertIn("fabricated_readiness_or_execution", errors)

    def test_bounded_loader_rejects_large_input(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "oversized.json"
            path.write_bytes(b" " * (512 * 1024 + 1))
            with self.assertRaises(ValueError): load(path)

if __name__ == "__main__": unittest.main()
