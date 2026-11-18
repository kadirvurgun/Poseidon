"""PLAN-1511-5803: merge observed host endpoint facts into the endpoint profile registry.

Usage:
  1. Dump observed facts from the actual host:
       $env:POSEIDON_ENDPOINT_PROFILE_OBSERVED_OUT = "<scratch>/observed.json"
       dotnet run --project tests/Poseidon.Api.Tests -- -method "*Every_mapped_endpoint_has_exactly_one*"
  2. py scripts/endpoint_profiles.py <scratch>/observed.json

Observed facts (operationId, authorization, request media/schema, concurrency evidence,
S1 security operation, owner) are refreshed from the host. Curated fields (profileId,
deviations) are preserved. A new endpoint receives profileId "UNASSIGNED", which the
API refuses to load and the host refuses to start with: 58.03 paragraph:8 forbids
publishing an endpoint without an exact reviewed profile. Profile assignment is always
a reviewed edit; this script never chooses one. Removed endpoints are dropped.
Exit code 1 when any endpoint is UNASSIGNED. No model or network calls.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "contracts" / "endpoint-profiles.v1.json"
UNASSIGNED = "UNASSIGNED"
OBSERVED_FIELDS = ["method", "normalizedPath", "operationId", "anonymous", "authorizationPolicies",
                   "requestContentTypes", "requestSchema", "concurrencyEvidence", "securityOperationId", "owner"]
ENTRY_ORDER = ["method", "normalizedPath", "operationId", "profileId", "anonymous", "authorizationPolicies",
               "requestContentTypes", "requestSchema", "concurrencyEvidence", "securityOperationId", "owner", "deviations"]


def merge(observed, contract):
    current = {(e["method"], e["normalizedPath"]): e for e in contract["endpoints"]}
    merged = []
    for fact in observed:
        previous = current.get((fact["method"], fact["normalizedPath"]))
        entry = {name: fact[name] for name in OBSERVED_FIELDS}
        entry["profileId"] = previous["profileId"] if previous else UNASSIGNED
        entry["deviations"] = list(previous.get("deviations", [])) if previous else []
        merged.append(entry)
    merged.sort(key=lambda e: (e["normalizedPath"], e["method"]))
    contract["endpoints"] = [{name: e[name] for name in ENTRY_ORDER} for e in merged]
    return contract


def render(contract):
    """Serialize with the contract's own key order; one endpoint per line keeps parallel
    additions reviewable and merge-friendly. Round-trips the committed file byte for byte."""
    parts = []
    for key, value in contract.items():
        if key == "endpoints" and value:
            body = ",\n".join("    " + json.dumps(e, ensure_ascii=False, separators=(", ", ": ")) for e in value)
            rendered = "[\n" + body + "\n  ]"
        else:
            rendered = json.dumps(value, ensure_ascii=False, indent=2).replace("\n", "\n  ")
        parts.append("  " + json.dumps(key, ensure_ascii=False) + ": " + rendered)
    return "{\n" + ",\n".join(parts) + "\n}\n"


def unassigned(contract):
    return [f"{e['method']} {e['normalizedPath']}" for e in contract["endpoints"] if e["profileId"] == UNASSIGNED]


def main(argv, contract_path=CONTRACT):
    if len(argv) != 2:
        print(__doc__)
        return 2
    observed = json.loads(Path(argv[1]).read_text(encoding="utf-8"))
    contract = merge(observed, json.loads(Path(contract_path).read_text(encoding="utf-8")))
    Path(contract_path).write_text(render(contract), encoding="utf-8", newline="\n")
    missing = unassigned(contract)
    for item in missing:
        print(UNASSIGNED, item)
    print(f"endpoints={len(contract['endpoints'])} unassigned={len(missing)}")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
