"""Read-only bounded current-source references; no acceptance or execution authority."""
import hashlib
import re
import zipfile
from xml.etree.ElementTree import ParseError
from import_documents import extract

PREFIX = "analiz/POSEIDON GUNCEL 30.10.2026/"

class SourceRecords:
    def __init__(self, sources, root):
        self.sources, self.root, self.cache = sources, root.resolve(), {}

    def records(self, key):
        try:
            return self._records(key)
        except (zipfile.BadZipFile, ParseError, IndexError) as exc:
            raise ValueError("invalid_source_archive") from exc

    def _records(self, key):
        if key in self.cache:
            return self.cache[key]
        source = self.sources[key]
        path = (self.root / source["path"]).resolve()
        if not source["path"].startswith(PREFIX) or not path.is_relative_to(self.root):
            raise ValueError("source_identity")
        if path.stat().st_size > 32 * 1024 * 1024:
            raise ValueError("source_budget")
        if hashlib.sha256(path.read_bytes()).hexdigest() != source["sha256"]:
            raise ValueError("source_bytes")
        with zipfile.ZipFile(path) as archive:
            infos = archive.infolist()
            if len(infos) > 1000 or len({i.filename for i in infos}) != len(infos) or sum(i.file_size for i in infos) > 128 * 1024 * 1024:
                raise ValueError("archive_budget")
            for info in infos:
                if info.flag_bits & 1 or info.file_size > 64 * 1024 * 1024:
                    raise ValueError("archive_budget")
                if info.filename.endswith(".xml"):
                    raw = archive.read(info)
                    if b"<!DOCTYPE" in raw or b"<!ENTITY" in raw:
                        raise ValueError("xml_declaration")
        records = dict(extract(path))
        if len(records) > 30000:
            raise ValueError("record_budget")
        self.cache[key] = records
        return records

    def resolve(self, ref):
        records = self.records(ref["source"])
        locator = ref["locator"]
        if locator.startswith("paragraph:") and ".." in locator:
            a, b = map(int, locator[10:].split(".."))
            if not 0 < a <= b <= a + 40:
                raise ValueError("locator_budget")
            text = "\n".join(records.get(f"paragraph:{n}", "") for n in range(a, b + 1))
        else:
            text = records.get(locator, "")
        if not text:
            raise ValueError("missing_source_locator")
        if "value" in ref:
            column = ref.get("column")
            if column:
                row = locator.rsplit(":", 1)[-1]
                cells = dict(re.findall(r"(?:^| \| )([A-Z]+[0-9]+): (.*?)(?= \| [A-Z]+[0-9]+: |$)", text))
                if cells.get(column + row) != ref["value"]:
                    raise ValueError("source_value_mismatch")
            elif ref["value"] != text:
                raise ValueError("source_value_mismatch")
        return text


def local_file(root, name):
    path = (root / name).resolve()
    return path.is_relative_to(root.resolve()) and path.is_file()
