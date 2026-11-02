# RULE-OWNING-ADAPTERS-01

**INCOMPLETE_AUTHORED_UNVERIFIED_NOT_ACCEPTED.** Local product authoring checkpoint,
not a completed engineering foundation candidate. No SP awarded. All product
execution is **NOT RUN**. Publishing, acceptance and the later combined gate belong
to the coordinator. This task has made actual software changes; its remaining
items must not be reported as implemented by an Unknown observation.

## Receiving and literal selection

Receiving worktree was clean at d01f52a. Read final Crew task/phase, Foundation
reconciliation/evidence, qualification-wire-absence, regulatory admission and Tug
closure code/reports. Preserved 43536c9, 96c2901 and the ordinary Safety and Today
receipts. Historical independent 80b057a6d1830eb6c5fe5e33fc0adfc7ca132c44 is an
ancestor, not current QA. No merge, cherry-pick, reset or discarded partial work.
Backend/UI skills, AGENTS and architecture/automation/security/seed/dependency
rules applied. No MCP code graph was available; scoped Graphify query preceded
targeted code reads. No extra agent or account/auth/private configuration changes.

Actual selection is the unchanged **40 tasks/272SP candidate** in
[original candidate ledger](../phases/FOUNDATION-ACCEPTANCE-BUNDLE-01.candidates.json),
not every task mentioned by a later report. DEV-047/T-S7-001/Backlog48 depends on
020/015; 048/049/050/083/084/086 are not in this selection. DEV-009 selects source
golden dataset/behavior obligations; this report does not convert all later
operational production stories into predecessors or waive selected adapter gaps.

Active baseline: poseidon-v46.3-20261030. [Exact original source records and hashes](rule-owning-adapters-01.sources.json)
retain bounded original XLSX/DOCX locators after query_sources retrieval. Relevant
sources: 09.02 Input Catalog2–24/Golden Tests2–11; 09.01 paragraphs85/221/379;
18.01 Backlog21/27–33/48–51/53/81–85; 14.02 Dictionary1615;
15.01 paragraphs50/99/104/108/229–243; 07.01 Master Register71.

## Exact thirteen-input reconciliation

Seven predecessor profile adapters and the system mode/date/lifecycle adapters
are retained. These are the remaining **literal IDs**, not renamed report labels.
09.02 locators below are original Input Catalog rows; their G cells identify owners.

| ID / row | Owning entity, relationship and API | Qualification/read scope and original task boundary | Current authoring disposition |
|---|---|---|---|
|vessel.mainPropulsionKw /7|Versioned RegulatoryDraft/Revision; existing vessel regulatory-profile PUT/read/history and fact-attestations|Verified profile, not Technical text; DEV-020/T-S2-001 row21 + DEV-027/T-S3-002 row28; current vessel/profile/source grant|Implemented new producer, exact decimal wire, qualified consumer and tests below. No production verification created.|
|vessel.contractForConstructionDate /9|Distinct profile date + class/build record reference + exact ordinary StoredFile ID/revision/checksum; tenant/vessel composite FK on head/history|Class/build records, not build/keel/delivery alias; same selected020/027 with026/018/030 lineage; current file.read and exact scoped human source attestation|Implemented real file relationship/integrity, producer/consumer/migration/UI/tests. A textual reference alone cannot qualify.|
|vessel.operatingArea /12|TugJob.OperationArea and current plan/job relationship; tug-jobs/towage-plan|Selected081/T-TUG-002 row82 owns intake declaration, not active operation;027 consumer|Actual intake producer exists. Current operation adapter remains unfinished; cannot promote draft intention.|
|operation.jobType /13|TugJob.JobType/current job revision; tug-jobs|Selected081 row82; controlled human labels and source rule literals need explicit qualified mapping|Producer exists; evaluation reference/mapping consumer unfinished. No implicit Harbour Towage→HARBOUR_ASSIST mapping.|
|operation.profile /14|Job/towage-plan owning operation profile, not free text in evaluation|Selected082/T-TUG-003 row83 has planning; operational083/084 rows84/85 are unselected|No defined current authoritative writer in selected plan. Golden context consumer still unfinished; do not label it completed by draft geometry.|
|operation.serviceContext /15|Shore-owned service authorization related to real job|Selected081/082 intake/board do not grant authorized service; true external authorization distinct from consumer software|Owning relation/current consumer unfinished. No request-supplied AUTHORIZED_TUG_SERVICE assertion.|
|cargo.packagedDangerousGoods /16|Cargo plan; positive known false only|027/golden GOLD-008/009 consumer; no cargo-plan producer story identified in selected40. Later098/101 row99/102 activity logs are not cargo truth|Consumer/required owning cargo relationship unfinished. No missing→false default.|
|equipment.towingWinchFitted /18|Asset Register with exact equipment relationship and revision; equipment-hierarchy|Selected052/T-S8-001 row53 plus080 row81; equipment.read and source-qualified fitment meaning|Hierarchy producer exists; controlled fitment/source relationship and evaluation consumer unfinished. Name/type text is insufficient.|
|class.rulePack.IACS_M79 /19|Actual class rule-pack + survey/source record, never class society name|Selected026/027/029/030 and GOLD-002/003/006;09.01 paragraph221 requires actual pack validation|Owning qualified class-pack relationship/current consumer unfinished; genuine class/survey decisions external.|
|regulatory.ismApplicable /20|Qualified upstream applicability rule/evaluation, same vessel and current source/input lineage|Selected026/027; upstream rule/source policy required, not a human checkbox|Explicit upstream dependency consumer/current invalidation unfinished. Source-specific upstream ISM determination remains a real input.|
|regulatory.msmdVerified /21|Crew compliance + actual MSMD document/verification relationship|Owning050/T-S7-004 row51 unselected; not a047 predecessor. GOLD-004 must retain APPLICABLE + readiness review, not silently N/A|No completed MSMD workflow/consumer. Selected golden consumer obligation and later owning production story remain distinct.|
|crew.requiredRoleEligible /22|Eligibility evaluation tied to assignment/position/period and credentials/competence/rest/MSMD|Owning048/049/050 rows49–51 unselected;047 declarations do not grant eligibility|No completed owning evaluator. Keep exact required role/assignment relationships in any future adapter.|
|evidence.sourceVerified /23|Exact current Evidence→Control→Verification/source/evaluation relationship; evidence/verifications|Selected029/T-S3-004 row30 +030/T-S3-005 row31; file/person/type rights before metadata|Command/verification chain exists. Input-reference/current consumer remains unfinished; clean transport and generic acknowledgment cannot replace source verification.|

