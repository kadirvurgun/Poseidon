"""S0 engineering exit evaluation; never creates approvals or publishes anything.

Versioned source contracts define scope. Exact-target evidence is authenticated by
an explicitly trusted orchestration/review provider, not labels in the candidate.
A coordinator may bind actual independent session results without inventing
cryptographic rollout, human appointments or safety approvals.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile

from source_contracts import SourceRecords
from task_contracts import CHECKS, IMPACTS, validate as validate_task_definitions

SHA256 = re.compile(r"[a-f0-9]{64}\Z")
GIT_ID = re.compile(r"[a-f0-9]{40}\Z")
PRINCIPAL = re.compile(r"[A-Za-z0-9/][A-Za-z0-9:@._/-]{0,255}\Z")
KINDS = {"execution", "source-review", "engineering-review", "acceptance"}
TASK_IDS = {f"DEV-{n:03}" for n in range(1, 13)} | {"DEV-116"}
MAX_RECEIPT = 256 * 1024
MAX_ARTIFACT = 16 * 1024 * 1024


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def error_code(exc, fallback):
    value = str(exc)
    return value if isinstance(exc, ValueError) and re.fullmatch(r"[a-z][a-z0-9_]{0,95}", value) else fallback


def read_json(path, limit):
    if path.stat().st_size > limit:
        raise ValueError("json_budget")
    with path.open("rb") as stream:
        raw = stream.read(limit + 1)
    if len(raw) > limit:
        raise ValueError("json_budget")
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate_json_key")
            result[key] = value
        return result
    return raw, json.loads(raw.decode("utf-8"), object_pairs_hook=unique)


def safe_path(root, name):
    if not isinstance(name, str) or not name or len(name) > 512 or "\\" in name:
        raise ValueError("artifact_path")
    if name.startswith("/") or ":" in name or any(p in {"", ".", ".."} for p in name.split("/")):
        raise ValueError("artifact_path")
    path = (root / name).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError("artifact_path")
    # A receipt must not traverse a symlink/reparse alias to private material.
    current = root.resolve()
    for part in name.split("/"):
        current /= part
        if current.is_symlink() or getattr(current, "is_junction", lambda: False)():
            raise ValueError("artifact_alias")
    return path


def dependencies(cell):
    """Expand the actual DEV-001:DEV-011 source range, never just its endpoints."""
    result = set()
    for term in cell.split(",") if cell else []:
        match = re.fullmatch(r"DEV-([0-9]{3})(?::DEV-([0-9]{3}))?", term.strip())
        if not match:
            raise ValueError("dependency_syntax")
        start, end = int(match[1]), int(match[2] or match[1])
        if not 1 <= start <= end <= 121:
            raise ValueError("dependency_range")
        result.update(f"DEV-{n:03}" for n in range(start, end + 1))
    return sorted(result)


def source_scope(policy, root):
    if policy.get("schemaVersion") != "poseidon.s0-policy.v1" or policy.get("baseline") != "poseidon-v46.3-20261030":
        raise ValueError("policy_identity")
    active = read_json(root / "docs/sources/active.json", 65536)[1]
    if active.get("baseline_id") != policy["baseline"]:
        raise ValueError("inactive_source_baseline")
    manifest = read_json(safe_path(root, active["manifest"]), 1024 * 1024)[1]
    if manifest.get("baseline_id") != policy["baseline"]:
        raise ValueError("source_manifest_identity")
    manifest_files = {item["source_path"]: item["sha256"] for item in manifest["files"]}
    if any(manifest_files.get(source["path"]) != source["sha256"] for source in policy["sources"].values()):
        raise ValueError("source_manifest_mismatch")
    sources = SourceRecords(policy["sources"], root)
    rows = sources.records("backlog")
    tasks = {}
    for locator, text in rows.items():
        if not locator.startswith("sheet:Backlog/row:"):
            continue
        row = locator.rsplit(":", 1)[1]
        cells = dict(re.findall(r"(?:^| \| )([A-Z]+[0-9]+): (.*?)(?= \| [A-Z]+[0-9]+: |$)", text))
        if cells.get("J" + row) != "S0":
            continue
        ident = cells["A" + row]
        if ident in tasks:
            raise ValueError("duplicate_source_task")
        tasks[ident] = {"id": ident, "locator": locator, "acceptanceCriteria": cells["G" + row],
                        "traceId": cells["M" + row], "ownerRole": cells["K" + row],
                        "storyPoints": int(cells["P" + row]), "dependencies": dependencies(cells.get("L" + row, ""))}
    if set(tasks) != TASK_IDS or sum(t["storyPoints"] for t in tasks.values()) != 53:
        raise ValueError("s0_source_inventory_changed")
    if tasks["DEV-012"]["dependencies"] != [f"DEV-{n:03}" for n in range(1, 12)]:
        raise ValueError("exit_dependency_scope")
    template = read_json(root / policy["taskTemplate"], 65536)[1]
    checks = template.get("requiredCheckAssessments", [])
    if len(checks) != len(set(checks)) or set(checks) != CHECKS or set(checks) != set(policy["requiredCheckAssessments"]) or set(template.get("requiredImpacts", [])) != IMPACTS:
        raise ValueError("task_template_check_scope")
    if set(policy["pipelineChecks"]) != {"backendBuild", "frontendBuild", "lint", "artifact"} or len(policy["pipelineChecks"]) != 4:
        raise ValueError("source_pipeline_scope")
    if set(policy["nonExcludableChecks"]) != {"unit", "backendBuild", "frontendBuild", "lint", "artifact"} or len(policy["nonExcludableChecks"]) != 5:
        raise ValueError("source_pipeline_exclusion")
    register = read_json(root / policy["taskRegister"], 1024 * 1024)[1]
    if register.get("baseline") != policy["baseline"]:
        raise ValueError("task_register_baseline")
    register["tasks"] = [t for t in register["tasks"] if t["id"] in TASK_IDS]
    expected_defined = TASK_IDS - {"DEV-001", "DEV-002", "DEV-012"}
    if not expected_defined.issubset({t["id"] for t in register["tasks"]}) or validate_task_definitions(register, root)["errors"]:
        raise ValueError("invalid_s0_task_definitions")
    # The candidate is not the authority for known software gaps. A trusted PASS
    # receipt cannot erase a critical gap retained by the current S0 definition.
    # Generic deferred/review markers and noncritical future adapters remain
    # separate; only the original S0 rows' explicit critical software is coupled.
    for definition in register["tasks"]:
        tasks[definition["id"]]["registeredCriticalSoftwareCount"] = sum(
            gap.get("severity") == "critical" for gap in definition.get("unresolvedSoftware", []))
    return tasks


class GitTarget:
    """Inspect immutable git objects with bounded stdout and no shell evaluation."""
    def __init__(self, root):
        self.root = root.resolve()
        self.artifacts = {}
        self.artifact_bytes = 0

    def _git(self, *arguments, limit=MAX_ARTIFACT):
        with tempfile.TemporaryFile() as output:
            result = subprocess.run(["git", "-C", str(self.root), *arguments], stdout=output,
                                    stderr=subprocess.DEVNULL, timeout=30, check=False)
            if result.returncode or output.tell() > limit:
                raise ValueError("git_target_unavailable")
            output.seek(0)
            return output.read(limit + 1)

    def verify(self, target):
        if set(target) != {"commit", "tree"} or not all(GIT_ID.fullmatch(target.get(k, "")) for k in ("commit", "tree")):
            raise ValueError("target_identity")
        commit = self._git("rev-parse", "--verify", "HEAD^{commit}", limit=1024).decode().strip()
        tree = self._git("rev-parse", "--verify", target["commit"] + "^{tree}", limit=1024).decode().strip()
        if commit != target["commit"] or tree != target["tree"]:
            raise ValueError("stale_target")
        if self._git("status", "--porcelain=v1", "--untracked-files=all", limit=1024 * 1024):
            raise ValueError("dirty_target")

    def artifact(self, target, name):
        safe_path(self.root, name)
        identity = (target["commit"], name)
        if identity in self.artifacts:
            return self.artifacts[identity]
        if len(self.artifacts) >= 256:
            raise ValueError("target_artifact_budget")
        # Disallow symlink blobs even when the current checkout happens to hide it.
        mode = self._git("ls-tree", target["commit"], "--", name, limit=4096)
        if not mode.startswith((b"100644 blob ", b"100755 blob ")):
            raise ValueError("artifact_not_regular_git_file")
        size = int(self._git("cat-file", "-s", target["commit"] + ":" + name, limit=64).decode().strip())
        if size > MAX_ARTIFACT or self.artifact_bytes + size > 64 * 1024 * 1024:
            raise ValueError("target_artifact_budget")
        raw = self._git("show", target["commit"] + ":" + name)
        self.artifact_bytes += len(raw)
        if self.artifact_bytes > 64 * 1024 * 1024:
            raise ValueError("target_artifact_budget")
        self.artifacts[identity] = raw
        return raw


class TrustedOrchestratorAuthority:
    """Trust boundary for actual coordinator-observed session results.

    Construct this only from verified host/tool results. For standalone CLI use,
    a coordinator exports those observations outside the author-controlled repo
    and supplies its digest separately. A file hash is integrity, not identity:
    the caller must genuinely trust the coordinator export and provider. The
    candidate cannot select an issuer, appoint a reviewer or authenticate itself.
    """
    def __init__(self, observations):
        if observations.get("schemaVersion") != "poseidon.s0-orchestrator-authority.v1":
            raise ValueError("authority_identity")
        provider = observations.get("provider", {})
        if provider.get("kind") != "trusted-orchestrator" or not provider.get("instanceId") or not provider.get("coordinatorSessionId"):
            raise ValueError("trusted_provider_required")
        authors = observations.get("authorPrincipals", [])
        author_sessions = observations.get("authorSessionIds", [])
        if not authors or not author_sessions or len(authors) != len(set(authors)) or len(author_sessions) != len(set(author_sessions)) or not all(PRINCIPAL.fullmatch(p) for p in authors):
            raise ValueError("author_inventory_required")
        self.authors, self.author_sessions = set(authors), set(author_sessions)
        self.sessions = {}
        for session in observations.get("sessions", []):
            ident, principal = session["sessionId"], session["principal"]
            if not PRINCIPAL.fullmatch(ident) or not PRINCIPAL.fullmatch(principal) or ident in self.sessions or session.get("providerInstanceId") != provider["instanceId"]:
                raise ValueError("session_identity")
            if session.get("state") != "completed" or session.get("parentSessionId") != provider["coordinatorSessionId"]:
                raise ValueError("unverified_session_state")
            self.sessions[ident] = session

    @classmethod
    def from_coordinator_export(cls, path, expected_hash, root):
        path = Path(path).resolve()
        if path.is_relative_to(Path(root).resolve()) or not isinstance(expected_hash, str) or not SHA256.fullmatch(expected_hash):
            raise ValueError("external_coordinator_observation_required")
        raw, observations = read_json(path, 1024 * 1024)
        if digest(raw) != expected_hash:
            raise ValueError("changed_coordinator_observation")
        return cls(observations)

    def verify(self, receipt, raw):
        try:
            session = self.sessions[receipt["sessionId"]]
            principal, kind = session["principal"], receipt["kind"]
            if receipt["principal"] != principal or receipt["target"] != session["target"] or receipt["baseline"] != session["baseline"]:
                return False
            if kind != "execution" and (principal in self.authors or session["sessionId"] in self.author_sessions):
                return False
            reports = session.get("verifiedReports", [])
            return any(r.get("sha256") == digest(raw) and r.get("kind") == kind and r.get("taskId") == receipt["taskId"] and r.get("outcome") == "PASS" for r in reports)
        except (ValueError, KeyError, TypeError):
            return False


def timestamp(value):
    if not isinstance(value, str) or not value.endswith("Z"):
        raise ValueError("receipt_time")
    result = datetime.fromisoformat(value[:-1] + "+00:00")
    if result.utcoffset().total_seconds() != 0:
        raise ValueError("receipt_time")
    return result


def authenticated_receipt(ref, kind, task, candidate, policy, root, git, authority, now):
    if authority is None:
        raise ValueError("missing_external_authority")
    if set(ref) != {"path", "sha256"} or not SHA256.fullmatch(ref.get("sha256", "")):
        raise ValueError("receipt_identity")
    path = safe_path(root, ref["path"])
    raw, receipt = read_json(path, MAX_RECEIPT)
    if digest(raw) != ref["sha256"]:
        raise ValueError("changed_receipt")
    if receipt.get("schemaVersion") != "poseidon.s0-receipt.v1" or receipt.get("kind") != kind or receipt.get("taskId") != task["id"]:
        raise ValueError("receipt_scope")
    if receipt.get("target") != candidate["target"] or receipt.get("baseline") != policy["baseline"] or receipt.get("sourceSha256") != policy["sources"]["backlog"]["sha256"] or receipt.get("traceId") != task["traceId"]:
        raise ValueError("stale_receipt_target_or_source")
    issued = timestamp(receipt["issuedAt"])
    if issued > now:
        raise ValueError("future_receipt")
    if "expiresAt" in receipt:
        expires = timestamp(receipt["expiresAt"])
        if not issued <= now <= expires or expires <= issued:
            raise ValueError("expired_receipt")
    if receipt.get("outcome") != "PASS" or receipt.get("unresolvedP0") != 0 or type(receipt.get("unresolvedP0")) is not int:
        raise ValueError("receipt_not_pass")
    try:
        trusted = authority.verify(receipt, raw)
    except Exception as exc:
        raise ValueError("authority_provider_unavailable") from exc
    if trusted is not True:
        raise ValueError("untrusted_or_nonindependent_receipt")
    artifacts = receipt.get("artifacts", [])
    if not artifacts or len(artifacts) > 128:
        raise ValueError("receipt_artifact_inventory")
    seen = set()
    for artifact in artifacts:
        name = artifact["path"]
        if name in seen or not SHA256.fullmatch(artifact.get("sha256", "")):
            raise ValueError("artifact_identity")
        seen.add(name)
        if digest(git.artifact(candidate["target"], name)) != artifact["sha256"]:
            raise ValueError("changed_target_artifact")
    return receipt


def evaluate(candidate, policy, root, authority=None, git=None, now=None):
    """Return a fail-closed report. Local definition success is never acceptance."""
    errors, pending = set(), set()
    root = Path(root).resolve()
    git = git or GitTarget(root)
    now = now or datetime.now(timezone.utc)
    report = {"schemaVersion": "poseidon.s0-result.v1", "accepted": False,
              "phase": "S0", "scope": "engineering exit only; no production, legal or safety authority",
              "tasks": [], "errors": [], "pendingGates": []}
    try:
        tasks = source_scope(policy, root)
        if candidate.get("schemaVersion") != "poseidon.s0-candidate.v1" or candidate.get("baseline") != policy["baseline"]:
            raise ValueError("candidate_identity")
        report["target"] = candidate.get("target")
        target_valid = False
        try:
            git.verify(candidate.get("target", {}))
            target_valid = True
        except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as exc:
            pending.add(error_code(exc, "target_unavailable"))
        entries = candidate.get("tasks", [])
        if not isinstance(entries, list) or len(entries) != len(tasks) or {e["id"] for e in entries} != set(tasks):
            raise ValueError("candidate_task_inventory")
        by_id = {entry["id"]: entry for entry in entries}
        blockers = candidate.get("blockers")
        if not isinstance(blockers, list) or len(blockers) > 1000:
            raise ValueError("blocker_inventory_required")
        seen = set()
        for blocker in blockers:
            if not isinstance(blocker.get("id"), str) or not re.fullmatch(r"BLK-[A-Z0-9-]{1,48}", blocker["id"]) or blocker["id"] in seen or blocker.get("severity") not in {"P0", "P1", "P2", "P3"} or blocker.get("state") not in {"OPEN", "RESOLVED"}:
                raise ValueError("invalid_blocker")
            seen.add(blocker["id"])
            if blocker["severity"] == "P0" and blocker["state"] != "RESOLVED":
                pending.add("unresolved_P0:" + blocker["id"])
        if authority is None:
            pending.add("missing_external_authority")
        if candidate.get("externalBindings"):
            # Bindings are scoped by criterion; a contract-only environment task
            # is not automatically made dependent on a live production rollout.
            for binding in candidate["externalBindings"]:
                if binding.get("taskId") not in tasks or not binding.get("criterion") or not binding.get("ownerRole") or binding.get("state") not in {"MISSING", "PROVISIONED"}:
                    raise ValueError("external_binding_definition")
                if binding["state"] == "MISSING":
                    pending.add(binding["taskId"] + ":missing-external-binding")
        evidence_identities = set()
        for ident, task in sorted(tasks.items()):
            entry = by_id[ident]
            result = {**task, "authored": False, "executed": False, "sourceReviewed": False,
                      "engineeringReviewed": False, "accepted": False}
            registered_critical = task.get("registeredCriticalSoftwareCount", 0)
            if type(registered_critical) is not int or registered_critical < 0:
                raise ValueError("invalid_registered_software_gap_count")
            if registered_critical:
                pending.add(ident + ":registered-critical-software")
            authored = entry.get("authored", {})
            refs = authored.get("artifacts", [])
            if authored.get("state") == "AUTHORED" and isinstance(refs, list) and 0 < len(refs) <= 128:
                try:
                    paths = set()
                    for ref in refs:
                        if ref["path"] in paths or not SHA256.fullmatch(ref.get("sha256", "")):
                            raise ValueError("authored_artifact_identity")
                        paths.add(ref["path"])
                        if digest(git.artifact(candidate["target"], ref["path"])) != ref["sha256"]:
                            raise ValueError("changed_authored_artifact")
                    result["authored"] = True
                except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError):
                    errors.add(ident + ":invalid-authored-artifacts")
            else:
                pending.add(ident + ":not-authored")
            receipts = {}
            for kind in sorted(KINDS):
                ref = entry.get("evidence", {}).get(kind)
                if ref is None:
                    pending.add(ident + ":missing-" + kind)
                    continue
                try:
                    identity = (ref["path"], ref["sha256"])
                    if identity in evidence_identities:
                        raise ValueError("reused_receipt")
                    evidence_identities.add(identity)
                    receipts[kind] = authenticated_receipt(ref, kind, task, candidate, policy, root, git, authority, now)
                except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as exc:
                    errors.add(ident + ":" + kind + ":" + error_code(exc, "invalid_receipt"))
            result["sourceReviewed"] = "source-review" in receipts
            result["engineeringReviewed"] = "engineering-review" in receipts
            execution = receipts.get("execution")
            if execution:
                checks = execution.get("checks", {})
                exclusions = receipts.get("source-review", {}).get("excludedChecks", {})
                check_errors = []
                if set(checks) != set(policy["requiredCheckAssessments"] + policy["pipelineChecks"]) or set(exclusions) - set(checks):
                    check_errors.append("required_check_inventory")
                for name, check in checks.items():
                    run, passed, failed, skipped = (check.get(k) for k in ("runCount", "passedCount", "failedCount", "skippedCount"))
                    counts = all(type(n) is int and n >= 0 for n in (run, passed, failed, skipped))
                    if check.get("state") == "PASS":
                        if not counts or not run or passed != run or failed or skipped or check.get("exitCode") != 0 or type(check.get("exitCode")) is not int or not check.get("commandId") or name in exclusions:
                            check_errors.append("unexecuted_failed_or_skipped_check")
                    elif check.get("state") == "NOT_APPLICABLE":
                        if name in policy["nonExcludableChecks"] or not counts or any((run, passed, failed, skipped)) or not isinstance(exclusions.get(name), str) or not exclusions[name].strip():
                            check_errors.append("unreviewed_check_exclusion")
                    else:
                        check_errors.append("check_not_pass")
                if check_errors:
                    errors.update(ident + ":" + e for e in check_errors)
                else:
                    result["executed"] = True
            # The three independent decisions must explicitly bind the original
            # literal AC and task dependencies; a generic 'reviewed' note is insufficient.
            for kind in ("source-review", "engineering-review", "acceptance"):
                if kind in receipts:
                    receipt = receipts[kind]
                    if receipt.get("acceptanceCriteria") != task["acceptanceCriteria"] or receipt.get("dependencies") != task["dependencies"] or receipt.get("unresolvedBindings") != []:
                        errors.add(ident + ":" + kind + ":incomplete-literal-criterion")
                    expected = {k: by_id[ident].get("evidence", {}).get(k, {}).get("sha256") for k in KINDS - {kind}}
                    # Acceptance attests the exact execution + both review bytes,
                    # preventing replacement of a passing report after approval.
                    if kind == "acceptance" and receipt.get("evidenceDigests") != expected:
                        errors.add(ident + ":acceptance:evidence-digest-mismatch")
            result["accepted"] = target_valid and not registered_critical and result["authored"] and result["executed"] and result["sourceReviewed"] and result["engineeringReviewed"] and "acceptance" in receipts and not any(e.startswith(ident + ":") for e in errors)
            report["tasks"].append(result)
        resolved = {t["id"]: t for t in report["tasks"]}
        # Fixed point: an accepted downstream task cannot cover an open dependency.
        changed = True
        while changed:
            changed = False
            for task in report["tasks"]:
                missing = [d for d in task["dependencies"] if not resolved[d]["accepted"]]
                if missing:
                    pending.add(task["id"] + ":dependency-not-accepted:" + ",".join(missing))
                    if task["accepted"]:
                        task["accepted"] = False; changed = True
        report["progress"] = {"totalTasks": len(tasks), "totalStoryPoints": 53,
                              "authoredTasks": sum(t["authored"] for t in report["tasks"]),
                              "acceptedTasks": sum(t["accepted"] for t in report["tasks"]),
                              "acceptedStoryPoints": sum(t["storyPoints"] for t in report["tasks"] if t["accepted"])}
        report["accepted"] = not errors and not pending and all(t["accepted"] for t in report["tasks"])
    except (OSError, ValueError, KeyError, TypeError, AttributeError, RecursionError, subprocess.SubprocessError):
        errors.add("invalid_s0_contract_or_candidate")
    report["errors"], report["pendingGates"] = sorted(errors), sorted(pending)
    return report


def prepare_candidate(policy, root, git=None):
    """Generate an honest draft from source + existing references, never approvals.

    Presence of an implementation reference cannot establish literal-AC completion,
    so every task remains NOT_ASSESSED even when artifact hashes are populated.
    """
    root = Path(root).resolve()
    tasks = source_scope(policy, root)
    git = git or GitTarget(root)
    commit = git._git("rev-parse", "--verify", "HEAD^{commit}", limit=1024).decode().strip()
    tree = git._git("rev-parse", "--verify", commit + "^{tree}", limit=1024).decode().strip()
    target = {"commit": commit, "tree": tree}
    git.verify(target)
    register = read_json(root / policy["taskRegister"], 1024 * 1024)[1]
    definitions = {t["id"]: t for t in register["tasks"]}
    result = {"schemaVersion": "poseidon.s0-candidate.v1", "baseline": policy["baseline"], "target": target,
              "tasks": [], "blockers": [{"id": "BLK-S0-ASSESSMENT-PENDING", "severity": "P0", "state": "OPEN"}],
              "externalBindings": [], "status": "DRAFT_NOT_ASSESSED_NOT_ACCEPTED",
              "note": "Generated source definition and referenced artifact identities only. Coordinator must assess original scope, real P0/bindings and actual execution/independent review. No acceptance or passed check is generated."}
    for ident, task in sorted(tasks.items()):
        refs = definitions.get(ident, {}).get("references", {})
        names = sorted({name for category in ("code", "tests", "docs") for name in refs.get(category, [])})
        artifacts = [{"path": name, "sha256": digest(git.artifact(target, name))} for name in names]
        result["tasks"].append({"id": ident, "definition": task,
                                "authored": {"state": "NOT_ASSESSED", "artifacts": artifacts}, "evidence": {}})
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("--policy", type=Path, default=Path("contracts/s0/exit-policy.v1.json"))
    parser.add_argument("--authority", type=Path)
    parser.add_argument("--authority-sha256")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--prepare", action="store_true", help="write source-derived NOT_ASSESSED draft to candidate path; requires clean target")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    try:
        policy = read_json(args.policy, 65536)[1]
        if args.prepare:
            if args.authority is not None or args.authority_sha256 or args.output:
                raise ValueError("prepare_has_no_approval_inputs")
            prepared = prepare_candidate(policy, root)
            with args.candidate.open("x", encoding="utf-8") as stream:
                stream.write(json.dumps(prepared, ensure_ascii=False, indent=2) + "\n")
            print(json.dumps({"schemaVersion": "poseidon.s0-prepare-result.v1", "prepared": True, "accepted": False, "tasks": len(prepared["tasks"]), "meaning": "draft only, all tasks NOT_ASSESSED and execution/review absent"}))
            return 0
        authority = None
        if args.authority is not None:
            if not args.authority_sha256:
                raise ValueError("authority_pin_required")
            authority = TrustedOrchestratorAuthority.from_coordinator_export(args.authority, args.authority_sha256, root)
        candidate = read_json(args.candidate, 1024 * 1024)[1]
        result = evaluate(candidate, policy, root, authority)
    except (OSError, ValueError, KeyError, TypeError, RecursionError):
        result = {"schemaVersion": "poseidon.s0-result.v1", "accepted": False, "errors": ["invalid_gate_input_or_external_authority"], "pendingGates": []}
    output = json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.write_text(output, encoding="utf-8")
    print(output, end="")
    return 0 if result["accepted"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
