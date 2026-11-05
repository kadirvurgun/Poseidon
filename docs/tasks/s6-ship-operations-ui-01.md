# S6 Ship Operations UI — Fuel / Port

Status: SOURCE_AUTHORING / ALL_EXECUTION_DEFERRED. Owner: UI author. No acceptance claimed.

## Controlled scope

- DEV-100 / T-OPS2-004, original 18.01 Backlog row 101, 13 SP: consumption/delivery/transfer/ROB, fuel/supplier/BDN, quantities/properties, discrepancy, compliance/verification/evidence and applicability. Dependencies DEV-097/099/030 remain owning dependencies, not satisfied by visual presence.
- DEV-101 / T-OPS2-005, row 102, 8 SP: timestamped port timeline, cargo/draught/tug/pilot/delay, Letter of Protest/evidence, voyage link and controlled finalization. Dependency DEV-097. This is not a Port OS.
- Original workbook SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`. Original Not Started statuses remain immutable. This selected UI scope totals 21 SP; canonical S6 remains nine tasks / 87 SP / zero accepted. DEV-102 is outside this package.
- Current source baseline is `poseidon-v46.3-20261030` from `docs/sources/active.json`; 14.02 SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`, Data Dictionary2316 rows 2186–2207 Fuel (22 fields), 2208–2225 Port (18 fields). Read H/I/J/M/P/Q/T/V/W/X and approval/lookup columns in bounded source retrieval. Evidence fields 2207/2225 do not establish a StoredFile mapping; unresolved references cannot become typed free-text catalogs.
- 13.04 SHA256 `83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c`, paragraphs 29–34 context/navigation/header, 80–89 unknown/status/history, 110–114 TR/EN, keyboard/labels/contrast/touch, 124–131 Why/offline/responsive.
- Scoped local Graphify query preceded targeted reads; MCP graph service was unavailable. Architecture and automation cadence were read. No historical prototype runtime or archive is current implementation/acceptance evidence.

## First shared source checkpoint

Presentation metadata covers all 40 source fields, grouped into task-oriented Fuel and Port sections. Mutable forms exclude Fuel vessel/compliance/verification and Port SOF state, which are server-owned. The grouped shared enterprise fields use associated labels, distinct Known/Missing/Unknown controls, explicit unknown catalog reasons, source-only units, lexical numeric text and multiline narrative inputs. Explicit-offset instants are an implementation transport boundary, not a source-approved legal date policy. No local timezone conversion or ROB/emissions/sulphur calculation is introduced.

`ShipOperationsFields.test.tsx` authors T-OPS2-004-UI-01 exact decimal/zero/missing/unknown behavior and unavailable reference/state protections, plus T-OPS2-005-UI-01 explicit-offset and multiline remarks with no guessed cargo/draught units. Synthetic schema fixtures mirror original source options; they are not configured authority or real endpoint evidence. These tests are NOT RUN.

## Coordinated contracts and next work

The domain owner authors `ShipOperationsPolicy` and the backend owner authors ordinary Fuel/Port storage/routes. The agreed draft protocol uses actual per-kind capabilities, closed fact schemas, actor-owned idempotency keys, immutable outcomes and expected versions. Exact endpoint consumers, origin context, list/detail/history, current-read fences and real-host browser sources follow their coherent source contracts. No unsupported filters, origin content, catalogue or file binding is invented.

Root owns backend navigation/lens admission, Daily source launch links, central browser registration and canonical phase reports. Optional origin uses only nonprivate kind/UUID URL selectors and a separately read exact server pin; it must never copy Noon/Engine summaries or unknown-unit consumption into Fuel mt fields. Supplied origin is a read-only lineage link, not new operational authority.

## Acceptance and verification

All DEV-100/101 acceptance criteria remain OPEN. Applicability, approved reference mappings, evidence adoption, Human review/finalization and offline policy remain owning source gates. Saving a draft cannot create READY, VERIFIED, FINAL, operating instructions, financial results or legal validity.

Tests/build/typecheck/lint/browser/generation/lab/provider/private/runtime/push/PR operations have NOT RUN. Local source/whitespace review alone is not passing QA, canonical completion or delivery acceptance.

## Owning API and workspace source checkpoint

The actual consumer now follows backend checkpoint 7f4c16b44ba0d21ebfc2e887a971170f1fd7aff0, domain checkpoint 6bb244a6760a9a34b9c30da5cce8ef48d37b6142 and root origin reader 763d5b3. Root's follow-up Ship-only visibility does not change the public DTO. Canonical API routes are `fuel-bunker-logs` and `port-activity-sof`; list/history accept only vesselId/offset. Current-page filters are explicitly local to returned rows and never sent as unsupported server queries.

The new standalone workspace includes authorized Ship selection, scoped human-readable vessel context, task-focused draft editor, browse list/recorded Port timeline, source facts and immutable revision history. Editing omits the browse list and uses a compact saved-source header. Actual server schemas retain unresolved source conditional predicates. Source calendar instants preserve their lexical explicit offset and seven fractional digits; validation compares event ordering without rounding to browser milliseconds. No quantities are converted, rounded, added or used to infer compliance. Missing and Unknown stay separate from zero.

Strict decoding requires all 22/18 read fields, actual source metadata and server-owned Fuel review/pending / Port draft states. Write bodies contain only 19/17 mutable fields and optional exact origin metadata. Actor/tenant boundaries are checked before publishing source data and before/after writes. Unknown commands retain exact original body/key/base/identity after late denial or a received receipt followed by session failure. The immutable outcome is not editable until a separate authorized owning current GET succeeds. Incomplete drafts remain allowed; no review/submit/finalization workflow is enabled.

Daily origin URL context consists only of originKind/originId/originRevision and is rejected with a saved id, duplicates, unsupported kinds, missing triplet members or private field values. Fuel may link Noon/Engine and Port only Noon. The owning exact revision/hash/version/source pin is read under current rights and never autofills a target fact. New commands cannot adopt historical source pins; existing unchanged historical lineage remains readable and unchanged. Source reloads do not generate a second logical new record: the editor uses one per-surface UUID, and confirmed creation replaces its URL with the actual saved id after the current read.

| Trace | Authored source evidence | State |
| --- | --- | --- |
| T-OPS2-004/005-API-01 | `ship-operations-api.test.ts`: closed fields/scope/capabilities, no READY/VERIFIED/FINAL, exact numeric/zero text, seven-digit event ordering, unavailable references and strict URLs/navigation | OPEN / NOT RUN |
| T-OPS2-004-UI-02 | `ShipOperationsWorkspace.test.tsx`: incomplete save, exact key/body, current GET, keep/discard, identity withdrawal and post-receipt403 retention | OPEN / NOT RUN |
| T-OPS2-004/005-ORIGIN-01 | Workspace source tests: exact read-only origin, no summary/unit copying, historical-source create blocking | OPEN / NOT RUN |

Root owns actual server navigation and Daily launch admission. Frontend closed App/navigation/search/lens consumers include both operational destinations as daily items and reject them in a Tug projection. Ordinary audited operation reason labels add only the four actual Fuel/Port create/update names. No real role mapping or provider is changed.
