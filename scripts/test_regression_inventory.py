"""DEV-115 regression inventory: parser, comparison and URL/ID stability tests."""
import json
import unittest

import regression_inventory as inv


def catalog_paragraphs(rows):
    texts = ["2. Ekran İndeksi", "#", "Screen ID", "Screen", "Domain", "Fields", "Actions"]
    for number, (sid, fields) in enumerate(rows, 1):
        texts += [str(number), sid, sid.title(), "Domain", str(fields), "1"]
    texts.append("3. Sonraki bölüm")
    return {f"paragraph:{index}": text for index, text in enumerate(texts, 10)}


def surface(**overrides):
    base = {"destinations": {"noon-report": "/app/noon-report", "tug-command": "/app/tug-command"},
            "dispatched": ["noon-report", "tug-command", "tug-job-detail"], "lenses": ["noon-report", "tug-command"],
            "apiPaths": ["/api/v1/noon-reports", "/api/v1/poseidon-today"], "properties": {"weathersummary"}}
    base.update(overrides)
    return base


class ParserTests(unittest.TestCase):
    def test_catalog_index_reads_sequential_six_cell_rows_and_stops_at_next_section(self):
        screens = inv.catalog_screens(catalog_paragraphs([("safety-inbox", 0), ("noon-report", 20)]))
        self.assertEqual([s["id"] for s in screens], ["safety-inbox", "noon-report"])
        self.assertEqual(screens[1]["fields"], 20)
        self.assertEqual(screens[0]["locator"], "paragraph:18")

    def test_catalog_without_index_header_is_a_source_error(self):
        with self.assertRaisesRegex(ValueError, "catalog_index_header"):
            inv.catalog_screens({"paragraph:1": "Screen ID"})

    def test_catalog_row_with_non_slug_id_is_rejected_not_skipped(self):
        with self.assertRaisesRegex(ValueError, "catalog_row:1"):
            inv.catalog_screens(catalog_paragraphs([("Bad Id", 1)]))

    def test_cells_split_sheet_rows_by_column(self):
        self.assertEqual(inv.cells("A2: noon-report | B2: Noon | G2: API /api/v1/noon-report"),
                         {"A": "noon-report", "B": "Noon", "G": "API /api/v1/noon-report"})

    def test_route_regex_fragments_expand_to_literal_slugs(self):
        self.assertEqual(inv.expand("tug-(assignment|readiness)"), ["tug-assignment", "tug-readiness"])
        self.assertEqual(inv.expand("(noon-report|deck-daily-log)"), ["noon-report", "deck-daily-log"])
        self.assertEqual(inv.expand("audit-ready"), ["audit-ready"])
        self.assertEqual(inv.expand("([0-9a-f-]{36})"), [])

    def test_api_match_accepts_exact_child_and_plural_collection_only(self):
        paths = ["/api/v1/noon-reports", "/api/v1/poseidon-today", "/api/v1/tug-jobs/{id}"]
        self.assertTrue(inv.api_matches("/api/v1/poseidon-today", paths))
        self.assertTrue(inv.api_matches("/api/v1/noon-report", paths))
        self.assertTrue(inv.api_matches("/api/v1/tug-job", paths))
        self.assertFalse(inv.api_matches("/api/v1/noon", paths))
        self.assertFalse(inv.api_matches("", paths))

    def test_field_key_heuristic_is_camel_case_lower(self):
        self.assertEqual(inv.camel("weather_summary"), "weathersummary")


class SnapshotTests(unittest.TestCase):
    def snapshot(self):
        return inv.snapshot_of(surface())

    def test_unchanged_surface_has_no_regression_and_additions_are_allowed(self):
        grown = surface(destinations={**surface()["destinations"], "new-screen": "/app/new-screen"},
                        apiPaths=surface()["apiPaths"] + ["/api/v1/new"])
        self.assertEqual(inv.check_snapshot(self.snapshot(), grown), [])

    def test_removed_or_renamed_url_and_ids_are_regressions(self):
        changed = surface(destinations={"noon-report": "/app/noon", "tug-cmd": "/app/tug-command"},
                          dispatched=["noon-report"], lenses=["noon-report"], apiPaths=["/api/v1/poseidon-today"])
        kinds = sorted(f["kind"] + ":" + f["id"] for f in inv.check_snapshot(self.snapshot(), changed))
        self.assertEqual(kinds, ["api_path_removed:/api/v1/noon-reports", "destination_removed:tug-command",
                                 "destination_url_changed:noon-report", "dispatch_removed:tug-command",
                                 "dispatch_removed:tug-job-detail", "lens_removed:tug-command"])


class RepositoryTests(unittest.TestCase):
    """Runs against the pinned controlled sources and the current repository."""

    @classmethod
    def setUpClass(cls):
        cls.surface = inv.repository_surface(inv.ROOT)
        cls.snapshot = json.loads((inv.ROOT / inv.SNAPSHOT).read_text(encoding="utf-8"))
        cls.report = inv.build_report(inv.load_sources(inv.ROOT), cls.surface, cls.snapshot)

    def test_controlled_sources_agree_on_196_screens_2316_fields_and_11_new_screens(self):
        self.assertEqual(self.report["sourceIntegrity"], [])
        self.assertEqual(self.report["screens"]["total"], 196)
        self.assertEqual(self.report["fields"]["total"], 2316)
        self.assertEqual(len(self.report["newV463Screens"]), 11)

    def test_published_urls_lens_ids_and_api_paths_are_stable(self):
        self.assertEqual(self.report["snapshot"]["regressions"], [])

    def test_every_new_v463_screen_keeps_a_canonical_routed_url(self):
        for screen in self.report["newV463Screens"]:
            with self.subTest(screen=screen["id"]):
                self.assertEqual(screen["state"], "ROUTED")
                self.assertEqual(screen["href"], "/app/" + screen["id"])

    def test_tug_applicability_and_readiness_routes_have_no_snapshot_regression(self):
        for name, family in self.report["families"].items():
            with self.subTest(family=name):
                self.assertEqual(family["snapshotRegressions"], [])
        self.assertIn("tug-readiness", self.report["families"]["tug"]["routed"])

    def test_missing_screens_are_listed_not_hidden(self):
        screens = self.report["screens"]
        self.assertEqual(screens["routed"] + screens["apiOnly"] + screens["missing"], 196)
        self.assertEqual(len(screens["missingIds"]), screens["missing"])
        fields = self.report["fields"]
        self.assertEqual(fields["onRoutedScreens"] + fields["onApiOnlyScreens"] + fields["onMissingScreens"], 2316)

    def test_snapshot_file_is_the_canonical_add_only_shape(self):
        self.assertEqual(self.snapshot["schema"], "poseidon.route-snapshot.v1")
        self.assertEqual(self.snapshot["dispatched"], sorted(self.snapshot["dispatched"]))
        self.assertEqual(self.snapshot["apiPaths"], sorted(self.snapshot["apiPaths"]))


if __name__ == "__main__":
    unittest.main()
