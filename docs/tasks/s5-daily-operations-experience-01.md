# S5 canonical remainder — daily operations and tug context UI

Status: **AUTHORED / UNVERIFIED**. Canonical S5 is **8 tasks /69 SP /0 accepted**. The earlier four-task32SP compliance package is a selected subpackage, not the canonical full sprint. This source package adds DEV-080 and DEV-097/098/099 support. All test/build/typecheck/lint/browser/generated-contract execution remains **DEFERRED until actual PR preparation**. This UI author is not independent QA. No push, PR, runtime, provider or preview changes were made.

## Controlled source trace

Active baseline [active.json](../sources/active.json): `poseidon-v46.3-20261030`.
Original18.01 backlog SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674` was read at exact Backlog rows81,98–100. The other S5 tasks remain DEV-039/040/041/042 rows40–43; source acceptance is still open for all8 tasks.

| Task / trace / original locator | Source acceptance / authored behavior |
| --- | --- |
| DEV-080 / T-TUG-001 / row81 /8SP | Tug profile BP/type/equipment and role lens; Ship screens hidden except authorized cross-mode role. The existing context transport was unchecked and omitted type lineage. New scoped decoder verifies actual vessel/type-binding tenant, source revision/observation/options, BP provenance and equipment coverage; renderer displays real lineage and Unknown readiness. Existing server role lens remains the authority intersection; no client role label can grant a Ship destination. |
| DEV-097 / T-OPS2-001 / row98 /13SP | Single-entry Noon position/voyage/activity/speed/weather/fuel/ROB and Master-review/offline context. Grouped real declaration form, list/current/revision history and owning write transport now exist. No duplicate downstream fuel/emissions calculations or invented review approval. Dedicated secure offline and trusted human review are separately coordinated follow-ups; this checkpoint exposes truthful unavailable boundaries. |
| DEV-098 / T-OPS2-002 / row99 /8SP | Deck watch/nav/port-cargo/pilot-tug/ballast/safety/handover/officer/Master review; audit/offline. Exact source mutable fields, source-owned vessel/review values, isolated schema and history are consumed; unavailable reference owners cannot be typed into a false Known source. |
| DEV-099 / T-OPS2-003 / row100 /8SP | Engine/aux hours, fuel/lube, measurements/alarms/defects/WO/environment/handover/C/E review. Same owning boundaries and lexical numbers, with engine-specific grouped fields and explicit unresolved units/reference/review sources. |

Sources read with bounded retrieval, not full chunk dumps:

- 14.02 dictionary SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`, `Data Dictionary 2316/rows2128–2147` Noon20 fields,2148–2165 Deck18,2166–2185 Engine20; Screen Coverage196 rows187–189. Exact inline activity/watch/navigation/engine/environment values are preserved. Source-owned vessel/review fields are omitted from client mutation bodies:18/16/18 mutable fields. References include unresolved voyage/ports/officer/engineer/WO/evidence; no free-text lookup approval is manufactured.
- 11.01 matrix SHA256 `bc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9`, `v46.3 Delta/row2`, `196 Ekran v46.3/rows188–189`.
- 15.01 API source SHA256 `68955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837`, paragraphs1617–1627: owning canonical commands, idempotent retries, current authorization/concurrency, material conflicts, source-of-truth and TR/EN wire stability; paragraph1668 describes future Noon submission/review events rather than current approval evidence.
- 13.02 catalog paragraphs1137–1154 and actual12.01 HTML metadata for noon-report/deck-daily-log/engine-daily-log were read. Both identify20/18/20 source fields; catalog reports4 actions per screen while prototype metadata reports8/7/7. Those differing screen counts are not treated as authority to invent review/submit commands; the owning canonical DTOs govern this package.
- 13.04 design SHA256 `83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c`, paragraphs29–34,80–89,110–114,124–131: current context, aligned actions, Unknown/state text, language/focus/Why/responsive journeys. Existing shared patterns, shadcn Button and source tokens are reused.

MCP graph tools were not available. Scoped main-root Graphify query was used before targeted reads. Architecture, automation, active sources and UI skill remain controlling guidance. Root owns the shared AST-only refresh after integration.

## Actual owning contracts and implementation

[Daily API consumer](../../frontend/src/product/daily-operations-api.ts) follows actual backend checkpoint `8a68083132877686c02b8e5945b448ddaeaa6611`:

- `/api/v1/noon-reports`, `/deck-daily-logs`, `/engine-daily-logs` require exactly selected `vesselId`; capabilities/current/history and25-row pages remain scoped to kind/vessel/record. Strict decoders reject foreign scope, source-owned field/status expansion, invalid source pins, duplicate revisions and unsupported readiness/approval states.
- Capabilities carry actual58-field definitions across families: source row, type/options, readOnly/required, referenceUnavailable and unit. UI exact field metadata is sourced, not a second permission catalog. Only declared `nm`/`kn` units are shown as resolved; other measurement units remain explicit unavailable. Deck/Engine log_date is source Type=Text with unresolved operational format, so no extra ISO transport admission is invented. Noon report_date remains the actual ISO Date contract.
- Ordinary POST/PUT sends only `{id,expectedVersion,fields,reasonCode}` and CSRF/idempotency headers. A stable new draft UUID is retained. Lexical signed decimal text up to the owning48-character contract is preserved without Number conversion, rounding or implicit zero. Known/Missing/Unknown remain distinct. Narrative allows actual source-owned CR/LF/tab, other control characters rejected; source text/narrative limits are512/4000 respectively.
- Save produces only DRAFT with unverified provenance. Vessel and Master/C/E status fields remain server-owned. Existing PENDING/RECORDED_UNASSESSED metadata can be displayed separately, never promoted into safety approval or source-qualified sign-off.
- Unknown outcome captures the exact key/body/identity. A later403/401/409 after an earlier503 cannot prove that earlier commit absent: unknown stays sticky until an authorized matching receipt resolves it. No automatic write retry, fresh key or altered payload. Late write response crossing the committed identity fence remains uncertain rather than transferring prior private fields.

