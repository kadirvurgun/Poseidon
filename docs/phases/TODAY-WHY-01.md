# TODAY-WHY-01 — UNVERIFIED authored product checkpoint

Date: 2026-11-01. Current phase: larger foundation authoring; local checkpoint only. Not accepted, locally validated, sprint-complete or release-ready. No publication or independent QA was performed by this worker.

## Authority, reconciliation and sources

Read current AGENTS.md, backend/UI role skills, active.json, architecture, automation, security, seed and dependency policies. No extra agent was started. Independent prerequisite proof was read for exact DEV-OIDC-LAB-01 commit `93de7b6b376fb4592150c5c7b15a8c52e39bd4b3` and NAV-WORKSPACE-01 commit `ff1e95ebf5f43ba5883f69e5e52b0cb2f205b775`. Those approvals are historical local engineering evidence, not Today evidence.

Resumed existing uncommitted Today code/tests/contracts in the same worktree. Initial inspection found 11 tracked modifications (including the user cadence change in AGENTS.md) and nine new Today files. Nothing was discarded or recreated from an earlier baseline. The inherited cadence modification is left separately in the worktree. No auth/account/IdP/session redesign or changes to previous evidence. The existing authorization-result handler changes are confined to required Today read-denial audit events.

Active source root: `analiz/POSEIDON GUNCEL 30.10.2026`, baseline `poseidon-v46.3-20261030`. Scoped code discovery: MCP graph tools were unavailable; `graphify query "Today Why workspace navigation vessel profile authorization"` supplied local graph navigation. Bounded source lookup used `py -3.13 scripts/query_sources.py "why-engine" --limit 4` and the equivalent `"poseidon-today"` query. Read exact nearby chunks from the local SQLite index, not the whole corpus in model context.

| Controlled document | Exact locator / decision |
|---|---|
| 18.01 Development Backlog | `sheet:Backlog/row:24`, DEV-023 / T-S2-004, dependencies DEV-021/022; `row:25`, DEV-024 / T-S2-005, dependencies DEV-016/023. Canonical dependencies and acceptance remain open. |
| 11.01 Traceability Matrix | `sheet:196 Ekran v46.3/row:56`, poseidon-today, `/api/v1/poseidon-today`. |
| 15.01 Product API Specification | `paragraph:327-333`, Why endpoint `/api/v1/why-engine`; `paragraph:698-704`, Today endpoint `/api/v1/poseidon-today`. |
| 13.02 Screen Catalog | `paragraph:26-37`, Why; `paragraph:350-355`, Today. Inventory/screens do not establish an operational implementation. |
| 13.04 Design System | `paragraph:18-24`, role/mode-aware exception display, source/control/evidence/owner/next action, human authority and progressive disclosure. |

Naming decision: use the current traceability/API names `/api/v1/poseidon-today` and `/api/v1/why-engine`. Backlog `/today` and `/why` are shorthand; no duplicate incompatible aliases are added. Historical 26.01 runtime is not used as repo code or passing evidence. Documents 49/50 do not authorize operational policies or catalogs here.

## Delivered behavior and limits

The authorized navigation/search now links to `/app/today`. The TR/EN selector pages the existing scoped vessel directory; `/app/today/{vesselId}` resolves current vessel name/kind and edit capability from the server. Current tenant comes from trusted membership and the EF tenant context; a vessel ID is only a selector. Today and every Why expansion enforce DirectoryRead and vessel scope. Unsupported/client authority query keys and duplicates are rejected. Responses use the existing no-store middleware.

A real read adapter projects the existing persisted regulatory draft. One stable `regulatory-draft` advisory distinguishes absent draft from stored/unverified draft. TW-DRAFT-01 is a local informational projection identifier, not an approved regulatory rule. The 17 declared/missing/unknown input observations describe saved draft fields without exporting their values or private source reference. Declared zero and precise decimal data are neither rounded nor treated as missing. No number of known fields can produce Ready, NotApplicable, critical risk or clearance.

Coverage distinguishes an absent stored draft (Missing), a present unverified draft (Available), and unconnected OperationalTasks, SafetyDecisions, Permits and EvidenceVerification sources (Unavailable). A page with no returned cards says that this does not mean all clear. Priority/source/reason/next action are fixed supported advisory metadata. Owner remains null: no actual owner source exists. View/review links use the same authorized profile route. Why recomputes current observations, source observation date, save/read times, provenance and unavailable prerequisites after authorization; freshness is always Unverified. Save/read time does not verify the source.

UI includes loading, empty, transport/unavailable, 401/login, 403, 404, refresh/retry and stale/offline display. Disclosure supports keyboard activation and retains toggle focus; failures receive focus. Each expansion performs a new server request. A new Why 401/403/404 removes the previous Today vessel/card snapshot and source details. A vessel change unmounts prior data and aborts pending requests; late responses are ignored. Offline display never upgrades cached data to fresh, closes the disclosure and starts no source requests; reconnect initiates a new scoped Today read. Existing unsaved directory/profile guards continue to mediate Today navigation. Selector responses and Today/Why JSON are checked for scope context, fixed identifiers/destinations, coverage consistency, bounded pages, valid offset timestamps and supported states.

