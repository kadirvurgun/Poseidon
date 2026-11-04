# S1-CRITICAL-POSITIVE-JOURNEYS-01 — actual alias and lifecycle writes

Status: **AUTHORED; execution DEFERRED; acceptance false**. Eight new real-host
test cases target15 concrete critical operation IDs. These are authored cases,
not observed passing operations or a measured coverage percentage. No test,
build, restore, generation, scan, browser or provider operation was run here.

## Source and controlled scope

Active baseline: `poseidon-v46.3-20261030`. Original
[18.01 backlog](../../analiz/POSEIDON%20GUNCEL%2030.10.2026/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx),
SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`:
DEV-014/015/016/019 at `sheet:Backlog/row:15/16/17/20`, trace
`T-S1-002/003/004/007`. Literal requirements cover tenant isolation, server-side
role/vessel/object authority, denied mutation audit, attributed immutable
before/after/reason lineage and actual CI negative tests. Dependencies and
canonical acceptance remain with the integrated S1 gate.

The coordinator's current source-linked security contract defines83 mutation
route templates,77 persistence-capable templates and82 expanded operations,
including six Tug actions. Those are static definitions. Real route drift,
positive HTTP responses, committed attributed audit, negatives and independent
QA must run before the denominator becomes measured evidence.

## Bounded static positive-path gaps

Existing test code already submits actual successful canonical control
create/update/submit/complete, evidence create/update/submit, all four human
verification/decision operation shapes and Tug enroll/key/renew/revoke/replay.
These source observations do not imply those tests passed on the S1 target.

Targeted reads found no positive mutating requests for the graph aliases.
Technical root equipment aliases were only read; canonical maintenance was
exercised as an existing receipt replay, which does not witness its own write.
The maintenance job endpoint and Tug loss/cleanup lacked a positive mutating
request. Canonical evidence completion likewise needed a real successful
transition. The new [test file](../../tests/Poseidon.Api.Tests/S1CriticalWriteJourneyTests.cs)
targets these specific gaps:

| New concrete operation IDs | Authored behavior |
| --- | --- |
| `ControlLibraryCreate`, `ControlLibraryUpdate`, `ControlLibrarySubmit`, `ControlLibraryComplete` | Actual control-library alias draft/correction/submission/completion |
| `EvidenceGraphCreate`, `EvidenceGraphUpdate`, `EvidenceGraphSubmit`, `EvidenceGraphComplete` | Actual evidence-graph alias lifecycle |
| `EvidenceComplete` | Actual canonical evidence completion; preceding ordinary writes establish its genuine history |
| `EquipmentCanonicalSave`, `EquipmentAssetsSave` | Each root alias commits its own create and update, then shares cross-alias receipt replay |
| `MaintenanceCanonicalSave`, `MaintenanceJobSave` | Real canonical draft plan and its ordinary draft job reference |
| `TugOfflineActionLost`, `TugOfflineActionCleanup` | Fresh distinct real P256 enrolled device contexts and actual retained lifecycle records |

## Assertions and authority boundaries

Every new successful write binds the actual response's server correlation ID to
committed PostgreSQL `AuditEvent` rows. It checks actual operation name,
technical reason, tenant, vessel, actor, role reference and timestamp. A declared
test name or manually inserted `WebCommand` audit row cannot serve as evidence.

Graph journeys obtain source draft, review, distinct synthetic qualification and
evaluation through existing ordinary HTTP commands. They never insert verified
or qualified records directly. Each alias then writes an immutable four-revision
history; exact completion replay cannot add audit/history/receipts, stale edits
conflict, and removed write authority denies replay. Completion remains a
software declaration: `UNVERIFIED` and `UNKNOWN` effectiveness are asserted, with
no human review events or production/legal sign-off fabricated.

Equipment aliases each make real changes, verify their own audit operation and
test cross-alias replay, stale xmin and revoked authority without extra history.
The maintenance job's same-ID replay is one effect and a changed title conflicts.
Its canonical plan deliberately retains incomplete inputs and missing owner;
`ReviewRequired` and draft status stay visible rather than becoming readiness.

Tug loss/cleanup cases use fresh ordinary enrollments and real ephemeral P256
key enrollment. Their immutable lifecycle records preserve the original frozen
capture, request and plan. Loss blocks a correctly signed replay. Cleanup is
truthfully an allowed cleanup record and does not silently revoke its lease.
Both actions deny a subsequent request after write permission removal without
business audit or plan changes. Challenge, protected base and key material never
appear in audit summaries. No operational dispatch/approval is introduced.

## Required integrated execution

These tests reuse existing private partial-class fixtures and do not change
authentication helpers, provider configuration or product code. The coordinator
must integrate trusted command-context registration and the current runtime
observer before executing them. Their isolated authoring worktree is not an
independently executable acceptance target.

The final S1 gate must discover and execute the complete suites, collect actual
HTTP-to-committed-audit positive observations across all82 operation IDs, verify
the separate tenant/role/object negative matrix and existing immutable/rollback/
source-authority regressions, and obtain independent target-commit QA. Until then
S1 accepted points remain0/50; prior S0 acceptance is separate.