[Workspace](../../frontend/src/product/DailyReportWorkspace.tsx) uses exact tenant/actor ownership for retained editor/attempt state. Revalidation under another identity hides and locks prior values; only the original identity can resume an uncertain attempt. Nonuncertain local discard remains explicit. Current permission withdrawal removes write controls. Same-identity refresh retains the original base version and shows a newer-server warning; no critical silent overwrite.

[Daily routing](../../frontend/src/product/daily-location.ts) supports the exact scoped application paths plus validated id/offset query. Duplicate/unknown parameters, encoded context separators, invalid IDs and invalid offsets are rejected. Native history restoration and guarded navigation mirror the existing workspace pattern. Parent dirty/saving refs update synchronously, so an approved discard cannot encounter a stale dirty closure and ask twice. Back/cancel/Chrome navigation does not silently lose retained inputs.

[Field groups](../../frontend/src/product/DailyReportFields.tsx) use associated labels, explicit state controls, reference source explanations, optional schema-derived units/required markers, accessible safe validation text and responsive field/action groups. Normal revision/saved context is primary; UUID/concurrency/hash/source pins remain under source-reference details. No private vessel name is invented or fetched outside current scope; the shared topbar owns the real authorized vessel label.

[Navigation](../../frontend/src/product/workspace-lens-api.ts) accepts only exact bare/scoped daily destinations emitted by the current server, with the source-backed16-item bound. Actual backend lens policy filters Ship daily work out of Tug context without mode.cross; frontend never derives that grant from role labels or the context crossMode text. Root/sidebar/search/standalone dispatch are wired to implemented screens.

[Tug context](../../frontend/src/product/TugProfileContext.tsx) distinguishes unavailable equipment permission/source from zero declarations, shows exact unverified BP and configured type source lineage, and withdraws source details offline. Existing tug writes/planning and authoritative backend mode gates are preserved. The existing mocked tug fixture was aligned to actual source fields rather than relaxing the new decoder.

## Authored regression sources — NOT EXECUTED

| Source / traces | Meaningful coverage |
| --- | --- |
| [TugProfileContext.test.tsx](../../frontend/src/product/TugProfileContext.test.tsx) /T-TUG-001 | Foreign vessel/tenant binding, fabricated readiness, numeric coercion and source freshness rejection; type lineage; cross-mode text is not a grant; unavailable vs empty equipment; offline withdrawal. |
| [daily-operations-api.test.ts](../../frontend/src/product/daily-operations-api.test.ts) /T-OPS2-001/002/003 | Closed source schemas and server-owned states; no fake Ready/approval/offline grant; exact numeric0/precision vs Missing/Unknown; exclusion of vessel/review from body; exact key/body/version replay; isolated list/history; strict URLs. |
| [DailyReportWorkspace.test.tsx](../../frontend/src/product/DailyReportWorkspace.test.tsx) /T-OPS2-001/002/003 | Real transport save, disabled unresolved reference Known, no auto write, frozen exact retry,503→403→authorized receipt recovery, readonly Deck/Engine/history, one-confirmation keep/discard, beforeunload, delayed identity flip with no private body transfer, offline/unavailable source withdrawal. |
| [workspace-lens-api.test.ts](../../frontend/src/product/workspace-lens-api.test.ts), [enterprise-navigation.test.ts](../../frontend/src/product/enterprise-navigation.test.ts) | Actual daily destination projection only; absent roles do not grow new work; mismatched scoped hrefs rejected. |

Root owns additional Chrome/external/browser-Back desktop/mobile browser source, and later independent actual-host/whole-bundle execution. Synthetic transport fixtures are not approved vessel facts, seed credentials or pilot evidence. Source whitespace review is not runtime validation.

## Remaining coordinated work and acceptance

This checkpoint is coherent ordinary daily CRUD/history plus tug context authoring. Dedicated classified/encrypted daily offline adapter/panel is owned by the separate offline author; it must never reuse crew/tug grants or fall back to plaintext storage. Ordinary offline capability admits only paired false/UNAVAILABLE_POLICY or true/CONFIGURED_UNVERIFIED and exact family operation; true does not turn this UI into an installed adapter. Until its separate panel is integrated, capture is explicitly unavailable. No offline acceptance is claimed.

Trusted human appointment/review support is a separately coordinated backend/UI delta. No placeholder Submit/Ready or generic admin Master/C/E sign-off is implemented here. Genuine source/lookup/unit policies, downstream integration reuse and actual pilot authority remain open. Full generated contracts/build/tests/independent UI/authorization/offline/native history/CI checks are deferred to the actual PR gate. Canonical S5 remains0/69 accepted. Root owns project-status/phase reports and normal integration.
