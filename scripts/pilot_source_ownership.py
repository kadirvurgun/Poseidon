"""DEV-008 typed candidate responsibility plan, never legal approval or activation.

Validates only controlled candidate declarations and internal role-plan structure.
An independent exact-target review remains external to this authored artifact.
Diagnostics are fixed codes; no input strings, identities or secrets are printed.
"""
import argparse
import hashlib
import json
from pathlib import Path

from source_contracts import SourceRecords

BASELINE = "poseidon-v46.3-20261030"
PREFIX = "analiz/POSEIDON GUNCEL 30.10.2026/"
SOURCES = {
    "register": {"path": PREFIX + "03_REQUIREMENTS_REGULATORY/08.01_POSEIDON_Regulatory_Requirement_Traceability_Matrix_TR.xlsx",
                 "sha256": "c36c72dec31efe45d1f894214b10708a017d0c8a5dfb96fe0d3e1498d44145da"},
    "backlog": {"path": PREFIX + "06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx",
                "sha256": "09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674"},
    "start": {"path": PREFIX + "00_START_HERE/00.01_POSEIDON_v46.3_START_HERE_Controlled_Candidate_TR.docx",
              "sha256": "4cf7f9872a50d81d7cbc15c748965e139555bdb3bc5b859706331fb273b683b0"},
}
SCOPES = {"ISM": [(2, "SRC-ISM")], "SOLAS": [(3, "SRC-SOLAS")], "STCW": [(4, "SRC-STCW")],
          "MLC": [(5, "SRC-MLC")], "MARPOL": [(6, "SRC-MARPOL")],
          "Flag": [(15, "SRC-TR-ISM-2023-19")],
          "Class": [(17, "SRC-IACS-M79"), (18, "SRC-TL-OCEAN-TOW"), (19, "SRC-TL-BP")]}
VALIDATION = {"state": "UNVERIFIED", "editionResolution": "OPEN", "applicabilityResolution": "OPEN",
              "reviewerQualification": "UNVERIFIED", "activationBlocked": True}
REVIEW = {"state": "SEPARATE_EXACT_TARGET_REVIEW_REQUIRED", "decision": None, "evidence": None}
OPEN_WORK = ["AUTHORITATIVE_EDITION", "PILOT_APPLICABILITY", "REVIEWER_QUALIFICATION"]


def keys(value, expected):
    return isinstance(value, dict) and set(value) == set(expected)


def load(path):
    def unique(pairs):
        result = {}
        for name, value in pairs:
            if name in result:
                raise ValueError("duplicate_key")
            result[name] = value
        return result
    def reject_constant(_):
        raise ValueError("nonfinite_input")
    if Path(path).stat().st_size > 128 * 1024:
        raise ValueError("input_budget")
    return json.loads(Path(path).read_text(encoding="utf-8"), object_pairs_hook=unique,
                      parse_constant=reject_constant)


