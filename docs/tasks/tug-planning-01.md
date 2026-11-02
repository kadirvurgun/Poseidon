# TUG-PLANNING-01 — AUTHORED_UNVERIFIED checkpoint

Status: **INCOMPLETE / NOT ACCEPTED**. Current phase: FOUNDATION product authoring.
Canonical **8/970SP, 2/121 tasks, 0/196 accepted screens** remain unchanged.
No publishing, merge, source/catalog approval, pilot/Marine/legal/safety sign-off,
provider setup or independent QA occurred. All suites, builds, typechecks,
EF/OpenAPI/contract generation, migrations, browsers and CI are **DEFERRED / NOT RUN**.

## Reconciliation and source authority

Receiving tree was clean at `3a03937`. No stopped-worker partial edits existed.
Normal `git cherry-pick -x 8be19ed554d20b75429a919fe83e8fe9f151cdf4` produced
`df96875`, before Tug authoring. It preserves the already-sent HTTP status on a late
failure and independently counts failure; its framework-feature test is authored,
not executed. Newer Startup/telemetry, all prior support provenance, intake lexical
rules, scoped audit history, privacy/context/search/photo and offline work remain.
No private configuration was read or changed. No extra agents were used.

Read AGENTS, backend/UI/coordinate skills; active baseline, architecture,
automation, security, seed and dependency rules; latest Domain/S0/Foundation and
actual Tug task/phase reports. MCP graph tools were unavailable; local Graphify
query and targeted source reads were used. Bounded `query_sources.py` retrieval
for DEV-080, assigned_tugs, tug_type and14.02 preceded exact workbook locators.

Active baseline `poseidon-v46.3-20261030`:

| Source | Exact locator / trace |
|---|---|
|18.01 Development Backlog, SHA09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674|Backlog rows81/82/83: DEV-080/081/082, T-TUG-001/002/003, each8SP; all pending|
|14.02 Dictionary, SHA3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018|Data Dictionary2316 rows1902–1931 retained context/intake;1932–1945 board;1951/1952 planned times;1957 assigned references|
|11.01 Traceability, SHAbc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9|2316 Alan v46.3 corresponding rows/FR/TC; e.g. FR-/TC-tug-job-board-planned_start, planned_end, assigned_tugs, available_bollard_pull, conflict_state; FR-/TC-tug-job-detail-planned_start, planned_end, assigned_tugs|
|POL-DEFAULT-01, architecture14.01 §6/§9, D-STATE-01|Incomplete draft allowed; explicit current access/audit; material conflict requires human resolution; no invented operational transition|

[Per-field source/FR/TC coverage](tug-planning-01.fields.json) records56 bounded
source rows with separate authored, partial/unavailable and out-of-scope states.
It is not a count of accepted fields or a generated production contract. Sources49/50
remain proposals; no catalogs or role appointments were manufactured.

## Authored behavior and acceptance boundaries

* Separate `tug.DraftPlans`, `DraftPlanRevisions`, `DraftPlanTugs` persist nullable
  planned times and normalized revision-local assigned references. Requested intake
  times never populate planned times implicitly. No approval/permit/status mutation.
* Ordinary PUT preparation commands are explicit Window/Assign/Reassign/Remove.
  Server requires actual Tug mode, current tenant/vessel/job access, attributed
  tug.write plus tug.plan.write, editable candidate membership, an explicit
  tenant/actor/context eligibility rule, current configured policy version, current
  request xmin and plan xmin. Generic directory/Tug read never grants assignment.
* Serializable transaction contains attributed audited SaveChanges, immutable
  snapshot/references and pinned receipt. Reuse conflicts, races and changed request
  bases return409. Exact ordinary replay reauthorizes current policy/rights and
  all original assigned references before returning original revision/xmin; it
  does not reapply an old command. Source/body/key/private values stay out of audit.
* Scoped options/detail/history/exact-revision routes are reachable under each job.
  History filters unauthorized referenced Tugs before bounding, returns no hidden
  total, and never substitutes current xmin for a historical version. Missing
  synthetic historical xmin is null. Read and write observations use existing
  closed TugRead/TugSave security events; generic audit summaries retain safe
  planned instants/source/revision and withhold bodies and command keys.
* `TugPlanning` operator configuration has explicit version/source/effective period,
  tenant/actor/vessel scopes, eligible Tug references, bilingual type options and
  source-versioned context bindings. Empty, duplicate, expired or mismatched
  configuration fails closed. No runtime policy/catalog administrator was invented.
* Actual normalized Technical observations feed the BP adapter only with current
  equipment/maintenance access, configured asset/source/unit/version and freshness.
  Missing, Unknown, Unavailable, Future, Stale, PolicyChanged, Revoked and
  DeclaredUnverified remain distinct. Source revision, observation UUID/time,
  provenance and policy are retained. No declaration contributes verified capacity;
  no authoritative verified BP provider is integrated by this checkpoint.
