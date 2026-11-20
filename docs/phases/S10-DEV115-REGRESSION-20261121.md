# S10 DEV-115 — v46.3 competitor-delta regression inventory (21.11.2026)

State: **AUTHORED + LOCALLY RUN (subpackage)** — not an accepted sprint gate. CI and independent QA pending.
Branch `feature/s10-dev115-regression-20261121` (base `origin/integration/s9-bundle-20261120` `fb9d64a`). No product code, no migration, no new CI step.

## Sources (pinned SHA-256 via `docs/sources/2026-11-15/manifest.json`)
- 13.02 `paragraph:19..` "2. Ekran İndeksi" (196 rows of `# | Screen ID | Screen | Domain | Fields | Actions`).
- 11.01 sheets `196 Ekran v46.3`, `2316 Alan v46.3`, `v46.3 Delta` rows 2-12 (`ADD / v46.3`), `v46.3 Özet` rows 13-16 (196 / 2316 / 11 / 190).
- 14.02 sheets `Data Dictionary 2316`, `Screen Coverage 196`.

## Delivered
- `scripts/regression_inventory.py`: cross-checks the three sources (screen IDs, field `(screen, key)` pairs, declared sums), compares against frontend destinations (`enterprise-navigation.ts`), `App.tsx` dispatch, `WorkspaceLensEndpoints.known` and `contracts/openapi.v1.json`; writes `.runtime/regression/inventory.json`. Exit 1 only on source-integrity failure or route-snapshot regression — unbuilt screens are listed, never counted as PASS.
- `tests/fixtures/regression/route-snapshot.v1.json`: add-only snapshot (44 destinations, 46 dispatched slugs, 48 lens keys, 433 API paths). `--write-snapshot` refuses to refresh while a removal/rename exists.
- `scripts/test_regression_inventory.py` (15 tests, CI's existing `unittest discover -s scripts`): parsers, snapshot removal/rename detection, real-source totals, URL/ID stability, 11 new screens routed, tug/applicability/readiness families without regression.
- `tests/Poseidon.Browser.Tests/specs/v463-new-screens.spec.ts`: 11 screens × desktop/mobile; canonical URL, TR then EN heading, reload keeps URL, no page error, no write request. Synthetic transport (FICTIONAL session, other reads 403).

## Inventory result (honest counts)
| Item | Count |
|---|---|
| Source screens / fields | 196 / 2316 (all three sources consistent, 0 integrity findings) |
| Screens ROUTED / API_ONLY / MISSING | 42 / 11 / 143 |
| Fields on ROUTED / API_ONLY / MISSING screens | 638 / 104 / 1574 |
| OpenAPI property name matches (heuristic, not acceptance) | 330 |
| New v46.3 screens routed | 11 / 11 |
| Snapshot regressions | 0 |
| Routes outside the 196 IDs | audit, companies, defects, document-compare, fleet, requirements, safety-events, sources, today, voice-event-draft |

Families: tug 12/14 routed (missing `tug-operation-history` = S10 WP-B DEV-091, `tug-workspace`); applicability 0/3 routed (`applicability` is API_ONLY); readiness 4/14 routed. Several "extra" routes are naming mismatches with catalog IDs (e.g. `today` ↔ `poseidon-today`, `fleet` ↔ `fleet-vessels`); no alias was invented — mapping needs a product decision.

## Local checks
- `py -m unittest test_regression_inventory -v` → 15/15 OK (1.7 s).
- `py scripts/regression_inventory.py` → `inventoried`, exit 0.
- Playwright `v463-new-screens.spec.ts` (UI-only preview, both projects) → 22/22 passed, 35.9 s wall incl. server start, ~0.75 s/test. Full `browser_gate.py` (Docker/PG) NOT RUN locally (Docker deferred) — runs in CI.
- `tsc --strict --noEmit` on the new spec → 0 errors.

## Next
Coordinator integration; refresh the snapshot add-only (`--write-snapshot`) after WP-A/B/C add `data-trust-center`, `industry-assurance`, `tug-operation-history`.
