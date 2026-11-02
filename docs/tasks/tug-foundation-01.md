# TUG-FOUNDATION-01 — authored UNVERIFIED checkpoint

> Additive DOMAIN-COMPLETION-01 integration: exact authorized immutable audit revision drilldown (selected source facts, free text withheld), actual TR/EN review UI, current-session revocation regression and absolute BP lexical consumption are authored. Plan/assignment/lookup/BP/conflict/localization software remains INCOMPLETE. DEV-080/081/082 and24SP remain NOT ACCEPTED. See DOMAIN-COMPLETION-01 for current source and software states. All execution remains DEFERRED.


Status: **AUTHORED_UNVERIFIED / INCOMPLETE / NOT ACCEPTED**. No publishing, merge,
independent QA, canonical acceptance or awarded SP. The unchanged goal remains194/970.
All new suites/build/typecheck/contracts/migration/browser/CI checks are **DEFERRED**
under the01.11.2026 product-first cadence. This is product code with authored tests,
not evidence of executed behavior.

## Reconciliation and dependencies

The receiving worktree was clean at `5cb416f`. Earlier Crew, Equipment, regulatory,
IAM, navigation, audit and files authoring is preserved with its existing evidence
and UNVERIFIED limitations. The supplied local engineering prerequisite is
`80b057a6d1830eb6c5fe5e33fc0adfc7ca132c44`, with committed independent engineering
receipt [REG-BOUNDARY-TLS-01](../reviews/reg-boundary-tls-01-80b057a6d183.json).
This is not Tug acceptance or a production identity rollout.

No equivalent planning-window patch or origin receipt existed at the clean start.
Exact origin `40ec40cdef905ca3e7e0f0e5db56b0eb0ba76756` was cherry-picked with `-x`
as `c711fd8`. Its ten authored cases remain UNVERIFIED. Intake validation and board
assessment/comparisons call this actual policy. Separate/Defined are entered-data
math only. BoundaryTouch remains distinct; no turnaround buffer was supplied.

DEV-020/022 engineering prerequisites are supplied, not newly accepted here.
DEV-080→081→082 now have connected authoring, but full source acceptance remains
open pending the gaps and larger independent gate below. No extra agents, account
switches, live membership changes, sign-offs or publication were performed.

## Controlled source and criteria

