# S3 regulatory experience — authored UI package

Status: AUTHORING CHECKPOINT / NOT VALIDATED. Accepted story points: **0**.
This package prepares existing regulatory surfaces; it does not close canonical
S3 tasks, approve sources, certify evidence, or manufacture human authority.

## Controlled requirements

Active baseline: `poseidon-v46.3-20261030`, read through the
[active register](../sources/active.json). Original source:
`analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`,
SHA-256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Original Backlog rows 27–33 were read directly after bounded local retrieval.

| Task / trace | Locator | Acceptance criterion |
|---|---|---|
| DEV-026 / T-S3-001 | Backlog row 27 | Draft/adopted/future/in-force/superseded ayrımı vardır; current source silent overwrite edilmez. |
| DEV-027 / T-S3-002 | Backlog row 28 | Flag/type/GT/build/cargo/trading context predicate'leri versioned; result APPLICABLE/NOT_APPLICABLE/REVIEW_REQUIRED + reason döner. |
| DEV-028 / T-S3-003 | Backlog row 29 | Owner/frequency/evidence rule/verification rule/state ve requirement links kaydedilir; orphan critical control raporlanır. |
| DEV-029 / T-S3-004 | Backlog row 30 | Evidence immutable metadata/checksum/provenance ile kaydedilir; many-to-many links; stale/expired/superseded durumu görünür. |
| DEV-030 / T-S3-005 | Backlog row 31 | Verifier authorization + separation-of-duties; PENDING/VERIFIED/REJECTED/EXPIRED/SUPERSEDED; evidence existence alone state'i kapatmaz. |
| DEV-031 / T-S3-006 | Backlog row 32 | Authorized role + context + reason + decision lineage kaydedilir; AI veya generic admin final decision oluşturamaz. |
| DEV-032 / T-S3-007 | Backlog row 33 | Requirement→control→evidence→verification→decision state ve timestamps source-grounded drill-down ile görünür. |

All seven original statuses remain Not Started; the 66 SP source scope is not
claimed as accepted. DEV-032 depends on DEV-027 through DEV-031. Those backend
and independent gate obligations remain separate from this authored presentation
package.

Other cited authority: 08.02 Regulatory Control Review paragraph 59 gives the
Requirement→Control→Evidence→Verification→Decision chain; 13.04 Design System
paragraphs 110–131 require semantic TR/EN labels, visible focus, text/state beyond
color, contrast, Why drill-down and responsive journeys. The bounded prototype
registry entries `audit-ready` and `requirement-master` were read as UI intent,
not runtime or test evidence.

## Delivered authoring scope

- [RegulatoryRegister](../../frontend/src/product/RegulatoryRegister.tsx): grouped
  regulatory-workspace navigation, bounded search toolbar, register cards,
  shared pagination, aligned draft actions and explicit submit/non-submit button
  semantics. Exact source/revision selection, immutable history, dirty guards
  and frozen command payloads remain server governed.
- [LifecycleApplicability](../../frontend/src/product/LifecycleApplicability.tsx):
  shared rule/context pagination and draft action group; the rule document has a
  full-width editing area within a responsive field grid. Existing preview versus
  qualified-current evaluation and review-request boundaries remain explicit.
- [ControlEvidence](../../frontend/src/product/ControlEvidence.tsx): shared vessel
  and register context toolbar, pagination across retained references, card-based
  record selection and grouped draft/submit/completion actions. Save remains an
  explicit form submission; non-submit controls cannot accidentally submit it.
  Existing file gates, exact links, orphan reporting and frozen attempts remain.
- [HumanDecisionTrace](../../frontend/src/product/HumanDecisionTrace.tsx): scoped
  history toolbar, review-event action groups, human-command hierarchy, recovery
  actions and shared event pagination. Provisioned authorities, separation of
  duties, unavailable gates, reasons and immutable command envelopes remain.
- [RegulatoryTraceLineage](../../frontend/src/product/RegulatoryTraceLineage.tsx):
  ordered source-grounded nodes show exact record/revision, source state, reason
  and timestamp. Integrity hashes have a distinct disclosure and are explicitly
  not authority. Unavailable nodes show unavailable/unknown data and do not expose
  a supplied ID, timestamp or hash. Existing verification/decision history stays
  separate and source grounded; absent events are not invented as completed.

Layout uses the established enterprise action, toolbar and pagination components,
shadcn Button/Badge and product tokens. No dependencies, API contracts, command
payloads, persistence models, grants or live runtime configuration were changed.
The authoring branch starts from clean S2 C6 commit
`75d734285706848e55e9a6276aaa64ac789b8e59`; frozen S2 integration is untouched.

## Authored tests and pending gate

`S3-UI-TRACE-01` in
[RegulatoryTraceLineage tests](../../frontend/src/product/RegulatoryTraceLineage.test.tsx)
checks exact retained IDs/revisions/timestamps, hash disclosure, unavailable-source
redaction, ordered TR/EN references and absence of invented approved states.
`S3-UI-REGISTER-01` in
[register tests](../../frontend/src/product/RegulatoryRegister.test.tsx) checks
keyboard register navigation does not issue a write and the explicit save still
issues one unchanged draft command. Existing authority, dirty, frozen retry,
history and offline regression assertions are retained.

Tests, builds, lint, typecheck, contract generation and browser/visual checks are
**DEFERRED** until the next larger bundle gate, per user cadence. No mocked or
unobserved visual behavior is labeled PASS. Graph refresh is delegated to the
coordinator after integration. Remaining acceptance requires original dependency
review, integrated backend behavior, actual complete checks, desktop/mobile
contrast/focus/geometry observations and independent target-commit QA.
