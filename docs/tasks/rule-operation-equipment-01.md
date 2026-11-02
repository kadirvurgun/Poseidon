# RULE-OPERATION-EQUIPMENT-01

**AUTHORED_UNVERIFIED_NOT_ACCEPTED.** Ordinary isolated-worktree checkpoint in
FOUNDATION authoring. All product checks **NOT RUN**. No canonical SP, original-task
acceptance, production catalog, external issuance or human safety decision added.

Received clean worktree at `d0b2513`. Inspected status/diff, final
RULE-OWNING-ADAPTERS task/source/phase/evidence and Crew/Tug/current-qualification
code. Preserved predecessor product/tests, ordinary receipts, the seven original
profile adapters, propulsion and distinct construction-contract date/file paths.
Historical independent80b remains historical, not current QA. No agents, account,
authentication/private-setting changes, publishing or merge.

Applied backend/UI skills and current architecture, automation, security, seed and
dependency rules. No MCP code graph was available. Scoped Graphify discovery came
before bounded query_sources and original workbook reads. Source records are in
[the task source file](rule-operation-equipment-01.sources.json); active baseline
is `poseidon-v46.3-20261030`, not immutable prior source history.

## Original selection and literal ownership

The original **40 tasks / 272SP candidate ledger** and original194/970 genuinely
accepted-SP objective remain unchanged. Selected18.01 rows21/27–33/53/81–83 support
these consumers and existing source/control/evidence/job/plan/equipment commands.
Rows84/85 (DEV-083/084) remain later operational writers, not invented board
prerequisites. Cargo activity/log stories098/101 are not cargo-plan truth. Source49/50
proposals are not adopted rules, role appointments or approved catalogs.

The following are local engineering trace IDs, not newly invented source FR/TC IDs.
09.02 row numbers refer to the **original Input Catalog**.

| Trace / source input | Implemented owning writer and current consumer | Exact remaining external input |
|---|---|---|
|ROE-12 / row12 `vessel.operatingArea`|Existing Tug intake writes literal `OperationArea`. Typed OperationReference pins actual current job/plan revisions/xmins and an exact ServiceAuthorization Evidence revision. File-backed independently verified operation and authorization, current complete intake/window, unambiguous current records and reviewed literal binding are required before the server supplies a known area. Plan geometry/intake alone remain unqualified.|Actual operation and service documents, source-defined area meaning/overlay and appointed humans. KOCAELI is a source example, not a built-in authorization.|
|ROE-13 / row13 `operation.jobType`|Existing intake `JobType` remains its original controlled human label. Source qualification now carries exact owner-value→rule-value+locator bindings, versioned with exact source bytes. No implicit `Harbour Towage`→`HARBOUR_ASSIST` alias; missing mapping stays Unknown.|A genuinely reviewed mapping for the actual source edition/context. The synthetic test mapping grants no maritime authority.|
|ROE-14 / row14 `operation.profile`|OperationReference captures the document's literal profile against actual job and plan revisions/xmins. Existing Evidence save/submit, source qualification and independent verification commands own its lifecycle. Current service reference and plan window must still match. No inferred profile from geometry; no Draft→Active transition or full083/084 implementation is claimed.|Actual documented operation profile and source-specific semantics. `CLOSE_QUARTERS_TOWAGE` and `SOLELY_LONG_DISTANCE_OCEAN_TOWAGE` are retained literally in fictional tests.|
|ROE-15 / row15 `operation.serviceContext`|Distinct ServiceAuthorizationReference contains issuing-document reference/context and exact job/plan relation. A real ordinary vessel file, source-reviewed document author/verifier roles, appointed independent verifier and current validity are required. OperationReference follows the exact service Evidence revision; replacement/revocation withdraws it. Software records an externally issued authorization; it does not issue a permit or Master decision.|Actual Shore Ops/issuer authorization bytes, edition/scope/validity and legitimate appointments/independent human action. No caller `AUTHORIZED_TUG_SERVICE` flag can qualify an evaluation.|
|ROE-16 / row16 `cargo.packagedDangerousGoods`|Typed CargoPlanReference is an owning document-backed Evidence record with plan reference and nullable Boolean. Existing command/file/source/review paths produce its revisions; evaluation receives its ID only. Missing, draft, unverified, stale or competing current plans do not supply false. Known false requires the same exact qualification as true. Full cargo manifest/voyage authoring remains its later owning boundary.|Actual current cargo plan/manifest bytes, completeness/meaning of its DG statement and source-appointed human review. No cargo-provider endpoint/schema is specified or invented.|
|ROE-18 / row18 `equipment.towingWinchFitted`|Typed WinchFitmentReference pins the real Asset revision/xmin plus register document reference and nullable fitment declaration. Composite FK, immutable Evidence revision, exact ordinary file, reviewed document policy and independent verification qualify it. Names, hierarchy labels and arbitrary Technical text are never winch truth; competing current records block selection.|Actual fitment/register evidence for the exact vessel/equipment, valid source policy and human verification. No normative equipment catalog is seeded.|