Active baseline: `poseidon-v46.3-20261030`, from `docs/sources/active.json`.
Authority18.01 Development_Backlog_TR.xlsx SHA
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`:

| Locator / story | Required behavior | Authored result / remaining criterion |
|---|---|---|
| Backlog81 DEV-080 / T-TUG-001 /8SP | BP/type/equipment/role Tug context; Ship hidden unless cross-mode | Actual Vessel kind/grants, trusted mode.tug/mode.cross + tug.read, actual BP version with unknown source observation/freshness; scoped Asset declarations + real role references. Tug type and verified adapters unavailable; full criterion OPEN. |
| Backlog82 DEV-081 / T-TUG-002 /8SP | Job ID, assisted vessel/type/window/area/count/BP/priority/owner/audited create | Server job number, normalized scoped draft aggregate, all intake fields, actual provisioned owner references, xmin/revisions/receipt/audit and TR/EN editor. Approved submission/activation/state matrix and conditional predicates remain OPEN. |
| Backlog83 DEV-082 / T-TUG-003 /8SP | Planned window, assigned tugs, BP/readiness/conflict/priority/next action | API-derived board and assignment read adapter. Requested window is labeled as such, never an approved plan. Assigned IDs require actual stored assignment at this revision and current tug visibility. Controlled assignment coverage/available BP/readiness/operational conflict remain Unknown/Unavailable; full criterion OPEN. |

Supporting locators:10.01 paragraphs81..90;13.01 paragraphs23/50/87 (human
release authority);13.02 paragraphs1065..1080 (inventory);13.04 paragraphs20..34
(mode/context/explainability/no silent loss);14.01 paragraphs148..152
(`D-STATE-01`, contradictory lifecycle candidates);15.01 paragraphs1525..1549
(resource routes);16.01 OpenAPI candidate lines6081..6200 (draft fields, unknown
conditional predicates/stable-code decision),6241..6261 (draft policy);
19.01 paragraphs50/132 (Tug coverage and no override of Master/Tug Master).
Source49/50 proposal/expert/class/lookup gates are not catalogs or activation.

14.02 Data Dictionary2316 SHA
`3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`;
11.01 sheet2316 Alan v46.3 uses the corresponding FR/TC IDs. Field criteria:

| 14.02 row | Field | Traceability suffix (FR-/TC-tug-job-intake-) | Authored treatment |
|---|---|---|---|
|1914|job_id|job_id|server stable job number; client UUID identifies retry resource, not business number|
|1915|request_source|request_source|nullable draft, bounded source candidate labels|
|1916|customer_ref|customer_ref|bounded text, no remote lookup|
|1917|assisted_vessel|assisted_vessel|required for completeness; external descriptive text only|
|1918|assisted_vessel_imo|assisted_vessel_imo|nullable seven ASCII digits + checksum, no URL fetching|
|1919|job_type|job_type|closed source candidate labels, no production catalog approval|
|1920|requested_start|requested_start|required completeness; explicit UTC wire instant|
|1921|requested_end|requested_end|conditional, nullable; never filled implicitly|
|1922|operation_area|operation_area|required completeness, bounded text|
|1923|pickup_point|pickup_point|conditional nullable text|
|1924|destination_point|destination_point|conditional nullable text|
|1925|required_bollard_pull|required_bollard_pull|nullable invariant decimal string, numeric(10,2), no rounding/threshold inference|
|1926|required_tug_count|required_tug_count|required completeness, integer1..1000 storage/transport bound only|
|1927|special_requirements|special_requirements|nullable bounded text, withheld from audit summary|
|1928|priority|priority|required completeness, source candidate labels|
|1929|request_owner|request_owner|trusted provisioned role-reference eligibility, checked every replay|
|1930|job_state|job_state|server Draft only; D-STATE-01 blocks operational transitions|
|1931|notes|notes|optional bounded text, withheld from audit summary|

14.02 rows1902..1913 describe context,1932..1945 describe board. Corresponding
FR-/TC-tug-command-* and FR-/TC-tug-job-board-* apply. Job ID/type/assisted vessel,
priority/owner/BP derive from the persisted request. Planned start/end retain
`RequestedWindowOnly;NoApprovedPlan`. Assigned tugs derive exclusively from
stored revision-matched `TugAssignment` records after current kind/grant filtering.
Empty assignments are not availability evidence. Available BP is null, readiness
and conflict Unknown, with explicit missing-input reasons. Comparisons cover only
the visible page's declared instants and are separately labeled non-operational.
Detail rows1946 onward reuse intake data; Tow Master/comms/actual execution/
closure/evidence are outside this checkpoint and not populated by invention.

## Implementation and safety boundaries

`tug.Jobs` owns normalized input columns, tenant/vessel identity, immutable server
number, exact numeric BP, xmin and revision. `tug.JobRevisions` freezes the input,
source-contract version, provenance and save instant. `tug.Owners` contains only
references derived from explicit trusted provisioning; eligibility is rechecked
on each request. No names, issuer/subjects or credentials are returned in catalogs.
`tug.Assignments` is an immutable read integration boundary, not a public writer;
it requires tenant-correct job revision and actual tug references. There is no
assignment/dispatch/approval API or inferred appointment.

Controlled migration `20261102030000_TugFoundation`, authored designer/snapshot,
composite FKs, kind/check constraints and immutable history/reference triggers
are included. Generation/parity/real migration execution are DEFERRED.
Synthetic import adds one entirely incomplete FICTIONAL/unverified job + initial
revision for the existing synthetic Tug only, add-missing, preserving edits.
No owner/assignment/receipt/approval is seeded. Expected audited seed writes
increase61→63 (and one later operation62→64); these are authored expectations.

All writes use attributed audited SaveChanges inside a serializable transaction;
job/revision/owner/receipt changes commit together. No product ExecuteUpdate,
ExecuteDelete, raw SQL mutation or request-controlled administrative context.
Duplicate JSON members/case aliases, unknown fields, body bounds, query/header
multiplicity, invalid UUIDs/types/chronology/count/IMO/BP and stale versions are
rejected. Every replay reauthorizes mode/vessel/write/owner first and returns the
actual receipt's immutable revision and committed xmin, even after later edits.
Concurrent SSI/uniqueness conflicts return409; no silent merge.

Existing directory-read scope now filters actual kind before search, paging,
detail and shared-resource reads. Legacy Ship behavior remains available without
Tug mode. Tug-only membership cannot see Ship resources. `mode.cross` explicitly
allows Ship oversight for a Tug membership; it grants neither Tug read nor writes.
Directory-write/IdP role/body/header labels do not grant these capabilities.
Tug routes and navigation derive from the server. Existing synthetic API fixture
now explicitly provisions cross-mode for its historic mixed-fleet cases; it is
test-only and is not real signed-provider evidence.

TR/EN intake/detail/context/board routes use current API data and scoped URL
filters. Empty/loading/error/offline and exact-attempt retention are authored.
Dirty navigation uses the existing modal guard. Uncertain writes freeze the
original body/key/UUID; conflict retains input until explicit discard/reload.
Draft content stays in page memory, not localStorage. Durable offline queue,
device binding/restart recovery and complete localization of all source labels
remain OPEN; browser retry is not full16 offline synchronization acceptance.

## Audit coverage authored, not observed test evidence

| Action | Observation path | Coverage and limits |
|---|---|---|
|TugRead|context/catalog/detail/board + pre-handler authorization denial|Success/Denied/NotFound/invalid metadata; scope before projection/paging|
|TugSave|pre-handler denial, explicit validation/CSRF/owner/scope, Attempted, committed Success, replay|Required separate durable security observations, safe correlation/resource IDs; no query/body or personal data|
|TugJob/revision/owner/receipt|audited SaveChanges transaction|actor/role/capability/source, revision/xmin, safe allowlisted BP/count/time/reference/source-version before/after; free text and receipt keys/fingerprints withheld|

Audit history allowlists Tug resource types and requires both audit.read and
Tug scope/read; mode filtering precedes count/detail. Immutable revision API
for audit-drilldown still supports earlier profile/register types only; Tug
detail/replay lineage is implemented, dedicated audit revision projection is OPEN.
This is not full HTTP audit coverage, retention acceptance or GDPR certification.

## Authored checks and later gate

All checks below: **DEFERRED / NOT RUN**; no PASS claims.

- `TugPlanningWindowPolicyTests`: ten origin cases, including missing end/boundary touch.
- `TugRequestPolicyTests`: draft completeness, chronology, exact BP bounds, unknown/null,
  IMO/type/priority/count/owner validation.
- `TugFoundationTests`: real owned PostgreSQL/API host with synthetic identity,
  exact-revision/restart/revoked replay; roles/modes/search/paging/direct-read denial;
  BP/null-end/honest board; malformed/duplicate input; trusted-owner revocation;
  concurrent duplicate and receipt rollback; audit attribution/redaction.
- `TugPersistenceTests`: independent synthetic import/reseed/edit preservation,
  PostgreSQL tenant/kind/FK/revision immutability and model parity.
- `TugWorkspace.test.tsx`: mocked API UI exact decimal/null end, frozen uncertain
  attempt, dirty guard, honest board/detail URL, duplicate-selector rejection.
- `register-specs/tug-cases.ts`, registered in the existing private real-host runner:
  actual intake/detail/board/search/back/responsive flow, restricted direct URL
  and separately labeled controlled response loss after an actual commit.

Coordinator must provision disposable trusted capabilities in the existing
signed-provider/strict-TLS lab, run the larger backend/PostgreSQL/frontend/
contract/browser gate, repair failures and obtain independent exact-target QA
and required remote CI before publication/merge. Additional operational adapter,
full mode-matrix signed-provider, audit-store failure, untrusted enum catalog,
complete16 offline and expert assignment/dispatch cases remain necessary for
canonical acceptance. No invented pilot/Marine/owner/qualified-master sign-off.
