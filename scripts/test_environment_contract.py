"""AUTHORED / NOT RUN. Contract constraints and adversarial promotion inputs."""
import copy
import json
from pathlib import Path
import tempfile
import unittest

from environment_contract import definition_errors, deployment_errors, load


class EnvironmentContractTests(unittest.TestCase):
    def setUp(self):
        self.contract = json.loads((Path(__file__).resolve().parents[1] / "contracts/environments/environments.v1.json").read_text(encoding="utf-8"))

    def manifest(self, name="TEST"):
        refs = self.contract["environments"][name]["references"]
        bindings = {ref: "reviewed-ref://synthetic/policy#" + "a" * 64 for ref in refs.values()}
        for field in ("databaseSecret", "oidcSecret", "objectStoreSecret", "profileKeySecret", "offlineKeySecret"):
            bindings[refs[field]] = "secret-ref://synthetic/test#v1"
        for field in ("publicOrigin", "apiOrigin"):
            bindings[refs[field]] = "https://synthetic.example.invalid"
        return {"environment": name, "bindings": bindings, "artifactSha256": "a" * 64,
                "promotedArtifactSha256": "a" * 64,
                "checks": {c: "b" * 64 for c in self.contract["promotion"]["checks"]},
                "approvalReceipt": "c" * 64, "dataApprovalReceipt": "d" * 64}

    def test_frozen_definition_does_not_imply_deployment(self):
        self.assertEqual([], definition_errors(self.contract))
        unresolved = self.manifest()
        unresolved["bindings"] = {}
        self.assertEqual(["unresolved_configuration"], deployment_errors(self.contract, unresolved))

    def test_every_environment_has_separate_complete_scoped_references(self):
        for name in self.contract["environments"]:
            c = copy.deepcopy(self.contract)
            del c["environments"][name]["references"]["retentionPolicy"]
            self.assertIn("mandatory_references", definition_errors(c))
            c = copy.deepcopy(self.contract)
            c["environments"][name]["references"]["publicOrigin"] = "config://poseidon/OTHER/origin"
            self.assertIn("reference_scope", definition_errors(c))

    def test_production_copy_cloud_and_caller_role_cannot_weaken_definition(self):
        for field, value, error in [("productionCopyAllowed", True, "data_boundary"),
                                    ("hosting", "ManagedCloud", "hosting_boundary"),
                                    ("approvalRole", "Author", "responsible_role")]:
            c = copy.deepcopy(self.contract)
            c["environments"]["PILOT"][field] = value
            self.assertIn(error, definition_errors(c))

    def test_artifact_change_or_missing_required_check_blocks_promotion(self):
        m = self.manifest()
        self.assertEqual([], deployment_errors(self.contract, m)) # format only, never authority
        m["promotedArtifactSha256"] = "e" * 64
        del m["checks"]["IndependentQA"]
        self.assertEqual(["artifact_promotion", "missing_check_receipts"], deployment_errors(self.contract, m))

    def test_origins_reject_credentials_queries_fragments_paths_and_external_http(self):
        for url in ("https://user:synthetic@example.invalid", "https://example.invalid/?session=synthetic",
                    "https://example.invalid/#private", "https://example.invalid/api", "http://example.invalid", "https://example.invalid:bad"):
            m = self.manifest()
            m["bindings"][self.contract["environments"]["TEST"]["references"]["publicOrigin"]] = url
            self.assertIn("origin_policy", deployment_errors(self.contract, m))

    def test_loopback_http_is_limited_to_dev_test_and_plaintext_secrets_are_rejected(self):
        for name in ("TEST", "PILOT"):
            m = self.manifest(name)
            refs = self.contract["environments"][name]["references"]
            m["bindings"][refs["publicOrigin"]] = "http://127.0.0.1:4173"
            self.assertEqual(name == "PILOT", "origin_policy" in deployment_errors(self.contract, m))
            m["bindings"][refs["databaseSecret"]] = "synthetic-inline-value"
            self.assertIn("secret_reference_policy", deployment_errors(self.contract, m))

    def test_missing_policy_and_independent_authority_references_remain_gaps(self):
        m = self.manifest("UAT")
        m["approvalReceipt"] = None
        m["dataApprovalReceipt"] = None
        m["bindings"][self.contract["environments"]["UAT"]["references"]["retentionPolicy"]] = "TODO"
        self.assertEqual(["missing_data_authority", "missing_deployment_authority", "reviewed_policy_reference"], deployment_errors(self.contract, m))

    def test_duplicate_json_keys_cannot_hide_changed_origin_or_role(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "duplicate.json"
            path.write_text('{"environment":"TEST","environment":"PROD"}', encoding="utf-8")
            with self.assertRaises(ValueError):
                load(path)


if __name__ == "__main__":
    unittest.main()
