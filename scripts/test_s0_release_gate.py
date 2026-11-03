"""S0 exit adversarial cases. Synthetic host observations are test fixtures only."""
import copy
from datetime import datetime, timezone
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from s0_release_gate import (GitTarget, TrustedOrchestratorAuthority, dependencies,
                             digest, evaluate, prepare_candidate, read_json, safe_path, source_scope)


TARGET = {"commit": "a" * 40, "tree": "b" * 40}
NOW = datetime(2026, 11, 2, 9, tzinfo=timezone.utc)


class FixtureGit:
    def __init__(self):
        self.problem = None
        self.contents = {"fixture.py": b"synthetic implementation and meaningful test identity"}

    def verify(self, target):
        if self.problem:
            raise ValueError(self.problem)
        if target != TARGET:
            raise ValueError("stale_target")

    def artifact(self, target, name):
        if target != TARGET or name not in self.contents:
            raise ValueError("target_artifact_unavailable")
        return self.contents[name]


class S0ReleaseGateTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="poseidon-s0-tests-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name) / "repository"
        self.root.mkdir()
        self.git = FixtureGit()
        actual_root = Path(__file__).resolve().parents[1]
        self.policy = json.loads((actual_root / "contracts/s0/exit-policy.v1.json").read_text(encoding="utf-8"))
        self.scope = {}
        ids = [f"DEV-{n:03}" for n in range(1, 13)] + ["DEV-116"]
        for ident in ids:
            deps = [] if ident in {"DEV-001", "DEV-007"} else ["DEV-001"]
            if ident == "DEV-012":
                deps = ids[:11]
            self.scope[ident] = {"id": ident, "locator": "fixture:" + ident,
                                 "acceptanceCriteria": "Literal synthetic criterion " + ident,
                                 "traceId": "TEST-" + ident, "ownerRole": "fixture engineering role",
                                 "storyPoints": 1, "dependencies": deps}
        self.candidate = {"schemaVersion": "poseidon.s0-candidate.v1", "baseline": self.policy["baseline"],
                          "target": copy.deepcopy(TARGET), "tasks": [], "blockers": [], "externalBindings": []}
        self.host = {"schemaVersion": "poseidon.s0-orchestrator-authority.v1",
                     "provider": {"kind": "trusted-orchestrator", "instanceId": "test-host", "coordinatorSessionId": "coordinator"},
                     "authorPrincipals": ["fixture:author"], "authorSessionIds": ["author-session"], "sessions": []}
        for session_id, principal in (("author-session", "fixture:author"), ("independent-session", "fixture:reviewer")):
            self.host["sessions"].append({"sessionId": session_id, "principal": principal,
                                           "providerInstanceId": "test-host", "state": "completed",
                                           "parentSessionId": "coordinator", "target": copy.deepcopy(TARGET),
                                           "baseline": self.policy["baseline"], "verifiedReports": []})
        for ident in ids:
            entry = {"id": ident, "authored": {"state": "AUTHORED", "artifacts": self.artifacts()}, "evidence": {}}
            self.candidate["tasks"].append(entry)
            for kind in ("execution", "source-review", "engineering-review", "acceptance"):
                receipt = self.receipt(ident, kind)
                if kind == "acceptance":
                    receipt["evidenceDigests"] = {k: v["sha256"] for k, v in entry["evidence"].items()}
                entry["evidence"][kind] = self.store(receipt)

    def artifacts(self):
        return [{"path": name, "sha256": digest(raw)} for name, raw in self.git.contents.items()]

    def entry(self, ident="DEV-001"):
        return next(t for t in self.candidate["tasks"] if t["id"] == ident)

    def receipt(self, ident, kind):
        author = kind == "execution"
        value = {"schemaVersion": "poseidon.s0-receipt.v1", "kind": kind, "taskId": ident,
                 "sessionId": "author-session" if author else "independent-session",
                 "principal": "fixture:author" if author else "fixture:reviewer",
                 "target": copy.deepcopy(TARGET), "baseline": self.policy["baseline"],
                 "sourceSha256": self.policy["sources"]["backlog"]["sha256"],
                 "traceId": self.scope[ident]["traceId"], "issuedAt": "2026-11-02T08:00:00Z",
                 "outcome": "PASS", "unresolvedP0": 0, "artifacts": self.artifacts()}
        if author:
            value["checks"] = {name: {"state": "PASS", "runCount": 3, "passedCount": 3,
                                      "failedCount": 0, "skippedCount": 0, "exitCode": 0,
                                      "commandId": "fixture-actual-command"}
                               for name in self.policy["requiredCheckAssessments"] + self.policy["pipelineChecks"]}
        else:
            value.update(acceptanceCriteria=self.scope[ident]["acceptanceCriteria"],
                         dependencies=self.scope[ident]["dependencies"], unresolvedBindings=[])
            if kind == "source-review":
                value["excludedChecks"] = {}
        return value

    def store(self, receipt, trust=True):
        name = ".runtime/s0/" + receipt["taskId"] + "-" + receipt["kind"] + ".json"
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        raw = json.dumps(receipt, sort_keys=True).encode("utf-8")
        path.write_bytes(raw)
        hashed = digest(raw)
        if trust:
            session = next(s for s in self.host["sessions"] if s["sessionId"] == receipt["sessionId"])
            session["verifiedReports"].append({"sha256": hashed, "kind": receipt["kind"], "taskId": receipt["taskId"], "outcome": "PASS"})
        return {"path": name, "sha256": hashed}

    def change_receipt(self, kind, mutate, ident="DEV-001", trust=True, update_acceptance=True):
        entry = self.entry(ident)
        ref = entry["evidence"][kind]
        receipt = json.loads((self.root / ref["path"]).read_text(encoding="utf-8"))
        mutate(receipt)
        entry["evidence"][kind] = self.store(receipt, trust)
        if kind != "acceptance" and update_acceptance:
            self.change_receipt("acceptance", lambda a: a["evidenceDigests"].update({kind: entry["evidence"][kind]["sha256"]}), ident=ident)

    def result(self, authority=True):
        provider = TrustedOrchestratorAuthority(self.host) if authority else None
        with patch("s0_release_gate.source_scope", return_value=self.scope):
            return evaluate(self.candidate, self.policy, self.root, provider, self.git, NOW)

    def assert_rejected(self, contains=None):
        result = self.result()
        self.assertFalse(result["accepted"])
        if contains:
            self.assertTrue(any(contains in e for e in result["errors"] + result["pendingGates"]), result)
        return result

    def test_verified_independent_exact_target_decisions_close_original_scope(self):
        result = self.result()
        self.assertTrue(result["accepted"], result)
        self.assertEqual(13, result["progress"]["acceptedTasks"])
        self.assertEqual(13, len(result["tasks"]))

    def test_source_inventory_is_thirteen_tasks_fifty_three_points_and_full_exit_range(self):
        root = Path(__file__).resolve().parents[1]
        actual = source_scope(self.policy, root)
        self.assertEqual(53, sum(t["storyPoints"] for t in actual.values()))
        self.assertEqual(13, len(actual))
        self.assertEqual([f"DEV-{n:03}" for n in range(1, 12)], actual["DEV-012"]["dependencies"])
        self.assertEqual("T-V463-002", actual["DEV-116"]["traceId"])

    def test_dependency_range_never_silently_becomes_only_endpoints(self):
        self.assertEqual([f"DEV-{n:03}" for n in range(1, 12)], dependencies("DEV-001:DEV-011"))
        for invalid in ("DEV-011:DEV-001", "DEV-000", "DEV-001:DEV-999", "DEV-001 ignored"):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                dependencies(invalid)

    def test_local_hashes_and_pass_labels_do_not_authenticate_own_reviews(self):
        result = self.result(authority=False)
        self.assertFalse(result["accepted"])
        self.assertIn("missing_external_authority", result["pendingGates"])
        self.assertEqual(0, result["progress"]["acceptedTasks"])

    def test_forged_receipt_not_observed_by_host_is_rejected_even_with_new_hash(self):
        self.change_receipt("source-review", lambda r: r.update(issuedAt="2026-11-02T08:01:00Z"), trust=False)
        self.assert_rejected("untrusted_or_nonindependent_receipt")

    def test_same_author_under_another_session_cannot_self_approve(self):
        independent = self.host["sessions"][1]
        independent["principal"] = "fixture:author"
        self.change_receipt("source-review", lambda r: r.update(principal="fixture:author"))
        self.assert_rejected("untrusted_or_nonindependent_receipt")

    def test_author_session_with_changed_display_principal_cannot_self_approve(self):
        self.change_receipt("engineering-review", lambda r: r.update(sessionId="author-session", principal="fixture:author"))
        self.assert_rejected("untrusted_or_nonindependent_receipt")

    def test_pending_or_unrelated_provider_session_is_not_trusted(self):
        for key, value in (("state", "running"), ("parentSessionId", "unrelated"), ("providerInstanceId", "other-host")):
            with self.subTest(key=key):
                host = copy.deepcopy(self.host)
                host["sessions"][1][key] = value
                with self.assertRaises(ValueError):
                    TrustedOrchestratorAuthority(host)

    def test_review_of_other_commit_or_source_baseline_is_stale(self):
        self.change_receipt("engineering-review", lambda r: r.update(target={"commit": "c" * 40, "tree": "d" * 40}))
        self.assert_rejected("stale_receipt_target_or_source")

    def test_current_dirty_target_cannot_count_previous_target_acceptance(self):
        self.git.problem = "dirty_target"
        result = self.assert_rejected("dirty_target")
        self.assertEqual(0, result["progress"]["acceptedTasks"])

    def test_erp_boundary_cannot_be_removed_from_s0_denominator(self):
        self.candidate["tasks"] = [t for t in self.candidate["tasks"] if t["id"] != "DEV-116"]
        self.assert_rejected("invalid_s0_contract_or_candidate")

    def test_missing_middle_dependency_blocks_exit_even_when_endpoint_tasks_pass(self):
        self.entry("DEV-006")["evidence"].pop("engineering-review")
        result = self.assert_rejected("DEV-012:dependency-not-accepted:DEV-006")
        self.assertFalse(next(t for t in result["tasks"] if t["id"] == "DEV-012")["accepted"])

    def test_open_p0_blocks_phase_exit(self):
        self.candidate["blockers"] = [{"id": "BLK-TENANT-ISOLATION", "severity": "P0", "state": "OPEN"}]
        self.assert_rejected("unresolved_P0:BLK-TENANT-ISOLATION")

    def test_skipped_or_zero_discovered_tests_are_not_pass(self):
        self.change_receipt("execution", lambda r: r["checks"]["unit"].update(runCount=0, passedCount=0, skippedCount=1))
        self.assert_rejected("unexecuted_failed_or_skipped_check")

    def test_failing_build_blocks_source_task_even_if_other_tests_pass(self):
        self.change_receipt("execution", lambda r: r["checks"]["frontendBuild"].update(exitCode=2))
        self.assert_rejected("unexecuted_failed_or_skipped_check")

    def test_deferred_authored_tests_are_not_executed(self):
        self.change_receipt("execution", lambda r: r["checks"]["unit"].update(state="DEFERRED", runCount=0, passedCount=0))
        result = self.assert_rejected("check_not_pass")
        self.assertFalse(next(t for t in result["tasks"] if t["id"] == "DEV-001")["executed"])

    def test_source_reviewed_nonapplicable_ai_scope_is_supported_without_future_product_gate(self):
        self.change_receipt("execution", lambda r: r["checks"]["aiEvaluation"].update(state="NOT_APPLICABLE", runCount=0, passedCount=0))
        self.change_receipt("source-review", lambda r: r["excludedChecks"].update(aiEvaluation="No AI execution or provider is introduced by this contract task."))
        self.assertTrue(self.result()["accepted"])

    def test_literal_unit_build_lint_artifact_requirements_cannot_be_excluded(self):
        self.change_receipt("execution", lambda r: r["checks"]["unit"].update(state="NOT_APPLICABLE", runCount=0, passedCount=0))
        self.change_receipt("source-review", lambda r: r["excludedChecks"].update(unit="Claimed unnecessary"))
        self.assert_rejected("unreviewed_check_exclusion")

    def test_mutated_artifact_invalidates_exact_target_evidence(self):
        self.git.contents["fixture.py"] = b"different code"
        self.assert_rejected("changed_target_artifact")

    def test_generic_review_cannot_replace_original_literal_criterion(self):
        self.change_receipt("source-review", lambda r: r.update(acceptanceCriteria="Looks good"))
        self.assert_rejected("incomplete-literal-criterion")

    def test_replacing_report_after_acceptance_breaks_approval_binding(self):
        self.change_receipt("execution", lambda r: r.update(issuedAt="2026-11-02T08:01:00Z"), update_acceptance=False)
        self.assert_rejected("evidence-digest-mismatch")

    def test_missing_actual_external_binding_remains_explicit(self):
        self.candidate["externalBindings"] = [{"taskId": "DEV-008", "criterion": "Source owner role assignment", "ownerRole": "Regulatory Lead", "state": "MISSING"}]
        self.assert_rejected("DEV-008:missing-external-binding")

    def test_invalid_time_does_not_echo_raw_receipt_secrets_in_diagnostics(self):
        self.change_receipt("source-review", lambda r: r.update(issuedAt="private-value-do-not-logZ"))
        result = self.assert_rejected("invalid_receipt")
        self.assertNotIn("private-value", json.dumps(result))

    def test_expired_and_future_receipts_do_not_pass(self):
        self.change_receipt("engineering-review", lambda r: r.update(expiresAt="2026-11-02T08:30:00Z"))
        self.assert_rejected("expired_receipt")

    def test_author_controlled_export_path_and_changed_host_export_are_rejected(self):
        path = self.root / "author-observations.json"
        raw = json.dumps(self.host).encode()
        path.write_bytes(raw)
        with self.assertRaises(ValueError):
            TrustedOrchestratorAuthority.from_coordinator_export(path, digest(raw), self.root)
        outside = Path(self.directory.name) / "coordinator-observations.json"
        outside.write_bytes(raw)
        with self.assertRaises(ValueError):
            TrustedOrchestratorAuthority.from_coordinator_export(outside, "0" * 64, self.root)
        self.assertIsInstance(TrustedOrchestratorAuthority.from_coordinator_export(outside, digest(raw), self.root), TrustedOrchestratorAuthority)

    def test_duplicate_json_keys_and_oversized_inputs_fail_closed(self):
        path = self.root / "duplicate.json"
        path.write_text('{"outcome":"FAIL","outcome":"PASS"}')
        with self.assertRaises(ValueError):
            read_json(path, 4096)
        with self.assertRaises(ValueError):
            read_json(path, 1)

    def test_git_symlink_blobs_and_private_path_aliases_are_never_evidence(self):
        for name in ("../private.json", "C:/private.json", "file:///private.json", "safe//file", "safe\\file"):
            with self.subTest(name=name), self.assertRaises(ValueError):
                safe_path(self.root, name)
        reader = GitTarget(self.root)
        with patch.object(reader, "_git", return_value=b"120000 blob " + b"a" * 40 + b"\tlink\n"):
            with self.assertRaises(ValueError):
                reader.artifact(TARGET, "link")

    def test_provider_outage_or_truthy_string_never_becomes_approval(self):
        for value in (RuntimeError("private-provider-error"), "PASS"):
            with self.subTest(value=type(value).__name__):
                provider = type("FixtureProvider", (), {})()
                if isinstance(value, Exception):
                    def verify(*args):
                        raise RuntimeError("private-provider-error")
                    provider.verify = verify
                else:
                    provider.verify = lambda *args: "PASS"
                with patch("s0_release_gate.source_scope", return_value=self.scope):
                    result = evaluate(self.candidate, self.policy, self.root, provider, self.git, NOW)
                self.assertFalse(result["accepted"])
                self.assertNotIn("private-provider-error", json.dumps(result))

    def test_draft_generation_covers_all_thirteen_without_promoting_existing_references(self):
        register = self.root / self.policy["taskRegister"]
        register.parent.mkdir(parents=True)
        register.write_text(json.dumps({"tasks": [{"id": "DEV-010", "references": {"code": ["fixture.py"], "tests": [], "docs": []}}]}))
        self.git._git = lambda *args, **kwargs: ((TARGET["commit"] if args[-1] == "HEAD^{commit}" else TARGET["tree"]) + "\n").encode()
        with patch("s0_release_gate.source_scope", return_value=self.scope):
            result = prepare_candidate(self.policy, self.root, self.git)
        self.assertEqual(13, len(result["tasks"]))
        self.assertTrue(all(t["authored"]["state"] == "NOT_ASSESSED" and t["evidence"] == {} for t in result["tasks"]))
        self.assertEqual("OPEN", result["blockers"][0]["state"])
        self.assertEqual(self.artifacts(), next(t for t in result["tasks"] if t["id"] == "DEV-010")["authored"]["artifacts"])
        self.assertEqual([], next(t for t in result["tasks"] if t["id"] == "DEV-012")["authored"]["artifacts"])

    def use_actual_scope_with_registered_gap(self, severity, outside_s0=False):
        """Use a real current source row, not a made-up candidate blocker."""
        root = Path(__file__).resolve().parents[1]
        register_path = root / self.policy["taskRegister"]
        actual_register = json.loads(register_path.read_text(encoding="utf-8"))
        definition = (next(t for t in actual_register["tasks"] if t["id"] not in self.scope)
                      if outside_s0 else next(t for t in actual_register["tasks"] if t["id"] == "DEV-006"))
        definition["unresolvedSoftware"].append({"severity": severity,
            "description": "Required current S0 tenant isolation consumer remains missing." if severity == "critical"
                           else "Unavailable future integration adapter outside this literal S0 contract."})
        original_reader = read_json
        def scoped_reader(path, limit):
            if Path(path).resolve() == register_path.resolve():
                return b"unit-fixture-current-register", copy.deepcopy(actual_register)
            return original_reader(path, limit)
        with patch("s0_release_gate.read_json", side_effect=scoped_reader):
            self.scope = source_scope(self.policy, root)
        # Bind all otherwise-valid trusted receipts to the genuine original
        # source criteria/dependencies so rejection can only be the registered gap.
        for entry in self.candidate["tasks"]:
            entry["evidence"] = {}
            for kind in ("execution", "source-review", "engineering-review", "acceptance"):
                receipt = self.receipt(entry["id"], kind)
                if kind == "acceptance":
                    receipt["evidenceDigests"] = {k: v["sha256"] for k, v in entry["evidence"].items()}
                entry["evidence"][kind] = self.store(receipt)
        return definition["id"]

    def test_current_registered_s0_critical_gap_blocks_trusted_pass_without_candidate_blockers(self):
        ident = self.use_actual_scope_with_registered_gap("critical")
        self.assertEqual([], self.candidate["blockers"])
        self.assertEqual(1, self.scope[ident]["registeredCriticalSoftwareCount"])
        result = self.assert_rejected(ident + ":registered-critical-software")
        task = next(t for t in result["tasks"] if t["id"] == ident)
        self.assertTrue(task["executed"] and task["sourceReviewed"] and task["engineeringReviewed"])
        self.assertFalse(task["accepted"])
        self.assertEqual([], result["errors"])

    def test_noncritical_future_adapter_and_generic_deferred_register_markers_do_not_block_s0(self):
        ident = self.use_actual_scope_with_registered_gap("noncritical")
        self.assertEqual(0, self.scope[ident]["registeredCriticalSoftwareCount"])
        self.assertTrue(self.result()["accepted"])

    def test_critical_later_foundation_gap_does_not_expand_original_s0_exit_scope(self):
        ident = self.use_actual_scope_with_registered_gap("critical", outside_s0=True)
        self.assertNotIn(ident, self.scope)
        self.assertTrue(self.result()["accepted"])


if __name__ == "__main__":
    unittest.main()