The table separates later writers from selected consumer obligations; it does
not claim the remaining eleven adapters are completed or exclusively external.
GOLD-005 still needs its actual validated escort source/rule;49/50 proposals are
not that source. No normative catalog or source rule has been manufactured.

## Delivered code and acceptance cases for independent QA

Local ROA IDs below are engineering trace IDs, not invented source FR/TC IDs.

| Local ID / original locator | Actual behavior / authored tests |
|---|---|
|ROA-01;09.02 Input Catalog7,18.01 Backlog21/28|MainPropulsionKw uses invariant exact decimal strings, separate Missing/Unknown/Known draft states and profile history. Known remains Unverified until the existing source-reviewed HumanAttested policy and exact profile command qualify it. Declared basis is rejected for verified fields. ProviderVerified stays unverified without its real mechanism. `OwningProfileInputTests`, `RuleOwningProfileTests`.|
|ROA-02;09.02 Input Catalog9/Golden Tests3|Construction contract date is distinct from build date. Class/build reference and exact non-person/non-document clean vessel file are required for qualification. Profile head/revision carry exact tenant/vessel/file/revision/checksum FKs. Attestation reads actual object bytes and SHA256 through existing storage; a scanner status is not document truth. Current human source attestation remains a genuine external action. Domain/API/PG/protected-person negative tests authored. This alone does not complete GOLD-002.|
|ROA-03;15.01 current evaluation/immutable lineage +18.01 Backlog31/32|Expired or withdrawn/mutated source-attestation grants cannot promote facts. Current validity, control/evidence, human-review/recording and Tug consumers recheck the current scoped server appointment/hash. Stored history remains unchanged. Protected construction input rights precede profile history, evaluation detail/history/currentness and replay. `RuleOwningProfileTests` current authority/replay/person exclusions.|
|ROA-04;18.01 Backlog17/21,15.01 idempotency|Profile SaveChanges atomically retains head, immutable revision, audited file-stage xmin guard and exact original receipt outcome/xmin. Noops still append/touch xmin. Replay cannot substitute a later profile. Prior historical receipts lacking exact outcome fail explicitly; no backfill. Preserved predecessor race/receipt-fault tests; corrected only the former latest-profile replay expectation to exact original outcome.|
|ROA-05;09.02 input field/API meanings,18.01 Backlog21/26|Real TR/EN profile controls and paged current vessel-file selector; unconfigured/empty/denied/offline/loading/dirty/uncertain/stale paths use existing flow. File denial without a selection does not withdraw unrelated profile rights; denial of selected input withdraws the view. Strict current/history wire checks reject missing pin, unknown members, numeric coercion, dates/units/hash errors. `RuleOwningInputs.test.tsx`, preserved profile/privacy/workspace tests.|
|ROA-06;seed/security policy|Additive hand migration20261102233000, nullable historical bindings and receipt outcome only. Existing add-missing incomplete fixture gets default Missing fields/no file pin on fresh import; operator JSON/revisions are not rewritten by reseed. `RegulatoryQualificationPersistenceTests` adds idempotency/operator preservation/immutable history coverage. No extra entity, approval or verification is seeded.|
|ROA-07;19.01 real host separation|Registered actual signed-host ordinary power/Unknown-contract capture/reload/history case, plus existing independent human source qualification and denied case. No mocked browser route or provider success. This is not a full multi-human source→evidence→decision journey or an executed gate.|

