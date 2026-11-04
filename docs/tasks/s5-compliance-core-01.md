# S5 compliance owning-data workspace — backend authoring

Status: AUTHORED / UNVERIFIED. S5 acceptance remains **0/32 SP**. All test,
build, lint, typecheck, generated-contract and browser execution is **DEFERRED
UNTIL THE ACTUAL PR GATE**, as directed by the user. This author is not the
independent QA reviewer of these changes. No push, PR or runtime/provider change.

## Controlled requirements

Active baseline: [source register](../sources/active.json),
`poseidon-v46.3-20261030`. Original 18.01 workbook SHA256
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Original workbook and documents were read from the main repository's supplied
source root; no substitute source copies were manufactured in this worktree.

| Task / trace / locator | Source acceptance and delivered backend support |
| --- | --- |
| DEV-039 / T-S5-001 / Backlog row40 /8SP | Identity/profile/flag/class/type/GT/cargo/trading completeness; missing critical inputs produce REVIEW_REQUIRED. Saved profile assessment adds explicit Known/Missing/Unknown fields, preserves malformed legacy values as incomplete Unknown, and never enables submission or invents verification. Build date also follows prerequisite DEV-020/row21. |
| DEV-040 / T-S5-002 / Backlog row41 /8SP | Rule revision, captured inputs/hash, source pins, result/reason tree and current reviewer state are exposed from the actual qualified evaluation. Currentness is recomputed; a retained stale result remains immutable. Manual override remains explicitly unavailable because no source-authorized rule-override appointment/type contract is present; existing human Control/Evidence recording is not repurposed as rule approval. |
| DEV-041 / T-S5-003 / Backlog row42 /8SP | Each currently qualified applicable rule maps to exact requirement/source revisions and current authorized control heads. Existing ordinary control/evidence authoring retains owner/frequency/evidence/verification policy fields, actual SaveChanges audit and concurrency. Missing links are visible; link existence does not imply completion/verification. Unknown critical requirement coverage stays null, never a fabricated zero. |
| DEV-042 / T-S5-004 / Backlog row43 /8SP | Passport composes current applicability, control/evidence status, blockers, source freshness and onboarding with scoped drill-down. Unsupported vessel certificate catalog is explicitly UNAVAILABLE; clean files and crew declarations are not silently counted as verified vessel certificates. |

Dependencies remain source-owned: DEV-039 depends DEV-020/027; DEV-040 depends
DEV-027/039; DEV-041 depends DEV-028/040; DEV-042 depends DEV-032/041. Authored
prerequisites and an integration commit do not constitute their acceptance.

Supporting original reads: 15.01 API specification paragraphs594–599 (passport
surface, READY FOR BA/DEV REVIEW), SHA256
`68955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837`;
13.02 screen catalogue paragraphs261–265, SHA256
`b0b5e9fb3cad5137d76a4de912c3a60351b667d0aeb08252856e3a9445928b45`;
19.01 paragraphs72–74 (profile completeness/applicability review UAT), SHA256
`063823ea7d453adc91033c6eabb17f60906b4e6b9b4595be884eb12e02040a49`;
17.01 paragraphs73–79 (AI cannot issue safety decisions, bypass Master/Tug Master
or approve/publish regulatory sources), SHA256
`f31d9e5cd1d156a455284885309380838e4367b08932c47200f5f275cf547f60`.
Documents49/50 proposals were not converted into approved catalogs or permits.

## Routes and data boundaries

- `GET /api/v1/vessels/{id}/compliance-onboarding`
- `GET /api/v1/vessels/{id}/compliance-applicability-review`
- `GET /api/v1/vessels/{id}/compliance-control-plan?offset=0`
- `GET /api/v1/vessels/{id}/compliance-passport?offset=0`
- Canonical source alias: `GET /api/v1/vessel-compliance-passport?vesselId={id}&offset=0`.

All are reads. The existing mutation inventory remains **86 mutation route
templates /80 critical route templates /85 concrete critical operations**,
unchanged from this worktree's base. Historical82
native evidence is neither retargeted nor presented as proof of these additions.

[Onboarding policy](../../backend/Poseidon.Domain/ComplianceOnboardingPolicy.cs)
reports ten closed field keys and source-required blockers. Completeness applies
to the current saved revision, never unsaved local form data. Its highest result
is DECLARED_COMPLETE_UNVERIFIED; canSubmit is always false. The owning profile
SQL file gate runs before any field/revision metadata is exposed.

