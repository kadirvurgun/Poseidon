"""DEV-005 bounded OOXML inventory. Does not infer physical tables from screens.

Only the active Data Dictionary 2316 worksheet is selected. Original bytes are
read-only and verified before extraction. Run at the later foundation gate.
"""
import hashlib
import io
import json
from pathlib import Path
import posixpath
import zipfile
from xml.etree import ElementTree as ET

SOURCE_PATH = "analiz/POSEIDON GUNCEL 30.10.2026/05_TECHNICAL_ARCHITECTURE/14.02_POSEIDON_v46.3_Product_Wide_Data_Dictionary_TR.xlsx"
SOURCE_SHA = "3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018"
NS = {"m": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
MAX_SOURCE = 32 * 1024 * 1024
MAX_MEMBER = 64 * 1024 * 1024


def inventory(raw, expected_sha=SOURCE_SHA, expected_count=2316):
    if len(raw) > MAX_SOURCE or hashlib.sha256(raw).hexdigest() != expected_sha:
        raise ValueError("source_identity")
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        infos = archive.infolist()
        if len(infos) > 1000 or len({i.filename for i in infos}) != len(infos):
            raise ValueError("archive_shape")

        def read(name):
            member = archive.getinfo(name)
            if member.flag_bits & 1 or member.file_size > MAX_MEMBER:
                raise ValueError("member_budget")
            data = archive.read(member)
            if b"<!DOCTYPE" in data or b"<!ENTITY" in data:
                raise ValueError("xml_declaration")
            return data

        workbook = ET.fromstring(read("xl/workbook.xml"))
        sheets = [s for s in workbook.findall("m:sheets/m:sheet", NS) if s.attrib.get("name") == "Data Dictionary 2316"]
        if len(sheets) != 1:
            raise ValueError("authoritative_sheet")
        relid = sheets[0].attrib["{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"]
        relations = ET.fromstring(read("xl/_rels/workbook.xml.rels"))
        targets = [r.attrib.get("Target", "") for r in relations if r.attrib.get("Id") == relid and r.attrib.get("TargetMode") != "External"]
        if len(targets) != 1:
            raise ValueError("sheet_relationship")
        target = posixpath.normpath(targets[0].lstrip("/") if targets[0].startswith("/") else "xl/" + targets[0])
        if not target.startswith("xl/worksheets/"):
            raise ValueError("sheet_relationship")
        shared = []
        if "xl/sharedStrings.xml" in archive.namelist():
            strings = ET.fromstring(read("xl/sharedStrings.xml"))
            shared = ["".join(s.itertext()) for s in strings.findall("m:si", NS)]
        sheet = read(target)
        domains = {}; screens = set(); refs = set(); seen = set(); duplicate_trace = set(); locators = []
        for _, row in ET.iterparse(io.BytesIO(sheet), events=("end",)):
            if row.tag != "{" + NS["m"] + "}row":
                continue
            number = int(row.attrib["r"])
            if number == 1:
                row.clear(); continue
            values = {}
            for cell in row.findall("m:c", NS):
                column = "".join(c for c in cell.attrib["r"] if c.isalpha())
                if column not in ("A", "E", "H", "AG", "AH"):
                    continue
                v = cell.find("m:v", NS)
                value = v.text if v is not None else "".join(cell.find("m:is", NS).itertext()) if cell.find("m:is", NS) is not None else ""
                values[column] = shared[int(value)] if cell.attrib.get("t") == "s" else value
            if not any(values.values()):
                row.clear(); continue
            if not all(values.get(c) for c in ("A", "E", "H", "AG", "AH")):
                raise ValueError("missing_authoritative_field_identity")
            key = (values["E"], values["H"])
            if key in refs:
                raise ValueError("duplicate_field_identity")
            refs.add(key); screens.add(values["E"])
            domains[values["A"]] = domains.get(values["A"], 0) + 1
            for column in ("AG", "AH"):
                trace = values[column]
                if trace in seen:
                    duplicate_trace.add(trace)
                seen.add(trace)
            locators.append(number)
            if len(locators) > expected_count:
                raise ValueError("field_count")
            row.clear()
        if len(locators) != expected_count or duplicate_trace:
            raise ValueError("field_count_or_duplicate_trace")
        return {"schemaVersion": "poseidon.schema-source-inventory.v1", "sourceSha256": expected_sha,
                "sheet": "Data Dictionary 2316", "fields": len(locators), "screensNotTables": len(screens),
                "rows": sorted(locators), "domainFieldCounts": dict(sorted(domains.items())),
                "physicalMappings": "UNCONFIRMED_UNLESS_EXPLICIT_IN_CORE_SCHEMA_PLAN", "accepted": False}


def main():
    root = Path(__file__).resolve().parents[1]
    try:
        source = root / SOURCE_PATH
        if source.stat().st_size > MAX_SOURCE:
            raise ValueError("source_budget")
        result = inventory(source.read_bytes())
        print(json.dumps(result, ensure_ascii=True, sort_keys=True))
        return 0
    except (OSError, ValueError, KeyError, IndexError, zipfile.BadZipFile, ET.ParseError):
        print(json.dumps({"status": "source_inventory_unresolved", "accepted": False}))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
