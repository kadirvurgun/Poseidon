# ENTERPRISE-TECHNICAL-WORKSPACE-01

Status: **AUTHORED_UNVERIFIED / NOT ACCEPTED**. Same larger foundation branch,
`foundation/current-tug-today-20261102`; no canonical SP/task/screen acceptance.
Continues the enterprise presentation after [Tug](enterprise-tug-workspace-01.md)
while a separate agent authors Crew and another prepares the requested status report.

## Controlled source mapping

Active baseline `poseidon-v46.3-20261030`; [bounded receipts](enterprise-technical-workspace-01.sources.json).
Retains the actual owning workflows/source limits of [EQUIPMENT-MAINTENANCE-01](equipment-maintenance-01.md)
and [current Today/PMS projection](today-maintenance-01.md).

| Trace | Source / acceptance criterion | Authored test source |
|---|---|---|
| ETCH-STRUCTURE-01 | 13.04¶19–24,29–42; 14.02 Dictionary2316 rows1352–1367 / FR46-/TC46-equipment-hierarchy-*: actual complete authorized forest retained; grouped identity/classification/manufacturer/location/responsibility/notes and progressive lineage | `TechnicalReadViews.test.tsx`; retained tree/workspace sources |
| ETCH-PLAN-01 | 14.02 rows1406–1425 / FR46-/TC46-maintenance-plan-*: current due/reference/observation/source and owning context are separate from history, execution and fitness; exact decimal strings and Unknown boundaries retained | `TechnicalReadViews.test.tsx`, `technical-plan-contract.test.ts`, retained current/history workspace tests |
| ETCH-SEARCH-01 | 13.04¶24,105: returned-plan refinement includes job title/code AND equipment name/code/reference; empty register and no local matches remain distinct | `TechnicalReadViews.test.tsx` |
| ETCH-STATE-01 | 13.04¶21–23,112: summaries count only returned authorized hierarchy/plans; absent plan access does not imply zero maintenance coverage; NotDue remains a server calculation, not a green readiness declaration | `TechnicalReadViews.test.tsx` |
| ETCH-WIRE-01 | Existing TechnicalEndpoints.MaintenanceRow wire: reject malformed identity/source/calculation objects, numeric decimals, unknown readiness codes, invalid dates and duplicate plan IDs before rendering; retain incomplete drafts | `technical-plan-contract.test.ts` |
| ETCH-GUARD-01 | 13.04¶23,41–42,113–114: current-write-gated primary Add draft; existing commands/uncertain retries/dirty/history/observations retained; chrome closes mobile navigation before opening the owning dirty dialog | `TechnicalWorkspace.test.tsx`; signed-host `technical-cases.ts` keyboard/mobile assertions |

13.04's large-list server-search/pagination guidance is not declared complete:
equipment remains the existing complete authorized forest and the plan endpoint
returns its current vessel inventory. This continuation explicitly refines those
returned records locally. It adds no unprovisioned backend search or global totals.
Source status derivation/owner lookup/postponement decisions stay separately open.

## Code and behavior

[TechnicalReadViews](../../frontend/src/product/TechnicalReadViews.tsx) supplies
actual-scope metrics, plan list, empty selection and grouped equipment/current
maintenance detail. [Shared technical fields](../../frontend/src/product/technical-fields.ts)
keep edit/history and read labels aligned. [Workspace integration](../../frontend/src/product/TechnicalWorkspace.tsx)
retains its actual save/EF-backed server commands, current observation widgets,
history and guard ownership. Chrome anchors bypass the outer capture handler so
its mobile drawer closes before the dirty dialog; otherwise the dialog could stay
inside an inert main element.

[Current plan adapter](../../frontend/src/product/technical-api.ts) now validates
the consumed DTO shape and exact string boundaries. It does not reimplement the
server due formula, approve state transitions or claim generated OpenAPI parity.
No entity, migration, seed or dependency change is needed for these existing reads.

## Execution and next gate

All component/domain/API/persistence/browser suites, lint, generated/model parity,
security scans, independent QA and CI are **DEFERRED / NOT RUN**. A coordinated
build solely for the user's running preview is recorded separately in the phase
report after execution. Authored sources and compilation do not imply behavioral,
visual/a11y or canonical acceptance.

Current phase: larger foundation authoring. Next continue remaining source-backed
owning workflow/fixture/offline/generated integration, then one integrated gate
before a substantial PR, independent exact-target QA and required CI. Canonical
8/970SP,2/121tasks,0/196accepted and selected40tasks/272SP unchanged.
