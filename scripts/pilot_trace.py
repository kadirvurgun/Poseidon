"""DEV-007 source-addressed trace integrity, without manufacturing gate evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import re
from source_contracts import SourceRecords

SHA = re.compile(r"\A[a-f0-9]{64}\Z")
STATES = {"PLANNED", "DEFINED", "AUTHORED", "RUN", "PASS"}
KINDS = {"requirement", "screen", "api", "entity", "backlog", "test"}


def validate(package, root):
    errors = set(); gaps = set(); implementation = set(); execution = set()
    sources = package.get("sources", {})
    if not sources:
        errors.add("missing_sources")
    for key, source in sources.items():
        path = source.get("path", "")
        p = root / path
        if not path.startswith("analiz/POSEIDON GUNCEL 30.10.2026/") or not p.resolve().is_relative_to(root.resolve()) or not SHA.fullmatch(source.get("sha256", "")):
            errors.add("source_identity")
        elif not p.is_file() or hashlib.sha256(p.read_bytes()).hexdigest() != source["sha256"]:
            errors.add("source_bytes")
    capabilities = package.get("capabilities", [])
    ids = [c.get("number") for c in capabilities]
    if sorted(ids) != list(range(1, 11)):
        errors.add("capability_inventory")
    records = SourceRecords(sources, root)
    node_ids = set()
    definition_ids = set()
    for capability in capabilities:
        number = capability.get("number")
        if capability.get("source") not in sources or capability.get("locator") != f"sheet:Pilot Scope v46.2/row:{number + 1}":
            errors.add("capability_source_locator")
        if package.get("schemaVersion") == "poseidon.pilot-trace.v1" and "sourceIdentity" not in capability:
            errors.add("missing_source_definition_inventory")
        if "sourceIdentity" in capability:
            try:
                identity = capability["sourceIdentity"]
                if identity.get("source") != capability["source"] or identity.get("locator") != capability["locator"] or identity.get("column") != "B" or identity.get("value") != capability.get("title"):
                    raise ValueError("capability_identity_mismatch")
                records.resolve(identity)
                if capability.get("definitionScreen"):
                    records.resolve(capability["definitionScreen"])
                records.resolve(capability["definedUat"])
            except (OSError, ValueError, KeyError):
                errors.add("source_definition_mismatch")
            chains = capability.get("definitionChains", [])
            if not chains:
                gaps.add(f"capability-{number}:source-definition-gap")
                if not capability.get("definitionGap"):
                    errors.add("undeclared_source_gap")
            expected_fields = set()
            if capability.get("definitionScreen"):
                screen = capability["definitionScreen"]["value"]
                if any(chain["screen"]["value"] != screen for chain in chains):
                    errors.add("source_mismatched_capability_screen")
                for loc, text in records.records("trace").items():
                    if loc.startswith("sheet:2316 Alan v46.3/") and re.search(r"^A[0-9]+: " + re.escape(screen) + r" \|", text):
                        expected_fields.add(loc)
            if {chain["requirement"]["locator"] for chain in chains} != expected_fields:
                gaps.add(f"capability-{number}:missing-critical-source-fields")
            for chain in chains:
                if chain.get("state") != "DEFINED":
                    errors.add("definition_execution_conflation")
                for kind in ("requirement", "screen", "api", "entity", "backlog", "test", "backlogTest"):
                    try:
                        expected_source = "backlog" if kind in {"backlog", "backlogTest"} else "trace"
                        expected_column = {"requirement": "U", "screen": "A", "api": "R", "entity": "S", "test": "V", "backlog": "A", "backlogTest": "M"}[kind]
                        if chain[kind]["source"] != expected_source or chain[kind]["column"] != expected_column:
                            raise ValueError("source_kind")
                        records.resolve(chain[kind])
                    except (OSError, ValueError, KeyError):
                        errors.add("source_definition_mismatch")
                identity = chain["requirement"]["value"]
                if identity in definition_ids:
                    errors.add("duplicate_definition_id")
                definition_ids.add(identity)
                refs = [chain[k] for k in ("requirement", "screen", "api", "entity", "test")]
                if len({(r["source"], r["locator"]) for r in refs}) != 1:
                    errors.add("source_mismatched_chain")
        nodes = capability.get("nodes", [])
        local = {n.get("id"): n for n in nodes}
        if len(local) != len(nodes):
            errors.add("duplicate_id")
        for node in nodes:
            if node.get("id") in node_ids:
                errors.add("duplicate_id")
            node_ids.add(node.get("id"))
            if node.get("kind") not in KINDS or node.get("state") not in STATES:
                errors.add("node_contract")
            if node.get("source") not in sources or not node.get("locator"):
                errors.add("node_source_locator")
            # Authored code references are real paths, never invented test names.
            files = node.get("files", [])
            if node.get("state") in {"AUTHORED", "RUN", "PASS"} and (not files or any(not (root / f).resolve().is_relative_to(root.resolve()) or not (root / f).is_file() for f in files)):
                errors.add("broken_code_reference")
            if node.get("state") in {"RUN", "PASS"} and not SHA.fullmatch(node.get("executionEvidenceSha256", "")):
                errors.add("missing_execution_evidence")
        edges = capability.get("edges", [])
        for a, b in edges:
            if a not in local or b not in local:
                errors.add("broken_trace_reference")
        if "sourceIdentity" not in capability:
            for a_kind, b_kind in zip(("requirement", "screen", "api", "entity", "backlog"), ("screen", "api", "entity", "backlog", "test")):
                if not any(a in local and b in local and local[a].get("kind") == a_kind and local[b].get("kind") == b_kind for a, b in edges):
                    gaps.add(f"capability-{number}:orphan-{a_kind}-{b_kind}")
            reached = {k for k, n in local.items() if n.get("kind") == "requirement"}
            for kind in ("screen", "api", "entity", "backlog", "test"):
                reached = {b for a, b in edges if a in reached and b in local and local[b].get("kind") == kind}
                if not reached:
                    gaps.add(f"capability-{number}:disconnected-critical-chain")
                    break
        # Every retained critical node must participate, even if another complete
        # path exists. An existential path must not hide orphan inventory.
        linked = {v for edge in edges for v in edge}
        if any(n["id"] not in linked for n in nodes):
            gaps.add(f"capability-{number}:orphan-critical-node")
        for gap in capability.get("remaining", []):
            if gap:
                implementation.add(f"capability-{number}:declared-implementation-gap")
        if not any(n.get("kind") == "test" and n.get("state") == "PASS" for n in nodes):
            execution.add(f"capability-{number}:no-passing-current-journey")
    # DEV-007 is a trace-definition criterion, not a new demand to execute or
    # implement every future capability before defining its chain. Report those
    # real product/release gaps separately, without awarding trace acceptance.
    return {"structureErrors": sorted(errors), "criticalGaps": sorted(gaps),
            "implementationGaps": sorted(implementation), "executionGaps": sorted(execution), "accepted": False}


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("package")
    args = parser.parse_args(); root = Path(__file__).resolve().parents[1]
    try:
        path = Path(args.package)
        if path.stat().st_size > 2 * 1024 * 1024:
            raise ValueError("trace_budget")
        package = json.loads(path.read_text(encoding="utf-8"))
        result = validate(package, root)
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        result = {"structureErrors": ["invalid_input"], "criticalGaps": [], "implementationGaps": [], "executionGaps": [], "accepted": False}
    print(json.dumps(result, sort_keys=True))
    return 1 if result["structureErrors"] or result["criticalGaps"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
