import hashlib
import io
import unittest
import zipfile

from core_schema_inventory import inventory


def workbook(rows, sheet="Data Dictionary 2316", extra_sheet=False, duplicate_member=False):
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w") as z:
        z.writestr("xl/workbook.xml", '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="' + sheet + '" sheetId="1" r:id="r1"/></sheets></workbook>')
        z.writestr("xl/_rels/workbook.xml.rels", '<Relationships><Relationship Id="r1" Target="worksheets/sheet1.xml"/></Relationships>')
        cells = []
        for i, row in enumerate(rows, 2):
            cells.append('<row r="' + str(i) + '">' + ''.join('<c r="' + col + str(i) + '" t="inlineStr"><is><t>' + value + '</t></is></c>' for col, value in row.items()) + '</row>')
        z.writestr("xl/worksheets/sheet1.xml", '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>' + ''.join(cells) + '</sheetData></worksheet>')
        if extra_sheet:
            z.writestr("xl/worksheets/historical.xml", '<not-current/>')
        if duplicate_member:
            z.writestr("xl/workbook.xml", '<ambiguous/>')
    return output.getvalue()


class CoreSchemaInventoryTests(unittest.TestCase):
    def row(self, field="vessel_id"):
        return {"A": "SyntheticDomain", "E": "synthetic-screen", "H": field, "AG": "FR-" + field, "AH": "TC-" + field}

    def test_inventory_preserves_locators_and_does_not_infer_tables_or_duplicate_historical_sheets(self):
        raw = workbook([self.row(), self.row("person_id")], extra_sheet=True)
        result = inventory(raw, hashlib.sha256(raw).hexdigest(), 2)
        self.assertEqual([2, 3], result["rows"])
        self.assertEqual(2, result["fields"])
        self.assertEqual(1, result["screensNotTables"])
        self.assertFalse(result["accepted"])
        self.assertNotIn("tables", result)

    def test_changed_source_bytes_fail_before_semantic_mapping(self):
        raw = workbook([self.row()])
        with self.assertRaisesRegex(ValueError, "source_identity"):
            inventory(raw + b"changed", hashlib.sha256(raw).hexdigest(), 1)

    def test_historical_dictionary_is_not_active_inventory(self):
        raw = workbook([self.row()], sheet="1900 Alan v46")
        with self.assertRaisesRegex(ValueError, "authoritative_sheet"):
            inventory(raw, hashlib.sha256(raw).hexdigest(), 1)

    def test_missing_identity_duplicate_field_or_reused_trace_remain_real_gaps(self):
        for rows in ([{"A": "SyntheticDomain"}], [self.row(), self.row()],
                     [self.row(), {**self.row("other"), "AH": "TC-vessel_id"}]):
            raw = workbook(rows)
            with self.assertRaises(ValueError):
                inventory(raw, hashlib.sha256(raw).hexdigest(), len(rows))

    def test_missing_field_count_cannot_be_accepted_as_whole_source_coverage(self):
        raw = workbook([self.row()])
        with self.assertRaisesRegex(ValueError, "field_count"):
            inventory(raw, hashlib.sha256(raw).hexdigest(), 2316)

    def test_duplicate_zip_members_are_ambiguous_even_with_matching_source_hash(self):
        raw = workbook([self.row()], duplicate_member=True)
        with self.assertRaisesRegex(ValueError, "archive_shape"):
            inventory(raw, hashlib.sha256(raw).hexdigest(), 1)


if __name__ == "__main__":
    unittest.main()