* Conflict evidence compares shared assigned Tugs across **all currently authorized
  draft plans**, independent of board paging. Hidden plan/reference counts never
  escape. Saved plan revision/source/time accompany Unknown/BoundaryTouch/
  Intersecting results. Overlap requires human review; no comparison returns
  operational clearance, NONE, safety approval or a fabricated expert decision.
* Board consumers display actual draft plan times/revision/assignments alongside
  requested times. Changed request revision produces a review warning; priority,
  required BP and owner still derive from intake. Verified capacity/readiness and
  operational conflict coverage remain explicitly unavailable/unknown.
* Reachable TR/EN planning editor uses controlled Tug selectors, separate Window
  save, explicit assignment/reassignment/removal, dirty parent navigation guard,
  exact frozen uncertain retry, explicit conflict discard/reload, withdrawal on
  denial, context cancellation and exact immutable revision review. Intake source
  codes retain lexical wire values while labels are localized. Test mocks verify UI
  behavior only; no browser/real-host success is claimed.

Migration `20261102130000_TugDraftPlanning`, designer and snapshot are **hand-authored**,
not generated/applied/parity-verified. Composite tenant FKs, Tug-kind checks and
history/identity mutation guards are included. Business mutations use no raw SQL,
ExecuteUpdate/Delete or request-created administrative actor/context.

Seed adds an empty incomplete SyntheticUnverified plan and initial snapshot for
the existing synthetic Tug job only, add-missing and preserving edits. No assigned
Tug, verified BP/evidence, appointment, decision, receipt or credential is seeded.
Two additional actual-insert expectations change78/79→80/81; these are authored
expectations, never observed execution. Existing seed assertions are updated.

## Authored checks — every execution DEFERRED

| Source | Meaningful coverage |
|---|---|
|TugDraftPlanningTests.cs|Incomplete windows; immutable input/reassignment/removal; duplicate/missing/reused reference conflict; operational transition denial; reverse-window/reference-smuggling/boundary distinction|
|TugPlanningTests.cs|Actual host/antiforgery/owned PG: requested/plan separation; atomic revisions/links/receipts/audit; pinned history/restart/ordinary replay; current policy/membership revocation; races; receipt rollback; beyond-page conflicts; normalized BP current/revoked/missing/stale/future/unknown/version change; strict body/offset/CSRF/key/scope denial|
|TugDraftPlanPersistenceTests.cs|Real migration/model fixture; empty idempotent seed/operator preservation; tenant/FK/kind isolation; EF and PostgreSQL immutable history/identity guards; existing pending-model gate retained|
|TugPlanningPanel.test.tsx and retained TugWorkspace tests|Incomplete exact command; controlled assignment; frozen uncertain retry; conflict discard; denial withdrawal; exact historical review/TR-EN; old-context cancellation; unavailable-policy controls; original intake assertions retained|
|register-specs/tug-cases.ts|Two additional registered real signed-host journeys: plan window/assign/reassign/remove/reload/exact history/board/dirty guard and separately labeled real-commit transport-loss recovery; no auth bypass or fake provider response|

Static source inventories are not discovered/passed tests. Missing real-host
configuration fails the future journey; it is never skipped into a pass. Later
gate must generate contracts/models, compile all consumers, reconcile snapshot/
migration parity, run all suites with PostgreSQL and signed-provider/strict TLS,
then obtain independent exact-target QA and required CI under the coordinator.

Source-only `git diff --check` and staged whitespace inspection exited0. Final
Graphify AST maintenance exited0 (6190 nodes/13043 edges/443 communities;36 zero-node
warnings and size-based HTML omission). These are not product checks. The local
checkpoint uses a per-command nonexistent hook path to keep all hook-triggered
execution deferred; repository hook configuration is unchanged. No later gate is waived.

## Actual remaining gaps and next dependent work

This checkpoint **does not finish DEV-080/081/082**. Remaining software is explicit:
authoritative verified available-BP integration; real certificate/crew/equipment
readiness and operational conflict evidence/authority adapters; supported durable
Tug offline capture/enrollment/proof/replay/conflict resolution; broader command
aggregates and operational lifecycle beyond ordinary drafts. The predecessor's
working offline command contract is only `crew.draft.watch-group.v1`; this package
preserves it and reports Tug offline **unavailable**, not a fake queued/captured
banner or an unsafe widening of its field/device policy. These are software gaps,
separate from external catalog/evidence/provider/Marine/legal/pilot decisions.

Current phase remains larger FOUNDATION authoring. Next dependent author continues
those integrations and completes source coverage, followed by the mandatory later
combined generation/model/security/QA/CI gate. This clean local checkpoint is
AUTHORED_UNVERIFIED, never a validated candidate, accepted story or release gate.