Dictionary/API continuation is explicit:14.02 intake rows1919–1922 and plan
rows1955–1957 keep their literal capture meaning.15.01 current evaluation/persisted
trace and exact idempotent result/base-version contracts remain. The new typed
document reference payloads are engineering extensions of the existing Evidence
resource, not claims that the source already specified these DTO names.

## Authored software contract

`ControlEvidenceContent` has four mutually exclusive typed document references:
`operation`, `serviceAuthorization`, `cargoPlan`, `winchFitment`. Unknown members,
mixed document/capability payloads, coerced booleans, partial identity/version tuples,
wrong-vessel equipment and self-service references are rejected. Null values and
missing relations are valid drafts. Required missing fields block submission;
incomplete source document policies/bindings likewise save only as drafts and
cannot enter source review.

There is no generic evaluator fact bag. Existing Evidence commands at `/api/v1/evidence`
and their submit/update/version routes author the documents. Existing
`/regulatory-source-governance/{rule}/events` records source-reviewed `literalBindings`
and `owningDocuments` policies with exact source locator/author/verifier role. Existing
independent `/verifications` commands check the document's source-defined reviewer
role and exact appointment; operation/service review uses the actual owning JobId
as OperationId. Capture and ordinary source acknowledgment alone never qualify an input.

`EvaluateCommand.owningInputs` accepts only `operationId`, `cargoPlanId`,
`winchFitmentId`. The server resolves current tenant/vessel, record type, job/plan/
equipment versions, service relation, ordinary file revision/checksum, reviewed
source/qualification hashes, independent verification, source/reviewer appointment
and expiry. Known text requires a reviewed exact literal binding. Known Boolean
requires positive document knowledge, including false. ProviderVerified remains
unqualified without its real provider mechanism; no provider protocol is invented.

Hand-authored migration `20261102234000_OperationEquipmentReferences` adds append-only
`core.OperationEquipmentBindings` and `core.EvaluationOwningReferences`, composite
tenant/vessel/revision FKs, constraints and immutable database triggers. Existing
Evidence heads supply xmin, immutable revisions, file/requirement edges and exact
original command outcomes. Captured evaluation references have real relational
FKs to their owning bindings. Head/revision/binding/evaluation/event/receipt/audit
writes use ordinary attributed EF SaveChanges inside their transaction. No raw SQL
business mutation, actor construction or administrative context is exposed to requests.
Schema SQL is confined to the unapplied EF migration.

Evaluation snapshots retain exact IDs/revisions/hashes, file pins, source-qualified
facts and current dependency provenance. Currentness separately re-resolves facts
and provenance, including formerly Unknown references. Job/plan/equipment/noop,
assignment-set, document/review/head, source/qualification, role/appointment and time
changes withdraw validity. Old snapshots, events, receipts and history are not
rewritten. Competing current documents do not let the caller select convenient
truth. The129-record transport bound produces review/Unknown rather than a partial
qualified selection.

Owning-document verification uses an independently applicable source/control
evaluation without owning inputs. Trace selection exposes that actual evaluation;
a self-dependent consumer evaluation is rejected as verification lineage. This
allows replacement documents to be reviewed after a consumer snapshot became stale.
The existing source-control rule must actually apply independently; no source rule
is invented to bypass that condition. A bounded recursion guard rejects cycles.

Current graph/evaluation/history/replay filters enforce input/equipment/review/Tug
rights and referenced vessel scope before rows/paging/metadata. Source/reviewer
authority is checked again on consuming current facts and before relevant commits.
Operational appointment records are observed; no dispatch/assignment authority or
new operational release writer was fabricated. Audit coverage is still not a claim
of complete HTTP audit or GDPR/KVKK compliance.

Applicability feeds the existing control/evidence/review currentness path. Tug
planning/board, and their Today consumers, receive `OperationContext` observations
from the same adapter. It can be Current only for the exact qualified operation and
service context; overall readiness remains separately conditional on other owners
and legitimate Master decisions.

React captures typed owning documents against real job/plan/equipment/evidence
references, distinguishes Unknown/Yes/No, and passes selected IDs to current
evaluation. Source review edits explicit document policies and literal bindings.
Closed wire decoders preserve the extensions and reject unexpected authority/Boolean
shapes. Existing dirty/conflict/frozen retry flows remain; offline current reference
selection is disabled and denied selections are cleared. No browser persistence,
automatic approval or source acceptance is introduced.

