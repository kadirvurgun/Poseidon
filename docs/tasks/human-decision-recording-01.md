# HUMAN-DECISION-RECORDING-01 — AUTHORED / UNVERIFIED / NOT ACCEPTED

Current phase: product authoring before **FOUNDATION-ACCEPTANCE-BUNDLE-01**.
Canonical8/970SP, selected40-task/272SP ledger and194SP goal are unchanged.
No accepted SP, operational decision, signature, source adoption or publication.

## Receiving predecessor and source resolution

Incoming branch `agents/9ff6b20f711a4809a7bf5a1a3ced7172` was clean at
`f300d27d44211e4264acd380f7893fa72033fafa`. Its actual HUMAN-DECISION-TRACE-01
task, phase, source records and runtime/test files were inspected and preserved.
The completed predecessor still limits Decision to PENDING/REJECTED in both
HumanReviewPolicy and CK_HumanReviewEvents, returning
source_admission_and_signature_unavailable for every other outcome. This is a
source inspection, not a failed test or an acceptance verdict. This successor
replaces that blanket restriction for recording human text only. Previous
verification, rejection, immutable events, receipts, seed and tests remain.

Active baseline: docs/sources/active.json, poseidon-v46.3-20261030.
Bounded local query_sources lookups for DEV-031, HumanDecision, Decision Center,
POST /decisions, human_decision and TR-013 preceded exact original DOCX/XLSX reads.
No MCP code graph tools were available; scoped local Graphify query was used.
Original source text and complete file SHA256 values are retained in
[exact source records](human-decision-recording-01.sources.json):

- 18.01 Backlog row32, SHA09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:
  DEV-031 / T-S3-006 /8SP, authorized role, context, reason, decision lineage;
  AI/generic admin cannot create final decision.
- 15.01 paragraphs97–99 and288–291, SHA68955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837:
  POST /decisions; authorized human reason/evidence context; current vessel
  assignment for onboard decisions, DPA escalation separate from operational approval.
- 14.01 paragraphs131–132, SHAc3f5e612d69e33d2e5e3a1227644cb9a0ec8937bdcf74b20d2b5a60b968344ab:
  subject_type/id, decision, actor_role/id, reason, evidence_context, decided_at.
- 11.01 current `2316 Alan v46.3` rows1810–1823,
  SHAbc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9:
  FR46/TC46-decision-center-{decision_id,vessel_id,decision_type,trigger_source,
  decision_context,recommended_options,risk_summary,source_trace,ai_confidence_state,
  authorized_role,human_decision,decision_reason,decided_at,status}. human_decision
  is Text, not an invented APPROVED enum. Type/status are controlled. Row mutation
  requires actual role/state policy, connectivity, attribution and privacy.
  `Rol Yetki` rows2–9 distinguish Master, DPA and independent Auditor/Verifier.
- 07.01 `Master Register` row71/TR-013,
  SHAe82429a3aea460149d58ccae8e81b4760263f7e64a2770ba716eadc5d790dc95:
  qualified e-signature is conditional, “Use when qualified e-signature is implemented.”
  This does not establish a universal signature prerequisite for DEV-031 text recording.
- 13.04 design/role/accessibility context is retained in the same source record.
  Architecture, automation, security, seed, dependency policies and both role skills
  were read. No dependency, lock, account, authentication or authority configuration changed.

## Delivered recording semantics and policy boundary

Actual POST /api/v1/decisions and /decisions/{id}/events now support RECORDED +
HUMAN_DECIDED. These are software recording markers, not a production controlled
catalog or an operational outcome. The human decision itself is trimmed private
text, separate from rationale, trigger and decision context. A new stream may record
directly, or finalize an immutable PENDING request. A RECORDED stream is terminal;
changed input requires a new review/record, never rewriting the old human text.

The existing trusted operator appointment is extended with an optional exact
recording policy: decision type, Control/Evidence subject type/id, Master/Tug Master/DPA
source-role assignment, HumanRecording semantics, source locator and optional
required gate with its exact source locator. No policy is installed by this task.
No role/type/subject is inferred from admin capability, IdP labels or client claims.
Unresolved types have no fallback. Source locator is restricted to
`15.01/paragraph:99;18.01/Backlog/row:32`; only text-recording semantics are supported.
The concrete tenant-local type code must be resolved/provisioned by a genuine
operator for that subject/role/operation; fictional test type codes are not runtime
catalog entries. Trusted provisioning must retain genuinely applicable qualified
signature/admission/approval prerequisites in RequiredGate/GateSourceLocator.
Such a policy returns409 decision_type_gate_unavailable with its source locator;
there is no API to clear the gate or activate the subject. Other semantics remain
denied, including source adoption, legal signature, permit and readiness release.

Actual actor identity, role reference, tenant, vessel/mode edit rights, appointment
validity and exact operation/evidence/control/type/subject binding are resolved
server-side. Absent/wrong/expired/revoked bindings deny. RECORDED requires
human.decide even on initial POST; human.review.submit cannot finalize text.
Master and Tug Master recording policies additionally require actual Ship/Tug kind respectively; DPA retains assigned recording scope only. The existing independently authorized verifier path retains its SoD. Decision
recording adds no universal independent-author/signature prerequisite.

Every record retains exact evidence/control/verification revisions, optional retained
evaluation and its immutable source/rule lineage, actual profile revision, server
policy/authority snapshot, private human text/reason, UTC savedAt (=decided_at),
and context hash linked to the prior request hash. The actual profile must exist
and match expectedProfileRevision; absence/change yields explicit conflict. A
PENDING verification is explicitly retained as PENDING; recording does not verify
it. Qualification is UNASSESSED and trace remains REVIEW_REQUIRED/complete=false.
AI/risk/recommendation inputs are unavailable, not fabricated human observations.
The broader Decision Center is not claimed complete by this DEV-031 package.