[Workspace endpoints](../../backend/Poseidon.Api/ComplianceWorkspaceEndpoints.cs)
require the same existing server rights as the owning control/evidence reader for
review/plan/passport. Tenant/vessel scope precedes source reads and paging;
unknown/duplicate query parameters and forged selectors are rejected. Actual
protected owning/crew/document gates filter evaluation visibility before its
captured inputs are loaded. No caller actor, tenant or appointment is trusted.

Passport composition uses one RepeatableRead database snapshot. The existing
qualified-current resolver rechecks original profile, rule, source, authority,
attestation and owning-input lineage. Control/evidence projections reuse the
owning visibility and verification calculations; they do not create a second
approval model. Each graph page is bounded25, evaluations64, links25 with an
explicit truncation blocker. UncontrolledRequirementCount is a link finding
within the current qualified catalog scope, and stays null when unavailable.
OrphanCriticalCount remains null because critical obligation completeness is
not established. No all-clear follows an empty page.

Current-response authorization additionally follows the S3 private-trace fence:
the trusted actor/tenant/role/capability/vessel context is compared by canonical
values after composition and after the asynchronous success audit. Each fence
opens a new trusted DI database scope outside the RepeatableRead transaction.
One fresh SQL statement checks live vessel scope, current and captured profile
file visibility, exact current graph revisions, returned linked controls and the
owning evaluation's protected file/crew/document visibility. A restriction added
during the earlier snapshot cannot retain access through that old snapshot.
Context changes during the final read also deny the response; denial audit uses
the current trusted identity and contains no response payload values.

A readable immutable evaluation remains `available=true` while `current=false`
after profile/source staleness. Recorded results and inputs stay unchanged;
current control-plan coverage and counts are withdrawn. Link truncation remains
an explicit blocker, but only the pageable control/evidence record pages drive
`hasMore`; a fixed truncated link set cannot advertise endless empty pages.

An attributed `ComplianceWorkspaceRead` security observation must persist before
success/denial response; no raw snapshot/value is put in that observation. No new
entity, migration, seed record or fake signed/verified fixture is introduced.
Existing mutable authoring uses its audited SaveChanges and exact receipts.

The new UI consumes these DTOs and the existing profile mutation; coordination
occurred directly with the S5 UI author. Generated OpenAPI/TypeScript remains
unchanged until actual host generation at the authorized later PR gate.

## Authored tests and remaining delivery work

[Domain tests](../../tests/Poseidon.Domain.Tests/ComplianceOnboardingPolicyTests.cs)
cover Unknown/Missing, blank Known values, invalid legacy declarations, exact
field keys and complete-but-unverified behavior.
[Actual API/PostgreSQL tests](../../tests/Poseidon.Api.Tests/ComplianceWorkspaceTests.cs)
cover saved-revision and immutable history, no read business mutation/receipt,
file-bound profile denial, canonical alias/duplicate/foreign selectors, current
graph heads, actual command-qualified exact requirement/source linkage, stale
profile invalidation without rewriting the original evaluation, explicit missing
certificate/critical coverage, and current permission withdrawal.
[Response-fence tests](../../tests/Poseidon.Api.Tests/ComplianceResponseFenceTests.cs)
author deterministic PostgreSQL statement barriers for actor/tenant/role/scope
withdrawal and a new private profile binding. An owned-test-database advisory
lock/insert trigger holds the real success-audit insert; permission or private
binding changes during that await must still deny payload bytes. Trigger DDL is
test infrastructure only; ordinary business mutations use the audited API.
Equivalent reordered server memberships remain readable. These are authored
regressions, not executed evidence.

**NOT RUN:** every authored test; restore/build/lint/typecheck; model and generated
contract parity; regression, source/security scans, ordinary/signed browser,
independent QA and CI. Source/diff review and whitespace checks are not a passing
test result. The coordinator will refresh the integrated AST graph and run the
required package checks only at actual PR preparation.

Remaining source/production gates: an explicitly source-authorized rule override
contract and controlled reason/audit workflow, a genuine vessel-certificate
adapter/catalog with its own current authority/freshness semantics, complete
critical-obligation coverage, independent phase/dependency acceptance and actual
pilot/SME/Marine/DR/release decisions. This package makes these gaps visible and
does not claim they are delivered or approved.
