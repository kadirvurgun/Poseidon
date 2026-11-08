# S6-DOWNTIME-INTEGRATION-01 — saved-source entry and scoped navigation

DEV-102 / T-OPS2-006 / Backlog103 / 8 SP. Source-only continuation over [domain requirements](S6-DOWNTIME-DOMAIN-01.md), owning API and UI tasks. All new execution remains **DEFERRED / NOT RUN**. The S6 selection reaches9 tasks/87SP; selection is neither task completion nor canonical acceptance.

## Source and authority

Active `poseidon-v46.3-20261030`;18.01 Backlog SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`, row103. Dependencies DEV-055 and DEV-099 now have actual owning declaration sources. The new Work Order source additionally supports a typed link. Full defect lifecycle, verified closure and authoritative evidence/claim mapping remain open.14.02 rows2226–2239, SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`, is Ship-only.

The optional exact source pin is engineering lineage, outside unresolved linked_defect/linked_work_order/linked_event dictionary reference mappings. Selecting a saved source does not populate those fields or establish causality, evidence adoption, hire entitlement, operational fitness or closure. Neither missing LoH nor missing closure state becomes No/Open/Verified.

## Implemented integration and acceptance cases

- DOWNTIME-NAV-01: only actual `operations.downtime.read` plus directory/Ship authority exposes the implemented global route. Server-scoped role lenses and frontend decoding both reject a Tug downtime destination, including actors with cross-mode visibility. Switching to a Tug returns to its fleet context and removes prior record/origin query parameters.
- DOWNTIME-LAUNCH-02: saved Defect and Work Order current revisions reuse [SourceDraftLaunch](../../frontend/src/product/SourceDraftLaunch.tsx). A target write grant and the exact source kind in server `originKinds` are both required. The URL contains only kind/id/revision metadata. Dirty, busy, frozen, mismatched-owner or stale-base records cannot launch as a clean current source.
- DOWNTIME-ENGINE-03: actual Engine daily capabilities additionally expose the downtime destination only for an actual Ship and target writer. Noon/Deck never gain this entry. The strict frontend daily decoder retains its bounded five-destination contract and rejects injected source-kind or query-authority links.
- DOWNTIME-STATE-04: source field values, summaries, declared times, units, human reviews and command keys are never copied into a target record. The owning target source reader establishes the exact immutable source version; prospective stale sources cannot silently rebase.

The existing shared Button, compact module sidebar and explicit form-action layout remain the presentation foundation. No duplicate shell or module-specific control-size rules are introduced.

## Authored verification sources

[Target/source capability component cases](../../frontend/src/product/DowntimeSourceLaunch.test.tsx), [Engine source launch cases](../../frontend/src/product/DailyOperationLaunch.test.tsx), [scoped frontend navigation cases](../../frontend/src/product/downtime-navigation.test.ts) and [actual API/policy navigation cases](../../tests/Poseidon.Api.Tests/DowntimeNavigationTests.cs) accompany the implementation. The owning domain/backend/UI packages supply their own domain, actual API/PostgreSQL, protocol/component and signed journey sources. None ran; no build, typecheck, lint, generated contract, migration parity, browser, provider or CI result is claimed.

Actual larger PR preparation must validate all integrated sources on its frozen target, including current/historical/transitive permission races and the still-open approval/evidence/closure gates. No private fixture, running preview, provider or publication is activated by this source package.
