# Maintenance current planning projection — 02.11.2026

Status: AUTHORED_UNVERIFIED. No canonical story points accepted. This is a foundation subpackage for DEV-053 and a prerequisite for further DEV-023 Today composition, not completed Today integration or a release gate.

## Source and scope

Active baseline: `poseidon-v46.3-20261030`. See [equipment/maintenance source contract](../tasks/equipment-maintenance-01.md) and [Today contract](../tasks/today-why-01.md). Bounded SQLite reads reopened 14.02 Product Wide Data Dictionary, sheet `Data Dictionary 2316`, rows1416/1417/1422/1423: next_due_date, next_due_meter, owner_role, criticality. Their FR46/TC46 maintenance-plan IDs remain source trace IDs, not passing test receipts. Owner and controlled criticality lookup semantics remain REVIEW_REQUIRED; this work does not approve a catalog, WorkOrder completion, postponement or operational fitness.

The existing planning arithmetic is an explicit draft calculation. Previously the maintenance list returned the frozen save-time calculation even after the calendar boundary or an observation revocation. The current read now computes a separate projection using one server reference clock, actual current observation assessment, scoped equipment revision, available job and currently eligible owner. Immutable revision history retains the original calculation and reference clock. No entity, migration, seed, business write path or generated contract is added. Existing authorization and attributed read audit remain required before returning data.

## Acceptance and authored evidence

| ID | Behavior | Authored coverage |
|---|---|---|
| MC-01 | Calendar boundaries are evaluated at server read time; saved historical result stays unchanged | Domain boundary case; actual-host/PostgreSQL current-read case |
| MC-02 | Revoked/stale/changed-policy observations never reuse a resolved meter boundary | Domain three-state theory; actual observation revocation API case also checks immutable history |
| MC-03 | Assigned owner is rechecked against current scoped operator provisioning | Domain context theory; same-session API permission withdrawal |
| MC-04 | Changed equipment revision requires review and clears stale boundaries | Domain context theory; real API equipment update followed by plan/history reads |
| MC-05 | Configured observation values replace client echo; missing assessment and Unknown inputs remain unresolved | Domain observation, missing-assessment and incomplete-input cases |
| MC-06 | Current projection does not append revisions or command receipts | Actual-host/PostgreSQL revision/receipt assertions |
| MC-07 | TR/EN current calculation explanation is separate from unchanged historical results | React mocked API disclosure/history case |

Authored:10 domain cases,3 new API cases,1 strengthened existing API case and1 React component case. No suite, build, typecheck, host generation or browser journey was run for this authoring checkpoint. Tree-sitter syntax inspection of6 C# and3 TypeScript/TSX changed files returned no parse errors; this does not prove compilation, behavior, PostgreSQL translation or acceptance. Required bundle checks and independent exact-target QA remain pending.

## Foundation gate context and next step

The separate immutable PR12 source e92581c full API attempt discovered595 cases and reached its1800-second execution deadline: INTERRUPTED_DEADLINE, with no proven complete executed aggregate. Source and binary hashes remained unchanged and owned ClamAV cleanup completed. The earlier594-case deadline and scoped82-unit/3-isolation passes are separate historical evidence. Do not rerun an unchanged full scope merely to repeat a deadline; add safe partial progress diagnostics before selecting the next measured execution strategy.

PR12 still has164 source-scan findings awaiting genuine independent native review and protected external trust. No security exceptions, trust pins, required checks or thresholds were bypassed. Continue Today/Why composition using the shared current planning policy, then combine product changes at a later immutable foundation gate. No merge or canonical sprint completion is claimed.
