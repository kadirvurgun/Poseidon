# ENTERPRISE-TUG-WORKSPACE-01

**AUTHORED_UNVERIFIED / NOT ACCEPTED**. Continues [enterprise foundations](enterprise-workspace-01.md)
in the same large foundation branch. No canonical task/SP/screen acceptance added.

## Sources, scope and acceptance

Baseline `poseidon-v46.3-20261030`; [bounded receipts](enterprise-tug-workspace-01.sources.json).
13.04¶19–24,29–42,104–114 controls role/mode navigation, manage-by-exception,
progressive detail, primary actions, responsive layouts, server pagination and
TR/EN/keyboard/touch. 13.02¶1070–1081 identifies the board/detail screens. 14.02
Data Dictionary2316 rows1932–1945 bind the board fields and their FR-/TC IDs;
detail rows1946–1956 bind the presented identity/vessel/operation/capacity fields.
Requested-window/source context also retains [TUG-FOUNDATION-01](tug-foundation-01.md)
and [TUG-PLANNING-01](tug-planning-01.md) source mappings.

This is an enterprise presentation/consumer continuation of the existing request
and draft-plan implementation. It does not implement every operational field or
resolve source `DERIVATION_REQUIRES_SPECIFICATION`, owner lookup or state-transition
gaps. Canonical readiness and approved planned/actual times remain separate.

| Trace | Acceptance criterion | Authored evidence |
|---|---|---|
| ETW-BOARD-01; FR-/TC-tug-job-board-{job_id,assisted_vessel,job_type,priority,next_action} | Responsive job rows expose identity, priority, current next action and guarded detail navigation; critical fields/actions need no horizontal scroll | `TugBoardView.test.tsx`; extended actual-host `tug-cases.ts` journey |
| ETW-WINDOW-01; FR-/TC-tug-job-board-{planned_start,planned_end} | Requested UTC instants, unverified draft-plan times and their source labels remain distinct; missing times stay Unknown | `TugBoardView.test.tsx`; existing planning/host sources |
| ETW-STATE-01; FR-/TC-tug-job-board-{assigned_tugs,required_bollard_pull,available_bollard_pull,readiness_state,conflict_state} | Page-scoped counts preserve server states; absent assignment coverage is Unknown rather than zero available tugs; exact BP strings and owning facts remain unchanged | `TugBoardView.test.tsx`, `tug-board-contract.test.ts`; existing Crew/Tug wire suites |
| ETW-FILTER-01; 13.04¶105 | Server search, priority and pagination preserve each other; applying filters resets offset; clear removes filters; offline refresh disabled | `TugBoardView.test.tsx` |
| ETW-DETAIL-01; FR-/TC-tug-job-detail-{job_id,assisted_vessel,assisted_vessel_imo,job_type,operation_area,required_tug_count,required_bollard_pull} | Group actual request data; missing required fields stay visible; edit callback exists only with current write access; field completeness is not approval | `TugRequestSummary.test.tsx`; existing dirty/retry/real-host journeys |
| ETW-WIRE-01; 13.04¶19,23 | Reject foreign/forged job href, duplicate jobs/assignment references and malformed explanation collections before rendering | `tug-board-contract.test.ts` |

## Implemented code

[TugBoardView](../../frontend/src/product/TugBoardView.tsx) replaces the960px
minimum-width board with one responsive representation. State badges and text
keep Unknown/Blocked/ReviewRequired visible. Full owning Crew/BP/readiness/conflict
evidence, assigned references, comparison scope and blocking reasons remain in
native Why disclosures. No geometry implies operational availability.

[TugRequestSummary](../../frontend/src/product/TugRequestSummary.tsx) groups request,
requested UTC schedule, route/operation and planning demand. Source/revision
lineage is available on demand. The existing [workspace](../../frontend/src/product/TugWorkspace.tsx)
owns dirty/uncertain-save guards, actual writes, planning and offline controls;
the new view delegates actions. One current-write-gated New request action appears
on command/board screens. Read-only users receive no edit callback.

## Verification and remaining work

Requested-preview TypeScript/Vite production build **PASS**, no tests executed.
Main JS837.23kB (gzip243.67kB), lazy ECharts435.13kB (gzip148.79kB), CSS70.61kB.
Main chunk size warning remains; no threshold relaxation. Current local HTTPS API
serves the new distribution. No backend/model/migration/dependency changes were
needed for this continuation.

New component/wire tests and real signed-host keyboard/mobile assertions are
**AUTHORED, DEFERRED / NOT RUN**. Lint, actual browser/visual/a11y regression,
backend/persistence/contract/generation/security suites, independent QA and CI are
also deferred. Source inventory/build are not passing tests. Prior preview and
historical test evidence retain their original scopes.

Current phase: incomplete larger foundation authoring. Next continue remaining
source-supported owning workflows/fixtures/offline/integration, then the single
integrated gate before a substantial PR, independent exact-target QA and required
CI. Canonical8/970SP,2/121tasks,0/196accepted and selected40tasks/272SP unchanged.
