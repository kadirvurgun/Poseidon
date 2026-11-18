"""PLAN-1511-5803: merge observed host endpoint facts into the endpoint profile registry.

Usage:
  1. Dump observed facts from the actual host:
       $env:POSEIDON_ENDPOINT_PROFILE_OBSERVED_OUT = "<scratch>/observed.json"
       dotnet run --project tests/Poseidon.Api.Tests -- -method "*Every_mapped_endpoint_has_exactly_one*"
  2. py scripts/endpoint_profiles.py <scratch>/observed.json

Observed facts (operationId, authorization, request media/schema, concurrency evidence,
S1 security operation) are refreshed from the host. Curated fields (profileId,
deviations) are preserved. A new endpoint receives profileId "UNASSIGNED", which the
API refuses to load: 58.03 paragraph:8 forbids publishing an endpoint without an exact
reviewed profile. Removed endpoints are dropped. No model or network calls.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "contracts" / "endpoint-profiles.v1.json"
OBSERVED_FIELDS = ["method", "normalizedPath", "operationId", "anonymous", "authorizationPolicies",
                   "requestContentTypes", "requestSchema", "concurrencyEvidence", "securityOperationId", "owner"]


def merge(observed, contract, bootstrap=None):
    current = {(e["method"], e["normalizedPath"]): e for e in contract["endpoints"]}
    merged = []
    for fact in observed:
        key = (fact["method"], fact["normalizedPath"])
        previous = current.get(key)
        entry = {name: fact[name] for name in OBSERVED_FIELDS}
        if previous:
            entry["profileId"] = previous["profileId"]
            entry["deviations"] = previous.get("deviations", [])
        else:
            entry["profileId"] = bootstrap(fact) if bootstrap else "UNASSIGNED"
            entry["deviations"] = []
        merged.append(entry)
    merged.sort(key=lambda e: (e["normalizedPath"], e["method"]))
    order = ["method", "normalizedPath", "operationId", "profileId", "anonymous", "authorizationPolicies",
             "requestContentTypes", "requestSchema", "concurrencyEvidence", "securityOperationId", "owner", "deviations"]
    contract["endpoints"] = [{name: e[name] for name in order} for e in merged]
    return contract


def write(contract):
    lines = json.dumps({k: v for k, v in contract.items() if k != "endpoints"}, ensure_ascii=False, indent=2).rstrip("}\n").rstrip()
    # One endpoint per line keeps parallel additions reviewable and merge-friendly.
    body = ",\n".join("    " + json.dumps(e, ensure_ascii=False, separators=(", ", ": ")) for e in contract["endpoints"])
    CONTRACT.write_text(lines + ",\n  \"endpoints\": [\n" + body + "\n  ]\n}\n", encoding="utf-8", newline="\n")


def main(argv):
    if len(argv) != 2:
        print(__doc__)
        return 2
    observed = json.loads(Path(argv[1]).read_text(encoding="utf-8"))
    contract = json.loads(CONTRACT.read_text(encoding="utf-8"))
    contract = merge(observed, contract)
    write(contract)
    unassigned = [f"{e['method']} {e['normalizedPath']}" for e in contract["endpoints"] if e["profileId"] == "UNASSIGNED"]
    for item in unassigned:
        print("UNASSIGNED", item)
    print(f"endpoints={len(contract['endpoints'])} unassigned={len(unassigned)}")
    return 1 if unassigned else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
