"""Trusted local DoR/DoD register diagnostics, never an approval service.

Draft authoring remains possible with recorded dependency/source decisions and
DEFERRED execution. Publication acceptance remains independently gated.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
from source_contracts import SourceRecords, local_file

IMPACTS = {"design", "security", "offline", "audit", "regulatory", "docs", "test"}
CHECKS = {"unit", "integration", "contract", "shipE2E", "tugE2E", "security", "offlineChaos", "regulatoryRegression", "aiEvaluation", "usability", "disasterRecovery"}
SHA = re.compile(r"[a-f0-9]{40}\Z")


def dependency_ids(cell):
    """Read literal backlog IDs and inclusive ranges without dropping middle tasks."""
    if not isinstance(cell, str):
        raise ValueError("dependency_source")
    if not cell.strip():
        return set()
    result = set()
    for token in cell.split(","):
        match = re.fullmatch(r"DEV-([0-9]{3})(?::DEV-([0-9]{3}))?", token.strip())
        if not match:
            raise ValueError("dependency_source")
        start = int(match[1]); end = int(match[2] or match[1])
        if start < 1 or end < start:
            raise ValueError("dependency_source")
        result.update(f"DEV-{number:03d}" for number in range(start, end + 1))
    return result


def validate(register, root):
    errors = set(); pending = set()
    sources = SourceRecords(register.get("sources", {}), root)
    template_path = register.get("template", "")
    if not local_file(root, template_path):
        errors.add("missing_template")
    else:
        template = json.loads((root / template_path).read_text(encoding="utf-8"))
        if template.get("schemaVersion") != "poseidon.task-template.v1" or set(template.get("requiredImpacts", [])) != IMPACTS or set(template.get("requiredCheckAssessments", [])) != CHECKS:
            errors.add("template_contract")
        for ref in template.get("sourceCriteria", []):
            try: sources.resolve(ref)
            except (OSError, ValueError, KeyError): errors.add("template_source")
    tasks = register.get("tasks", [])
    ids = [t.get("id") for t in tasks]
    if not tasks or len(ids) != len(set(ids)):
        errors.add("duplicate_or_missing_task_id")
    for task in tasks:
        ident = task.get("id", "missing")
        def error(reason): errors.add(f"{ident}:{reason}")
        for key in ("id", "source", "acceptanceCriteria", "dependencies", "ownerRole", "authoringScope", "references", "checks", "pendingDecisions", "unresolvedSoftware"):
            if key not in task or key != "dependencies" and task[key] is None:
                error("missing_" + key)
        if not task.get("ownerRole") or not task.get("acceptanceCriteria") or not task.get("authoringScope"):
            error("missing_authoring_definition")
        try:
            source_text = sources.resolve(task["source"])
            if task["source"].get("column") != "A" or task["acceptanceSource"].get("column") != "G" or task["traceSource"].get("column") != "M" or len({(task[k]["source"], task[k]["locator"]) for k in ("source", "acceptanceSource", "traceSource")}) != 1:
                error("source_mismatch")
            dependency_cell = re.search(r"(?:^| \| )L[0-9]+: (.*?)(?= \| [A-Z]+[0-9]+: |$)", source_text)
            expected_dependencies = dependency_ids(dependency_cell.group(1) if dependency_cell else "")
            actual_dependencies = [d["id"] for d in task.get("dependencies", [])]
            if set(actual_dependencies) != expected_dependencies or len(actual_dependencies) != len(set(actual_dependencies)):
                error("dependency_inventory")
            sources.resolve(task["acceptanceSource"])
            sources.resolve(task["traceSource"])
            if task.get("traceId") != task["traceSource"]["value"]:
                error("source_trace_mismatch")
            if task["source"].get("value") != ident or task["acceptanceCriteria"] != task["acceptanceSource"]["value"]:
                error("source_mismatch")
            for dep in task.get("dependencies", []):
                sources.resolve(dep["source"])
                if dep["id"] != dep["source"].get("value") or not dep.get("disposition"):
                    error("dependency_definition")
        except (OSError, ValueError, KeyError):
            error("broken_source_reference")
        impacts = task.get("impacts", {})
        if set(impacts) != IMPACTS:
            error("missing_impact_assessment")
        for impact in impacts.values():
            if impact.get("disposition") not in {"assessed", "not_applicable"} or not impact.get("reason") or not impact.get("references"):
                error("incomplete_impact_assessment")
            for name in impact.get("references", []):
                if not local_file(root, name): error("broken_reference")
        refs = task.get("references", {})
        if not refs.get("source") or not refs.get("code") or not refs.get("tests") or not refs.get("docs"):
            error("missing_authored_references")
        for names in refs.values():
            for name in names:
                if not local_file(root, name): error("broken_reference")
        checks = task.get("checks", {})
        if set(checks) != CHECKS:
            error("missing_check_assessment")
        for name, check in checks.items():
            state = check.get("state")
            run, passed = check.get("runCount", 0), check.get("passedCount", 0)
            if type(run) is not int or type(passed) is not int or not 0 <= passed <= run:
                error("contradictory_execution_counts"); continue
            if state not in {"DEFERRED", "NOT_RUN", "RUN", "FAIL", "PASS", "NOT_APPLICABLE"} or not check.get("reason"):
                error("invalid_check_state")
            if state in {"DEFERRED", "NOT_RUN", "NOT_APPLICABLE"} and (run or passed or check.get("executionEvidence")):
                error("deferred_is_not_execution")
            if state == "NOT_APPLICABLE" and not check.get("scopeJustification"):
                error("unjustified_exclusion")
            if state == "PASS" and (not check.get("authored") or run == 0 or passed != run or not check.get("executionEvidence") or not SHA.fullmatch(check.get("targetCommit", ""))):
                error("missing_pass_evidence")
            for receipt in check.get("executionEvidence", []):
                if not local_file(root, receipt.get("path", "")):
                    error("broken_execution_evidence")
                elif hashlib.sha256((root / receipt["path"]).read_bytes()).hexdigest() != receipt.get("sha256"):
                    error("changed_execution_evidence")
            if state not in {"PASS", "NOT_APPLICABLE"}:
                pending.add(f"{ident}:execution-{name}")
        for gate in task.get("unresolvedSoftware", []):
            if not gate.get("description") or gate.get("severity") not in {"critical", "noncritical"}:
                error("unscoped_software_gap")
            pending.add(f"{ident}:unresolved-software")
        for decision in task.get("pendingDecisions", []):
            if not all(decision.get(k) for k in ("scope", "ownerRole", "description")):
                error("unscoped_decision")
        accepted = task.get("acceptanceState") == "ACCEPTED"
        if accepted:
            if any(c.get("state") not in {"PASS", "NOT_APPLICABLE"} for c in checks.values()): error("accepted_with_unexecuted_checks")
            if any(g.get("severity") == "critical" for g in task.get("unresolvedSoftware", [])): error("accepted_with_critical_software")
            if task.get("pendingDecisions"): error("accepted_with_pending_decisions")
            evidence = task.get("independentEvidence", [])
            if not evidence: error("missing_independent_evidence")
            # Files/hashes are necessary evidence identities, not proof of reviewer
            # authority. Trust validation and exact-target approval stay external.
            for receipt in evidence:
                if not local_file(root, receipt.get("path", "")) or not SHA.fullmatch(receipt.get("targetCommit", "")) or receipt.get("role") not in {"independent-QA", "independent-source-review"}:
                    error("invalid_independent_evidence")
                elif hashlib.sha256((root / receipt["path"]).read_bytes()).hexdigest() != receipt.get("sha256"):
                    error("changed_independent_evidence")
            targets = {c.get("targetCommit") for c in checks.values() if c.get("state") == "PASS"}
            if len(targets) != 1 or any(r.get("targetCommit") not in targets for r in evidence): error("target_commit_mismatch")
            pending.add(f"{ident}:external-independent-authority-validation")
        elif task.get("acceptanceState") != "NOT_ACCEPTED":
            error("invalid_acceptance_state")
    return {"errors": sorted(errors), "pendingGates": sorted(pending), "accepted": False,
            "meaning": "definition diagnostics only; no source, safety or publishing authority"}


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("register", type=Path)
    args = parser.parse_args()
    try:
        if args.register.stat().st_size > 1024 * 1024: raise ValueError("register_budget")
        result = validate(json.loads(args.register.read_text(encoding="utf-8")), Path(__file__).resolve().parents[1])
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        result = {"errors": ["invalid_register"], "accepted": False}
    print(json.dumps(result, sort_keys=True))
    return int(bool(result["errors"]))

if __name__ == "__main__": raise SystemExit(main())
