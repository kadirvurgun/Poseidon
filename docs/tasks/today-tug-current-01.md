# TODAY-TUG-CURRENT-01 — current Tug / Today composition

Status: **AUTHORED_UNVERIFIED / NOT ACCEPTED**. Receiving commit `610074e`;
branch `foundation/current-tug-today-20261102`. Part of the larger foundation
continuation; no separate PR, push, canonical SP or phase acceptance.

## Controlled sources and decisions

Active baseline: `poseidon-v46.3-20261030`, as recorded in
[active sources](../sources/active.json). Bounded local retrieval and exact
source/hash/locator/text receipts are retained in
[the source receipt](today-tug-current-01.sources.json).

| Source locator in the current analysis | Requirement / trace |
|---|---|
|18.01 Backlog row24, DEV-023 / T-S2-004; row25, DEV-024 / T-S2-005|Exception cards carry priority, source, Why, owner and next action; deterministic loading/empty/error/offline behavior; current freshness and no unauthorized details. Existing story dependencies are unchanged.|
|18.01 Backlog row83, DEV-082 / T-TUG-003, dependent on DEV-081|Actual job board exposes planned window, assigned tugs, capacity, readiness, conflict and next action. This continuation consumes the already implemented job/plan paths.|
|18.01 Backlog row54, DEV-053 / T-S8-002, dependent on DEV-052|Deterministic maintenance context, job, interval, observation, criticality and owner. No postponement/completion writer is added.|
|13.04 paragraphs19–24; 13.02 paragraphs350–355|Role/mode-specific exception feed and progressive source/control/evidence explanation; authorized human decisions remain separate.|
|06.01 paragraphs199–215; 15.01 paragraphs1533–1554|Tug intake, board and detail APIs; no client-invented operational transition or dispatch.|
|10.01 paragraph295; 14.02 Data Dictionary 2316 rows1940–1941 and related plan fields|Why exposes current source blockers. The dictionary still marks canonical readiness derivation as requiring specification. This package does not supply that missing business definition.|

`TW-TUG-01` and its Attention/Review ordering are **local draft-planning
advisories**, not implementations of the canonical READY/CONDITIONAL/NOT_READY
state machine or maritime priority policy. Existing underlying Blocked/Unknown/
ReviewRequired projections retain their limited meaning. A complete preparation
record, current capacity certificate or separate draft windows never authorizes
operation. No generic administrator, AI, service, synthetic fixture or implied
Master appointment provides an operational sign-off.

## Acceptance cases and code/test mapping

| ID | Authored behavior | Authored coverage, NOT RUN |
|---|---|---|
|TT-01|Current persisted Tug requests and exact current immutable plan revisions feed Today. Plan absence, changed request, missing declaration, blocked owning input, intersecting/touching geometry and unresolved preparation have distinct explanations. Local precedence is conflict, blocking input, changed request, absent plan, touching boundary, incomplete request, other review.|Domain `TodayTugPolicyTests`; actual-host/PostgreSQL `TodayTugTests`; strict React/wire cases.|
|TT-02|Directory, current mode, `tug.read`, tenant and vessel scope precede querying jobs/plans. Every assigned Tug must still be visible; otherwise suppress the entire card before mixed-feed pagination and return404 for Why. No hidden counts, partial assignment or false Missing-plan disclosure.|API module/mode withdrawal, cross-tenant/object and inaccessible linked Tug cases; React coverage, wrong-vessel href and hidden-assignment rejection.|
|TT-03|Why re-reads each source and exact request/plan lineage. It uses the same owning `TugPlanningEvidenceAdapter` as board/detail, including all-visible-plan conflict comparisons beyond the returned page. A stored request-owner role reference is exposed only while the ordinary owning module still considers it eligible in the current tenant/Tug scope; revocation removes it. It exposes references, source facts and boundaries, without assisted-vessel text, job notes, person names, medical fields, raw credential data or invented owners.|API revision/history, owner withdrawal, outside-page conflict and read-only receipt assertions; strict owner/evidence decoder; selected-card explanations.|
|TT-04|Tug maintenance consumes `MaintenanceCurrentReader` for current equipment revision, eligible owner, job and normalized observation policy/revocation. Calendar/meter/condition calculations and observation freshness are assessed at the declared job end when supplied. Ordinary maintenance/Today reads continue using server read time. Immutable saved calculations, revisions and receipts are retained.|API current-owner withdrawal, equipment revision change, current calendar versus future job-end Due, revoked and future-stale meter observations; no business writes during projection.|
|TT-05|Seven explicit source-coverage entries add TugPlanning; unsupported/Ship context is Unavailable, an authorized source with no visible jobs is Missing. Mixed cards retain stable priority/key paging. Every Tug article has an accessible source/job identity and a bounded server-owned detail destination.|API empty/unavailable and paged conflict cases; React strict context/priority/coverage/duplicate/href validation; retained fixtures updated to seven entries.|
|TT-06|TR/EN Why explains plan/request revisions, assigned references, current facts and separate Master decision. Late requests, offline state and permission denial clear protected disclosures; authenticated source failure remains an error. Cross-workspace navigation keeps the existing dirty/uncertain guard.|React disclosure/keyboard/context/offline/denial sources; mocked desktop/mobile Playwright source; registered real signed-provider API/UI source.|
|TT-07|Today success/denial auditing remains fail closed. No new entity, migration, mutation path, capability grant, source qualification, human event or seed approval is required.|Actual-host audit-trigger failure test and unchanged command receipt/revision assertions.|

Test sources:

- [Domain policy](../../tests/Poseidon.Domain.Tests/TodayTugPolicyTests.cs).
- [Actual-host/PostgreSQL behavior](../../tests/Poseidon.Api.Tests/TodayTugTests.cs).
- [React and strict transport](../../frontend/src/product/today-tug.test.tsx).
- [Mocked desktop/mobile browser](../../tests/Poseidon.Browser.Tests/specs/today.spec.ts).
- [Registered real-provider journey](../../tests/Poseidon.Browser.Tests/register-specs/today-tug-cases.ts),
  included once in the shared register/direct-runner manifest.

Registration and authored assertions are not executed test evidence. Existing
synthetic add-missing fixtures remain incomplete; positive tests use ordinary
commands and explicit fictional configuration. No fixture is a verified
credential, approved catalog, qualified source, human appointment or pilot record.

## Deferred gate and remaining foundation scope

By the user's renewed instruction, **all suites, build, restore, lint, typecheck,
EF/model/contract generation, browser/provider/lab execution, scans, independent
QA and CI are DEFERRED / NOT RUN**. Generated API/TypeScript files are preserved;
the authored Why extension requires actual-host export and generation at the
later complete foundation gate. No generated parity or behavioral success is
asserted. Source whitespace and AST graph maintenance are recorded separately.

No new dependencies, persistent model or operational entities were introduced.
This package does not close all DEV-023/024/053/082 acceptance criteria. Remaining
foundation software includes job-specific qualified Crew/Credential/MSMD/golden
composition, additional source-supported offline adapters, complete distinct-human
provider fixtures and generated/model integration. Canonical field derivations,
production source/catalog adoption, legal/privacy/provider/Marine authority and
genuine pilot decisions remain separate where their sources require them.

Current phase: continued foundation product authoring. Next: continue those
source-dependent software gaps in the same substantial delivery bundle; run one
combined verification gate only when ready to prepare its PR, then independent
exact-target QA and required CI. Canonical **8/970 SP,2/121 tasks,0/196 accepted
screens** and selected **40 tasks/272 SP** are unchanged.