`OperationEquipmentFixtures.AddMissingAsync` is an **optional**, administrative
owned-lab fixture. It adds deterministic incomplete typed Evidence/head/binding
records, never rewrites operator data, and creates no files, observed cargo/fitment,
source policies, appointments, verifications, receipts or approvals. It is not
automatically called by startup or normal synthetic import; existing normal seed
and its audit expectations are preserved. Evaluation bindings are real command
observations and are not preseeded.

## Test sources and honest limits

Every row below is **AUTHORED / NOT RUN**. No pass, discovered count, duration,
result hash, migration result or browser/provider success exists for this checkpoint.

| Test source | Authored behavior |
|---|---|
|`OperationEquipmentInputTests`|Incomplete drafts/policies, required review blockers, exact mapping/no alias, mixed typed records, unknown-member/authority injection, Boolean coercion and version tuples.|
|`OperationEquipmentTests` in actual API collection|Ordinary command-produced Ship cargo true/false/unverified and fitment; Tug intake/plan/service/operation qualification; missing mapping and unverified authorization; exact source sample M79 profile exclusion/IMDG predicates; job/plan/equipment noops, source edit, verifier revocation and time expiry; immutable snapshot; protected history/read/retry denial; cross-vessel asset and caller fact rejection; duplicate race, exact receipt, and receipt rollback. Uses actual host, disposable PG and fictional scan-peer/document/source/actor fixtures; not a production provider claim.|
|`OperationEquipmentPersistenceTests`|Real PostgreSQL migration chain, optional incomplete fixture/reseed/operator preservation, EF/database append-only guard and cross-vessel Asset FK rejection.|
|`OperationEquipmentInputs.test.tsx`|Mocked component capture Unknown versus false, unavailable current references, closed wire authority/coercion/mixed payload rejection. UI-only evidence.|
|`register-specs/operation-equipment-cases.ts`|Registered genuine signed-host incomplete cargo/history case and Ship/Tug qualified owning-reference consumers. Positive cases require explicitly FICTIONAL command-authored, independently reviewed fixtures and source rules, current signed humans and real owned transport. Missing fixtures fail, never skip or fake success.|

Golden source IDs are not normalized:09.02 names `RULE-IMDG-42-24` and
`RULE-IACS-M79`; the actual09.04 sample has `RULE-IMDG-PACKAGED-DG` and
`RULE-IACS-M79-APPLICABILITY`. Tests load unchanged controlled sample bytes by their
actual IDs. IMDG positive/false/unverified variants use command-produced current
Ship facts. Operation M79 variants command-produce the four operational inputs and
use explicitly fictional complementary class/contract/winch domain facts; this is
not a full M79 production chain. GOLD-008's exact historical2026-10-04 clock journey,
full cross-owner GOLD-002/003/004/005 and signed multi-human fixture stitching are
not claimed as executed or accepted. Genuine class/compliance adapters are the next
required software task, not replaced with source-free truth.

## Phase, remaining authoring and next step

Current phase: **FOUNDATION authoring / scoped UNVERIFIED checkpoint**. Acceptance
criteria for independent QA are the six per-input contracts plus current authority,
immutable trace, exact retry/noop and atomic rollback above. They are authored
criteria, not passed checks. Existing normal receipts and historical independent80b
evidence retain their original meaning.

Next **RULE-COMPLIANCE-INPUTS-01**: implement the remaining exact class-pack,
upstream ISM, MSMD, required-role eligibility and evidence-source consumer paths,
with their real owning boundaries. Complete the combined signed-host fixture
stitching and original golden chains. Generated EF designer/snapshot/OpenAPI/TS
integration, build and every domain/API/PG/React/wire/browser/security/independent QA
check remain required at the one later combined foundation gate, with actual
findings corrected. No per-module execution loop was started. Graph AST maintenance
is also deferred under this group's explicit no-generation/no-scans instruction;
the existing graph was used only for scoped discovery.

External inputs are separate: actual source/document bytes and source-specific
literal/role/validity policies, real authorized humans, actual issuer/service/cargo/
equipment records, and any genuinely specified provider trust contracts. These
are not software acceptance and do not excuse remaining authored integration or
fixture work. No real issuance, Marine/clinical/pilot decision, legal signature,
approved production rule/catalog or verified seed evidence was manufactured.

Checks **NOT RUN**: tests/suites, restore/build, lint/typecheck, contract/model/
migration generation/application, seed execution, browser/lab/provider/scanner/
security scans, Graphify update, independent QA and CI. Source retrieval and editing
are not product verification. No publishing/merge. Added accepted SP **0**;
canonical **8/970SP,2/121tasks,0/196accepted** unchanged.