Audited EF SaveChanges covers head/event/receipt/audit in one transaction.
Input, verification, source/requirement/rule and profile heads are touched with xmin,
including noops. No raw SQL/ExecuteUpdate business mutation. Changed material
lineage rejects finalization and receipt replay; current history overlays review
required without changing recorded state/hash. Before paging, decision history is
restricted to the currently live original human appointment/policy/actor. A new
appointment does not inherit private history automatically; a broader separately
authorized audit/reviewer adapter remains future work. Legacy null-payload history
uses exact current appointment IDs; immutable predecessor receipts may omit the
new nullable field and the consumer recognizes that legacy shape only for old states.
Authority is re-resolved before replay, write and transaction commit. Stored rationale,
text and context never enter generic audit summaries. Successful receipt replay
returns original capture bytes only while the actual context and authority remain valid.

Additive migration170000 adds nullable DecisionJson and expands the check constraint.
No old rows, append-only triggers or receipts are rewritten. Destructive downgrade
is denied; rollback requires a reviewed forward retention-preserving migration.
No human actor/appointment, decision, verified evidence, signature or receipt is
seeded. Existing idempotent incomplete fixtures remain and reseed preservation tests
are extended. EF designer/snapshot and actual migration parity are deferred.

The actual TR/EN accessible trace screen offers recording from a verification or
pending decision, shows server type/subject options and explicit pending qualification,
collects decision text/context/trigger/reason, and preserves offline/dirty/ambiguous
retry/conflict/history behavior. Strict wire parsing checks nested policy, matching
subject/type, non-promoted qualification and command-bound recording outcomes.

## Acceptance criteria and authored evidence for independent QA

All following test sources are **AUTHORED / NOT RUN**, not accepted criteria:

| ID | Behavior and test source |
|---|---|
|HDR-01 / DEV-031 / T-S3-006|Master/DPA actual host recording with human text, reason, actor/role, immutable context and UNASSESSED qualification; direct POST and PENDING→RECORDED; exact receipts and terminal overwrite denial. HumanDecisionRecordingTests.cs.|
|HDR-02 / FR46/TC46-decision-center-authorized_role|Denied AI/service/unspecified/generic admin, missing role/type/subject/operation/vessel/tenant/mode, revoked/expired and unresolved policy; request actor/role/signature/policy/time claims rejected. Actual host/owned PostgreSQL tests.|
|HDR-03 / FR46/TC46-decision-center-decision_type|Conditional qualified-signature type denied with exact TR-013 locator; no fallback to recording. Host and React tests.|
|HDR-04 / FR46/TC46-decision-center-source_trace|Stale evidence/control/source/profile/verification causes conflict/review; original context retained in historical view; stale replay denied. Host tests and predecessor evaluation/rule tests preserved.|
|HDR-05 / FR46/TC46-decision-center-status|Same-key race/exact replay/reused-key conflict, xmin noops, receipt-fault atomic rollback and no extra audit; revoked history filtered before paging. Host and persistence tests.|
|HDR-06 / FR46/TC46-decision-center-{human_decision,decision_reason,decided_at}|Domain current/terminal transitions; persistence null legacy compatibility, content/FK atomic denial, immutability/reseed; audit excludes private text. Domain, host and PostgreSQL tests.|
|HDR-07|TR/EN keyboard-accessible recording form, missing data and source gate disabled; nested strict-wire promotion/forgery denial; signed-host recording journey with genuine preprovisioned synthetic-scope appointment. React/wire and registered human-review-cases.ts.|

## Changed files and checks

Product: backend/Poseidon.Domain/HumanReviewPolicy.cs;
backend/Poseidon.Api/HumanReviewEndpoints.cs;
backend/Poseidon.Persistence/{HumanReviewStorage.cs,AuditMetadata.cs};
backend/Poseidon.Persistence/Migrations/20261102170000_HumanDecisionRecording.cs;
frontend/src/product/{HumanDecisionTrace.tsx,human-review-api.ts}.

Test source: tests/Poseidon.Api.Tests/HumanDecisionRecordingTests.cs;
tests/Poseidon.Domain.Tests/HumanReviewPolicyTests.cs;
tests/Poseidon.Persistence.Tests/HumanReviewPersistenceTests.cs;
frontend/src/product/{HumanDecisionTrace.test.tsx,human-review-api.test.ts,human-review.test-fixtures.ts};
tests/Poseidon.Browser.Tests/register-specs/human-review-cases.ts.

Delivery records: this task/source record, docs/phases/HUMAN-DECISION-RECORDING-01.md,
docs/project-status.md/json; derived Graphify artifacts maintained separately.

Actual execution: **NOT RUN** for all suites/build/lint/typecheck/restore/OpenAPI/
model generation/scaffold/migrations/browser/provider/security scans. No discovery,
pass/fail/skip counts or durations. Only source retrieval/inspection, Graphify AST
maintenance and whitespace inspection are authorized here; final observations are
recorded in the phase report. No independent QA, CI or publishing occurred.

Remaining: actual generated API/client/EF parity and migration application; full
source/evaluation integrations and specific gated decision adapters; genuinely
provisioned role/type/subject appointments and broader protected audit/reviewer
access; production policy/source qualification and signature provider where required.
None of these gives a blanket reason to deny the supported human text recording.
Next: **FOUNDATION-ACCEPTANCE-BUNDLE-01**, real parity/build/test/security execution,
independent exact-target QA and required CI, then coordinator publication/merge.
