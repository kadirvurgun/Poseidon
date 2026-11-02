"""DEV-009 bounded deterministic dataset definition validator; run at bundle gate."""
import argparse
import json
from pathlib import Path
from source_contracts import SourceRecords

ENGINES = {"Applicability", "RequiredData", "CredentialDates", "Freshness", "AssetHierarchy", "Maintenance", "OfflineConflict"}
PROFILE_IDS = {"GP-SHIP-OIL-TR", "GP-SHIP-CHEM", "GP-SHIP-DG", "GP-TUG-HARBOUR", "GP-TUG-ESCORT", "GP-TUG-OCEAN", "SYN-GENERAL-CARGO"}

def validate(data, root):
    errors = set()
    if data.get("schemaVersion") != "poseidon.golden-pilot.v1" or data.get("referenceTime") != "2026-10-04T00:00:00Z" or data.get("syntheticOnly") is not True:
        errors.add("dataset_identity")
    records = SourceRecords(data.get("sources", {}), root)
    profiles = data.get("profiles", [])
    if {p.get("id") for p in profiles} != PROFILE_IDS or len(profiles) != 7:
        errors.add("profile_inventory")
    cases = data.get("sourceCases", [])
    if {c.get("id") for c in cases} != {f"GOLD-{i:03}" for i in range(1, 11)} or len(cases) != 10:
        errors.add("golden_inventory")
    for profile in profiles:
        if profile.get("id") != "SYN-GENERAL-CARGO":
            for column, value in profile.get("facts", {}).items():
                try: records.resolve({**profile["source"], "column": column, "value": value})
                except (OSError, ValueError, KeyError): errors.add("profile_source_mismatch")
    for item in profiles + cases + data.get("consumerCases", []):
        for key in ("source", "spec", "expectedSource"):
            if key in item:
                try:
                    records.resolve(item[key])
                except (OSError, ValueError, KeyError):
                    errors.add("source_reference")
        if item in cases:
            for key, column in (("id", "A"), ("profile", "B"), ("condition", "C"), ("rule", "D"), ("expectedApplicability", "E"), ("expectedControl", "F")):
                try: records.resolve({**item["source"], "column": column, "value": item[key]})
                except (OSError, ValueError, KeyError): errors.add("source_case_mismatch")
            if item.get("source", {}).get("locator") != item.get("expectedSource", {}).get("locator"):
                errors.add("source_case_mismatch")
            if item.get("adapter") != "UNAVAILABLE" or item.get("execution") != "NOT_RUN":
                errors.add("fabricated_adapter_or_execution")
            if item.get("expectedApplicability") != item.get("expectedSource", {}).get("value"):
                errors.add("source_expectation_mismatch")
    consumers = data.get("consumerCases", [])
    if not 1 <= len(consumers) <= 128 or len({c.get("id") for c in consumers}) != len(consumers):
        errors.add("consumer_inventory")
    if not {"crew", "certificate", "asset", "defect", "evidence", "applicability"} <= {c.get("area") for c in consumers}:
        errors.add("missing_edge_coverage")
    for case in consumers:
        if case.get("engine") not in ENGINES or case.get("profile") not in PROFILE_IDS or not case.get("inputs") or not case.get("expected"):
            errors.add("consumer_contract")
        if case.get("expectedReadiness") != "NO_POSITIVE_READINESS_INFERRED" or case.get("execution") != "NOT_RUN":
            errors.add("fabricated_readiness_or_execution")
    if not data.get("productionAdapterGaps"):
        errors.add("hidden_adapter_gaps")
    return {"errors": sorted(errors), "accepted": False, "execution": "NOT_RUN", "productionAdapters": "UNAVAILABLE"}


def load(path):
    if path.stat().st_size > 512 * 1024:
        raise ValueError("dataset_budget")
    return json.loads(path.read_text(encoding="utf-8"))


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("dataset", type=Path)
    args = parser.parse_args()
    try:
        result = validate(load(args.dataset), Path(__file__).resolve().parents[1])
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        result = {"errors": ["invalid_dataset"], "accepted": False}
    print(json.dumps(result, sort_keys=True))
    return int(bool(result["errors"]))

if __name__ == "__main__":
    raise SystemExit(main())