def validate(plan, root):
    errors = set()
    required = {"schemaVersion", "baselineId", "planRevision", "status", "assignmentKind", "activationBlocked",
                "review", "sources", "requirement", "acceptance", "test", "laterGates", "scopes"}
    result = lambda: {"errors": sorted(errors), "definitionValid": not errors, "accepted": False,
                      "reviewRequired": True, "activationAllowed": False}
    if not keys(plan, required):
        errors.add("plan_shape"); return result()
    if (plan["schemaVersion"] != "poseidon.pilot-source-ownership.v1" or plan["baselineId"] != BASELINE or
            type(plan["planRevision"]) is not int or plan["planRevision"] != 1):
        errors.add("plan_identity")
    if (plan["status"] != "AUTHORED_UNVERIFIED" or plan["assignmentKind"] != "INTERNAL_ROLE_PLAN_ONLY" or
            plan["activationBlocked"] is not True or plan["review"] != REVIEW):
        errors.add("approval_or_activation_claim")
    if plan["sources"] != SOURCES:
        errors.add("source_pins"); return result()
    root = root.resolve()
    try:
        active = json.loads((root / "docs/sources/active.json").read_text(encoding="utf-8"))
        if active["baseline_id"] != BASELINE or active["source_root"] != PREFIX.rstrip("/"):
            errors.add("active_baseline")
        for source in SOURCES.values():
            path = (root / source["path"]).resolve()
            if (not path.is_relative_to(root) or not path.is_file() or path.stat().st_size > 32 * 1024 * 1024 or
                    hashlib.sha256(path.read_bytes()).hexdigest() != source["sha256"]):
                errors.add("source_bytes")
    except (OSError, ValueError, KeyError, TypeError):
        errors.add("source_bytes")
    if errors & {"source_bytes", "active_baseline"}:
        return result()
    records = SourceRecords(SOURCES, root)

    def reference(value, source, locator, column=None, expected=None):
        expected_keys = {"source", "locator", "value"} | ({"column"} if column else set())
        try:
            if (not keys(value, expected_keys) or value["source"] != source or value["locator"] != locator or
                    value.get("column") != column or not isinstance(value["value"], str) or not value["value"] or
                    expected is not None and value["value"] != expected):
                raise ValueError("reference")
            records.resolve(value)
        except (OSError, ValueError, KeyError, TypeError, AttributeError):
            errors.add("source_reference")

    reference(plan["requirement"], "backlog", "sheet:Backlog/row:9", "A", "DEV-008")
    reference(plan["acceptance"], "backlog", "sheet:Backlog/row:9", "G")
    reference(plan["test"], "backlog", "sheet:Backlog/row:9", "M", "T-S0-008")
    gates = plan["laterGates"]
    if not isinstance(gates, list) or len(gates) != 3:
        errors.add("later_gate_inventory")
    else:
        reference(gates[0], "backlog", "sheet:Backlog/row:74", "A", "DEV-073")
        reference(gates[1], "backlog", "sheet:Backlog/row:94", "A", "DEV-093")
        reference(gates[2], "start", "paragraph:116")
    scopes = plan["scopes"]
    if (not isinstance(scopes, list) or len(scopes) != 7 or
            any(not isinstance(scope, dict) or not isinstance(scope.get("scope"), str) for scope in scopes) or
            {scope["scope"] for scope in scopes} != set(SCOPES)):
        errors.add("scope_inventory"); return result()
    for scope in scopes:
        if not keys(scope, {"scope", "ownerRole", "reviewerRole", "selection", "validation", "openWork", "candidateSources"}):
            errors.add("scope_shape"); continue
        name = scope["scope"]
        owner, reviewer = scope["ownerRole"], scope["reviewerRole"]
        if (not keys(owner, {"role", "assignmentKind", "source"}) or owner["role"] != "Regulatory Lead" or
                owner["assignmentKind"] != "PLAN_ROLE_ONLY"):
            errors.add("owner_role_plan")
        else:
            reference(owner["source"], "backlog", "sheet:Backlog/row:9", "K", owner["role"])
        if (not keys(reviewer, {"role", "assignmentKind", "source", "responsibility"}) or
                reviewer["role"] != "Regulatory SME" or reviewer["assignmentKind"] != "PLAN_ROLE_ONLY"):
            errors.add("reviewer_role_plan")
        else:
            reference(reviewer["source"], "backlog", "sheet:RACI/row:1", "I", reviewer["role"])
            reference(reviewer["responsibility"], "backlog", "sheet:RACI/row:4", "I", "A/R")
        conditional = name in {"Flag", "Class"}
        if scope["selection"] != {"kind": "PILOT_FLAG_OR_CLASS_REQUIRED" if conditional else "PILOT_APPLICABILITY_REVIEW_REQUIRED",
                                  "state": "UNRESOLVED", "selectedOperationalContext": None}:
            errors.add("invented_operational_binding")
        if scope["validation"] != VALIDATION or scope["validation"].get("activationBlocked") is not True:
            errors.add("validation_or_activation_claim")
        work = scope["openWork"]
        if (not keys(work, {"identityKind", "items", "nextGate"}) or
                work["identityKind"] != "DERIVED_PLAN_TRACKING_ONLY" or work["items"] != OPEN_WORK):
            errors.add("open_work_inventory")
        else:
            reference(work["nextGate"], "backlog", "sheet:Backlog/row:94" if conditional else "sheet:Backlog/row:74",
                      "A", "DEV-093" if conditional else "DEV-073")
        candidates = scope["candidateSources"]
        expected = SCOPES[name]
        if not isinstance(candidates, list) or len(candidates) != len(expected):
            errors.add("candidate_inventory"); continue
        for candidate, (number, identity) in zip(candidates, expected):
            if not keys(candidate, {"sourceId", "issuer", "instrument", "lifecycleAssertion", "sourceUrl", "controlledNote", "version"}):
                errors.add("candidate_shape"); continue
            locator = f"sheet:Kaynak Register/row:{number}"
            for field, column in {"sourceId": "A", "issuer": "B", "instrument": "C", "lifecycleAssertion": "F", "sourceUrl": "G", "controlledNote": "H"}.items():
                reference(candidate[field], "register", locator, column, identity if field == "sourceId" else None)
            version = candidate["version"]
            if (not keys(version, {"kind", "baselineId", "snapshotSha256", "locator", "declaration", "authoritativeEdition", "resolution"}) or
                    version["kind"] != "CANDIDATE_DECLARATION_AND_SNAPSHOT" or version["baselineId"] != BASELINE or
                    version["snapshotSha256"] != SOURCES["register"]["sha256"] or version["locator"] != locator or
                    version["authoritativeEdition"] is not None or version["resolution"] != "OPEN" or
                    version["declaration"] != candidate["instrument"]):
                errors.add("candidate_version")
            else:
                reference(version["declaration"], "register", locator, "C")
    return result()


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("plan")
    args = parser.parse_args()
    try:
        result = validate(load(args.plan), Path(__file__).resolve().parents[1])
    except (OSError, ValueError, KeyError, TypeError, AttributeError, RecursionError):
        result = {"errors": ["invalid_input"], "definitionValid": False, "accepted": False,
                  "reviewRequired": True, "activationAllowed": False}
    print(json.dumps(result, sort_keys=True))
    return 0 if result["definitionValid"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
