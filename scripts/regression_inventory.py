"""DEV-115 v46.3 competitor-delta regression inventory (read-only, advisory).

Compares the controlled 196-screen / 2316-field v46.3 inventory against what the
repository actually routes today:

* 13.02 screen index (docx, section "2. Ekran İndeksi"),
* 11.01 sheets "196 Ekran v46.3", "2316 Alan v46.3", "v46.3 Delta", "v46.3 Özet",
* 14.02 sheets "Data Dictionary 2316" and "Screen Coverage 196",

against the frontend destinations (enterprise-navigation.ts), App.tsx path
dispatch, WorkspaceLensEndpoints `known` lens keys and the exported OpenAPI
paths.  It also checks a committed route snapshot so previously published
URLs / screen IDs are never silently removed or renamed.

The result is an inventory, not an acceptance.  A screen counted as ROUTED only
means a canonical URL dispatches; it is not a claim that the screen satisfies
its field contract.  Field "name matches" are a heuristic over OpenAPI schema
property names and are labelled as such.  Missing screens are listed, never
hidden; the exit code fails only on source integrity problems or snapshot
regressions (removed/renamed URLs or IDs, lost tug/applicability/readiness
routes), not on the honest count of not-yet-built screens.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys

from import_documents import extract

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = "poseidon.regression-inventory.v1"
MANIFEST = "docs/sources/2026-11-15/manifest.json"
SNAPSHOT = "tests/fixtures/regression/route-snapshot.v1.json"
PREFIX = "analiz/POSEIDON GUNCEL 30.10.2026/"
SOURCES = {
    "13.02": PREFIX + "04_UX_PRODUCT_DESIGN/13.02_POSEIDON_v46.3_196_Ekran_Gorselli_Kullanim_Katalogu_TR.docx",
    "11.01": PREFIX + "03_REQUIREMENTS_REGULATORY/11.01_POSEIDON_v46.3_Controlled_Implementation_Traceability_Matrix_TR.xlsx",
    "14.02": PREFIX + "05_TECHNICAL_ARCHITECTURE/14.02_POSEIDON_v46.3_Product_Wide_Data_Dictionary_TR.xlsx",
}
EXPECTED_SCREENS = 196
EXPECTED_FIELDS = 2316
EXPECTED_NEW_SCREENS = 11
EXPECTED_NEW_FIELDS = 190
# Regression families named by DEV-115 AC ("tug/applicability/readiness regresyonu yok").
FAMILIES = {
    "tug": re.compile(r"^(tug-|towage-)"),
    "applicability": re.compile(r"applicability"),
    "readiness": re.compile(r"readiness|departure-signoff"),
}
CELL = re.compile(r"(?:^| \| )([A-Z]+)([0-9]+): (.*?)(?= \| [A-Z]+[0-9]+: |$)", re.S)
SLUG = re.compile(r"\A[a-z0-9]+(?:-[a-z0-9]+)*\Z")


def cells(text):
    return {column: value for column, _, value in CELL.findall(text)}


def load_sources(root, manifest=None):
    """Return locator->text per source after verifying the pinned manifest SHA-256."""
    manifest = manifest or json.loads((root / MANIFEST).read_text(encoding="utf-8"))
    pinned = {d["source_path"]: d["sha256"] for d in manifest["files"]}
    records = {}
    for key, relative in SOURCES.items():
        path = root / relative
        if relative not in pinned:
            raise ValueError(f"source_not_pinned:{key}")
        if hashlib.sha256(path.read_bytes()).hexdigest() != pinned[relative]:
            raise ValueError(f"source_bytes:{key}")
        records[key] = dict(extract(path))
    return records


def sheet_rows(records, sheet):
    prefix = f"sheet:{sheet}/row:"
    rows = []
    for locator, text in records.items():
        if locator.startswith(prefix):
            rows.append((int(locator[len(prefix):]), locator, cells(text)))
    return sorted(rows)


def catalog_screens(paragraphs):
    """13.02 '2. Ekran İndeksi': header '#|Screen ID|Screen|Domain|Fields|Actions' then 6-cell rows."""
    ordered = sorted(((int(k.split(":")[1]), v) for k, v in paragraphs.items() if k.startswith("paragraph:")))
    texts = [v for _, v in ordered]
    locs = [n for n, _ in ordered]
    header = ["#", "Screen ID", "Screen", "Domain", "Fields", "Actions"]
    start = next((i for i in range(len(texts) - 5) if texts[i:i + 6] == header), None)
    if start is None:
        raise ValueError("catalog_index_header")
    screens, i, expected = [], start + 6, 1
    while i + 5 < len(texts) and texts[i] == str(expected):
        number, sid, name, domain, fields, actions = texts[i:i + 6]
        if not SLUG.match(sid) or not fields.isdigit() or not actions.isdigit():
            raise ValueError(f"catalog_row:{expected}")
        screens.append({"number": expected, "id": sid, "name": name, "domain": domain,
                        "fields": int(fields), "actions": int(actions), "locator": f"paragraph:{locs[i + 1]}"})
        i, expected = i + 6, expected + 1
    return screens


def trace_screens(records):
    result = []
    for row, locator, c in sheet_rows(records, "196 Ekran v46.3"):
        if row == 1:
            if c.get("A") != "Screen ID" or c.get("G") != "API":
                raise ValueError("trace_screen_header")
            continue
        api = c.get("G", "")
        api = api[4:] if api.startswith("API ") else api
        result.append({"id": c["A"], "name": c.get("B", ""), "domain": c.get("C", ""),
                       "fields": int(c.get("D", "0")), "api": api, "locator": locator})
    return result


def field_rows(records, sheet, screen_col, key_col, header_key):
    result = []
    for row, locator, c in sheet_rows(records, sheet):
        if row == 1:
            if c.get(key_col) != header_key:
                raise ValueError(f"field_header:{sheet}")
            continue
        result.append({"screen": c.get(screen_col, ""), "key": c.get(key_col, ""), "locator": locator})
    return result


def delta_screens(records):
    result = []
    for row, locator, c in sheet_rows(records, "v46.3 Delta"):
        if row > 1 and c.get("E") == "ADD / v46.3":
            result.append({"id": c["A"], "name": c.get("B", ""), "fields": int(c["D"]), "locator": locator})
    return result


def summary_value(records, label):
    for _, locator, c in sheet_rows(records, "v46.3 Özet"):
        if c.get("A") == label:
            return int(c["B"]), locator
    raise ValueError(f"summary:{label}")


# ---------------------------------------------------------------- repository side

def expand(pattern):
    """Expand a simple `lit(a|b)lit` route regex fragment into literal slugs."""
    pattern = pattern.replace("\\-", "-")
    match = re.match(r"\A([a-z0-9-]*)\(([a-z0-9|-]+)\)([a-z0-9-]*)\Z", pattern)
    if match:
        head, alts, tail = match.groups()
        return [head + alt + tail for alt in alts.split("|")]
    return [pattern] if SLUG.match(pattern) else []


def frontend_routes(root):
    navigation = (root / "frontend/src/product/enterprise-navigation.ts").read_text(encoding="utf-8")
    block = re.search(r"const destinations: Record<string, string> = \{(.*?)\}\n", navigation, re.S)
    if not block:
        raise ValueError("frontend_destinations")
    destinations = {}
    for key, href in re.findall(r"'?([a-z0-9-]+)'?\s*:\s*'(/app/[a-z0-9/-]+)'", block.group(1)):
        destinations[key] = href
    app = (root / "frontend/src/App.tsx").read_text(encoding="utf-8")
    dispatched = set()
    for fragment in re.findall(r"\^\\/app\\/(.+?)(?:\(\?:|\\/|\$|\/\.test|\?)", app):
        dispatched.update(expand(fragment))
    dispatched.update(re.findall(r"pathname === '/app/([a-z0-9-]+)'", app))
    return destinations, sorted(dispatched)


def lens_keys(root):
    text = (root / "backend/Poseidon.Api/WorkspaceLensEndpoints.cs").read_text(encoding="utf-8")
    match = re.search(r"string\[\] known = \[(.*?)\];", text, re.S)
    if not match:
        raise ValueError("lens_known")
    return sorted(set(re.findall(r'"([a-z0-9-]+)"', match.group(1))))


def openapi_surface(root):
    document = json.loads((root / "contracts/openapi.v1.json").read_text(encoding="utf-8"))
    paths = sorted(document["paths"])
    properties = set()
    for schema in document.get("components", {}).get("schemas", {}).values():
        properties.update((schema.get("properties") or {}).keys())
    return paths, {p.lower() for p in properties}


def camel(key):
    head, *rest = key.split("_")
    return (head + "".join(part[:1].upper() + part[1:] for part in rest)).lower()


def api_matches(api, paths):
    if not api.startswith("/api/v1/"):
        return False
    return any(p == api or p.startswith(api + "/") or p.startswith(api + "s") and p[len(api) + 1:][:1] in ("", "/")
               for p in paths)


def repository_surface(root):
    destinations, dispatched = frontend_routes(root)
    paths, properties = openapi_surface(root)
    return {"destinations": destinations, "dispatched": dispatched, "lenses": lens_keys(root),
            "apiPaths": paths, "properties": properties}


# ---------------------------------------------------------------- comparison

def check_snapshot(snapshot, surface):
    """URL/ID stability: everything in the committed snapshot must still exist unchanged."""
    findings = []
    for key, href in snapshot["destinations"].items():
        current = surface["destinations"].get(key)
        if current is None:
            findings.append({"kind": "destination_removed", "id": key, "href": href})
        elif current != href:
            findings.append({"kind": "destination_url_changed", "id": key, "href": href, "current": current})
    for slug in snapshot["dispatched"]:
        if slug not in surface["dispatched"]:
            findings.append({"kind": "dispatch_removed", "id": slug})
    for key in snapshot["lenses"]:
        if key not in surface["lenses"]:
            findings.append({"kind": "lens_removed", "id": key})
    for path in snapshot["apiPaths"]:
        if path not in surface["apiPaths"]:
            findings.append({"kind": "api_path_removed", "id": path})
    return findings


def build_report(records, surface, snapshot):
    integrity = []
    catalog = catalog_screens(records["13.02"])
    trace = trace_screens(records["11.01"])
    dictionary_screens = [r for r in field_rows(records["14.02"], "Screen Coverage 196", "A", "A", "Screen ID")]
    trace_fields = field_rows(records["11.01"], "2316 Alan v46.3", "A", "D", "Field Key")
    dictionary_fields = field_rows(records["14.02"], "Data Dictionary 2316", "E", "H", "Field Key")
    delta = delta_screens(records["11.01"])
    total_screens, screens_loc = summary_value(records["11.01"], "Total screens")
    total_fields, fields_loc = summary_value(records["11.01"], "Total field contracts")
    new_screens, _ = summary_value(records["11.01"], "New v46.3 benchmark screens")
    new_fields, _ = summary_value(records["11.01"], "New v46.3 field contracts")

    def expect(name, actual, wanted):
        if actual != wanted:
            integrity.append({"check": name, "actual": actual, "expected": wanted})

    catalog_ids = [s["id"] for s in catalog]
    trace_ids = [s["id"] for s in trace]
    expect("13.02_screen_count", len(catalog), EXPECTED_SCREENS)
    expect("11.01_screen_count", len(trace), EXPECTED_SCREENS)
    expect("14.02_screen_count", len(dictionary_screens), EXPECTED_SCREENS)
    expect("11.01_summary_screens", total_screens, EXPECTED_SCREENS)
    expect("11.01_summary_fields", total_fields, EXPECTED_FIELDS)
    expect("11.01_field_rows", len(trace_fields), EXPECTED_FIELDS)
    expect("14.02_field_rows", len(dictionary_fields), EXPECTED_FIELDS)
    expect("11.01_new_screens", (new_screens, len(delta)), (EXPECTED_NEW_SCREENS, EXPECTED_NEW_SCREENS))
    expect("11.01_new_fields", (new_fields, sum(d["fields"] for d in delta)), (EXPECTED_NEW_FIELDS, EXPECTED_NEW_FIELDS))
    expect("13.02_vs_11.01_screen_ids", sorted(set(catalog_ids) ^ set(trace_ids)), [])
    expect("13.02_vs_14.02_screen_ids", sorted(set(catalog_ids) ^ {r["screen"] for r in dictionary_screens}), [])
    expect("13.02_unique_ids", len(set(catalog_ids)), len(catalog_ids))
    expect("13.02_declared_field_sum", sum(s["fields"] for s in catalog), EXPECTED_FIELDS)
    trace_pairs = {(f["screen"], f["key"]) for f in trace_fields}
    dictionary_pairs = {(f["screen"], f["key"]) for f in dictionary_fields}
    expect("11.01_vs_14.02_field_pairs", len(trace_pairs ^ dictionary_pairs), 0)
    expect("field_screens_in_catalog", sorted({f["screen"] for f in dictionary_fields} - set(catalog_ids)), [])
    expect("delta_screens_in_catalog", sorted({d["id"] for d in delta} - set(catalog_ids)), [])

    routed = set(surface["destinations"]) | set(surface["dispatched"])
    api_by_screen = {s["id"]: s["api"] for s in trace}
    screens = []
    for s in catalog:
        api = api_by_screen.get(s["id"], "")
        entry = {"id": s["id"], "name": s["name"], "domain": s["domain"], "declaredFields": s["fields"],
                 "locator": "13.02 " + s["locator"], "frontendRoute": s["id"] in routed,
                 "navigationDestination": surface["destinations"].get(s["id"]),
                 "lensKey": s["id"] in surface["lenses"], "sourceApi": api,
                 "apiPresent": api_matches(api, surface["apiPaths"])}
        entry["state"] = ("ROUTED" if entry["frontendRoute"] else
                          "API_ONLY" if entry["apiPresent"] else "MISSING")
        screens.append(entry)
    states = {s["id"]: s["state"] for s in screens}
    fields_by_state = {"ROUTED": 0, "API_ONLY": 0, "MISSING": 0}
    name_matches = 0
    for f in dictionary_fields:
        fields_by_state[states.get(f["screen"], "MISSING")] += 1
        if states.get(f["screen"]) == "ROUTED" and camel(f["key"]) in surface["properties"]:
            name_matches += 1
    extra = sorted(routed - set(catalog_ids))
    extra_lenses = sorted(set(surface["lenses"]) - set(catalog_ids))
    new = []
    for d in delta:
        state = states.get(d["id"], "MISSING")
        new.append({"id": d["id"], "name": d["name"], "fields": d["fields"], "locator": "11.01 " + d["locator"],
                    "state": state, "href": surface["destinations"].get(d["id"]),
                    "lensKey": d["id"] in surface["lenses"]})
    families = {}
    snapshot_findings = check_snapshot(snapshot, surface)
    for name, pattern in FAMILIES.items():
        source = [s for s in screens if pattern.search(s["id"])]
        families[name] = {"sourceScreens": len(source),
                          "routed": sorted(s["id"] for s in source if s["state"] == "ROUTED"),
                          "missing": sorted(s["id"] for s in source if s["state"] != "ROUTED"),
                          "snapshotRegressions": [f for f in snapshot_findings if pattern.search(f["id"].rsplit("/", 1)[-1]) or pattern.search(f["id"])]}
    count = lambda state: sum(1 for s in screens if s["state"] == state)
    return {
        "schema": SCHEMA,
        "authority": "Read-only inventory of controlled-candidate sources versus repository routes. ROUTED is not field-contract acceptance; name matches are heuristic; no PASS is claimed for unbuilt screens.",
        "sources": {k: {"path": v} for k, v in SOURCES.items()},
        "sourceTotals": {"screens": total_screens, "screensLocator": "11.01 " + screens_loc,
                         "fields": total_fields, "fieldsLocator": "11.01 " + fields_loc,
                         "catalogScreens": len(catalog), "traceFieldRows": len(trace_fields),
                         "dictionaryFieldRows": len(dictionary_fields)},
        "sourceIntegrity": integrity,
        "screens": {"total": len(screens), "routed": count("ROUTED"), "apiOnly": count("API_ONLY"),
                    "missing": count("MISSING"),
                    "missingIds": sorted(s["id"] for s in screens if s["state"] == "MISSING"),
                    "apiOnlyIds": sorted(s["id"] for s in screens if s["state"] == "API_ONLY"),
                    "extraRoutes": extra, "extraLensKeys": extra_lenses},
        "fields": {"total": len(dictionary_fields), "onRoutedScreens": fields_by_state["ROUTED"],
                   "onApiOnlyScreens": fields_by_state["API_ONLY"], "onMissingScreens": fields_by_state["MISSING"],
                   "openApiNameMatchesHeuristic": name_matches},
        "newV463Screens": new,
        "families": families,
        "snapshot": {"path": SNAPSHOT, "regressions": snapshot_findings},
        "screenDetail": screens,
    }


def snapshot_of(surface):
    return {"schema": "poseidon.route-snapshot.v1",
            "rule": "Published /app URLs, navigation keys, workspace lens IDs and /api/v1 paths may be added but never removed or renamed without an explicit migration/redirect decision.",
            "destinations": dict(sorted(surface["destinations"].items())),
            "dispatched": surface["dispatched"], "lenses": surface["lenses"], "apiPaths": surface["apiPaths"]}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--output", default=".runtime/regression/inventory.json")
    parser.add_argument("--write-snapshot", action="store_true",
                        help="add-only refresh: keep every snapshot entry, append newly published ones")
    args = parser.parse_args(argv)
    surface = repository_surface(ROOT)
    snapshot_path = ROOT / SNAPSHOT
    snapshot = json.loads(snapshot_path.read_text(encoding="utf-8"))
    if args.write_snapshot:
        if check_snapshot(snapshot, surface):
            print(json.dumps({"status": "refused", "reason": "snapshot_regression_present"}))
            return 1
        snapshot_path.write_text(json.dumps(snapshot_of(surface), indent=2) + "\n", encoding="utf-8")
        snapshot = json.loads(snapshot_path.read_text(encoding="utf-8"))
    report = build_report(load_sources(ROOT), surface, snapshot)
    output = (ROOT / args.output).resolve()
    if not output.is_relative_to((ROOT / ".runtime").resolve()):
        raise SystemExit("output must stay under .runtime/")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    failed = bool(report["sourceIntegrity"] or report["snapshot"]["regressions"])
    print(json.dumps({"status": "failed" if failed else "inventoried", "output": str(output.relative_to(ROOT)).replace("\\", "/"),
                      "screens": {k: report["screens"][k] for k in ("total", "routed", "apiOnly", "missing")},
                      "fields": report["fields"], "newV463": {n["id"]: n["state"] for n in report["newV463Screens"]},
                      "sourceIntegrity": len(report["sourceIntegrity"]),
                      "snapshotRegressions": len(report["snapshot"]["regressions"])}, ensure_ascii=False))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