Read/invalid/not-found/denied/unauthenticated events use fixed TodayRead/TodayWhyRead actions, attributed by the existing SecurityAudit writer. Required audit failure returns 503 before data or denial. Endpoint metadata identifies denial actions, including route case/trailing-slash variants; arbitrary client paths/reasons are not audit actions or payloads. This is scoped HTTP read/denial coverage, not a claim of complete product auditing or GDPR/KVKK compliance.

No new persisted operational entity, migration or seed is needed for these read projections. Existing tenant FKs, audited SaveChanges, idempotent profile history/commands, CSRF and exact-decimal persistence are preserved. No fabricated tasks, task completion, signed evidence, human sign-off, risk catalog, permits, activation or other fake lifecycle. Crew/technical/safety/permit/evidence verification adapters, operational deadlines and source-backed owners remain unimplemented and explicitly unavailable. No production test-auth or external-IdP acceptance claim.

## Authored regression coverage — all DEFERRED

| Acceptance IDs | Authored coverage |
|---|---|
| TW-01 | `TodayAdvisoryPolicyTests.cs`: absent vs stored missing, unknown/zero/precise decimal, all declared still unverified. |
| TW-01/02/03 | `TodayTests.cs`: actual API host and PostgreSQL seed/draft reads; current Why recomputation after real profile save; absent draft Why; stable empty page; no Today lifecycle command writes; tenant/vessel denial and spoofing; revocation while other grants remain; anonymous and fixed invalid audit; required audit failure for success/denial and route variants. |
| TW-02/03/06 | `DiagnosticsTests.cs`: actual-host OpenAPI parity plus endpoint names/query parameters/status metadata and absence of shorthand aliases. Existing parity assertion is retained. |
| TW-04/05/06 | `TodayWorkspace.test.tsx`: mocked transport TR/EN, keyboard/focus/disclosure, refresh/Why retry, revoked snapshot removal, loading/late response cancellation, selector paging, offline/reconnect, status errors, strict contract/timestamp/coverage/authority rejection, dirty navigation guard. |
| TW-04/05/06 | `today.spec.ts`: authored mocked-transport desktop/mobile Playwright disclosure, keyboard focus, denial removal, refresh and browser offline state. This is UI coverage, not a real authenticated operational journey. |
| TW-04 | `WorkspaceTests.cs`: server navigation/search expectations include Today and retain scope isolation. |

## Execution record and unresolved gates

The direct user instruction and current AGENTS.md defer execution until the larger foundation is written. No backend/unit/integration/React/Playwright/Python suite, restore, build, lint, typecheck, browser/lab journey or host/contract generation was run in this continuation. The deliberately stopped prior Today worker/test tree is INTERRUPTED; no inferred failure or PASS is recorded. Prior OIDC/NAV results remain historical and cannot validate these changes.

The inherited `contracts/openapi.v1.json` and `frontend/src/generated/api.d.ts` additions were preserved unchanged; their generation provenance/parity is UNVERIFIED. They were not manually edited here. Actual-host export and generated consumer regeneration are DEFERRED to the combined gate; source-reading does not establish parity.

Deferred command inventory, for the coordinator's later bundle gate only:

- Locked .NET/npm restore and solution/frontend builds.
- `dotnet test --solution Poseidon.slnx --filter-not-trait 'Category=Integration'`.
- `dotnet test --solution Poseidon.slnx --filter-trait 'Category=Integration'` with real PostgreSQL and actual API/OpenAPI host.
- Actual-host OpenAPI export followed by `npm --prefix frontend run generate:api` and consumer parity review.
- `npm --prefix frontend run lint`, `npm --prefix frontend run typecheck`, `npm --prefix frontend test`, `npm --prefix frontend run build`.
- Existing configured desktop/mobile Playwright regressions and applicable real HTTPS/OIDC journey; Python/source/security/EF gates as required for the combined foundation.
- Independent QA of the eventual exact target commit and required coordinator CI before any publication/merge. No thresholds or skipped-check policy changed.

Only source/Git inspection, bounded local source retrieval and AST graph work were allowed here. `graphify update .` completed with exit 0: 3014 nodes, 4730 edges, 239 communities; 12 zero-node extraction warnings. Derived graph files are ignored by Git and remain available locally. This is AST-only metadata, not a product check. No product correctness result can be inferred from AST extraction or a local commit.

Current phase: UNVERIFIED supported Today/Why product authoring checkpoint. Next step: coordinator-directed source-ready regulatory draft package, then one combined final foundation gate and independent QA. No request for already supplied prerequisite proof. Canonical acceptance remains **8/970 SP, 2/121 tasks, 0/196 screens**. No push, PR, merge or self-approval.