Actual APIs remain `/api/v1/vessels/{id}/regulatory-profile`, `/history`,
`/regulatory-source-governance/{rule}/fact-attestations`, existing source events,
applicability evaluate/current/history and downstream graph/review routes. Requests
capture declarations and referenced object identities; they cannot supply verified
status, Actor, scope, legal signature or derived readiness. New profile field/pin
shapes are authored software contracts backed by09.02; generated OpenAPI/TS/EF
artifacts are unchanged pending the combined gate.

## Crew handback against original fields

DEV-047/T-S7-001/Backlog48 remains incomplete/unaccepted. Keep all44 original
14.02 Dictionary1572–1615 FR46/TC46 field chains in crew-roster-01.fields.json and
the current identity/files/photo/medical/encrypted offline/current-rights tests.

* `crew-profile.status`, FR46/TC46-crew-profile-status, row1615 AO/AQ/AM explicitly
  says SYSTEM_DERIVED/DERIVATION_REQUIRES_SPECIFICATION/business meaning pending
  BA review. Neither employment state, eligibility nor declaration presence is
  its prescribed meaning. Controlled status derivation policy + implementation
  remain unresolved; no arbitrary state machine is approved by this checkpoint.
* `crew-roster.watch_group` is the actual selected, configured nonpersonal mutable
  capture adapter `crew.draft.watch-group.v1`; existing encrypted enrollment,
  restart, replay, conflict and current policy lineage code/tests are preserved.
  AB generic supported capture does not define an additional field/classification
  whitelist or authorize storing identity/medical/credential binaries offline.
  Further selected, defined mutable adapters still require exact field/policy
  reconciliation; macro offline acceptance is not claimed from this one slice.
* `certificate_state`, `competence_state`, `work_rest_state`, `fitness_state` and
  `eligibility_state` observations do not implement048/049/050. Their current
  protected read rights remain; unselected owning stories are not new047
  predecessors. No clinician/issuer/crew-source approval has been made here.
* Existing position/declaration policy fingerprints and immutable assignment
  lineage remain current configured governance, not a qualified production
  codebook. Durable source-owned catalog/status governance remains unfinished
  software plus genuine source-policy inputs, not completed by report labels.

## Execution, unresolved work and next step

**NOT RUN**: all suites/validators/restore/build/lint/typecheck/model/contract
generation/migration/seed application/browser/provider/security scans/QA/CI.
Only source retrieval/authoring, whitespace and Graphify AST maintenance are
permitted. Authored rows and cases have no discovered/pass count, duration or
result hash. Existing independent80b evidence retains its historical meaning.

Remaining software is concrete: eleven catalog paths in the table, complete
golden current operation/class/crew/evidence commands and invalidation; remaining
source-specific provider success implementations where a usable contract exists;
full multi-human signed-host fixture/journey stitching; Crew controlled status/
catalog and any genuinely selected defined offline adapters; whole-branch generated
EF/API/consumer integration. The two new paths still need execution and correction
of actual findings. No full local engineering completion is asserted.

External inputs are separate: real source bytes/editions and appointed source
reviewers, class/build/issuer/clinical/MSMD records, source-defined semantics,
real human approvals and source-required trust/accreditation/provider contracts.
15.01 paragraph50 makes the trust/signature provider optional;07.01 Master
Register71 says use qualified e-signature when implemented. Neither specifies a
production endpoint/schema/correlation/trust anchor. Required mechanisms stay
default-denied; a JWT acknowledgment is not a legal signature. This does not
excuse the unrelated software rows above.

Next **continue required authoring**, then the original single combined foundation
generation/model/build/test/security gate, independent exact-target semantic/
engineering QA, required CI and original-task acceptance. No repeated module
validation loop or assessment-only package is substituted. Canonical8/970SP,
2/121tasks,0/196accepted and the194SP target remain unchanged.
