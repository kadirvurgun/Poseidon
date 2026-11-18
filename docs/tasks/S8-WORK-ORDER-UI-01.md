# S8-WORK-ORDER-UI-01 — Owning work order enterprise source extension

Status: IMPLEMENTED_SOURCE_ONLY / verification DEFERRED. This is not canonical DEV-054 completion or S8 acceptance.

## Controlled sources and scope

- Active baseline: `poseidon-v46.3-20261030`, [active source](../sources/active.json).
- DEV-054 / T-S8-003: 18.01 Backlog row 55, SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`; dependencies DEV-053 and DEV-029 remain subject to the owning package gate.
- 14.02 Data Dictionary 2316 rows 1426–1463, SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`: 38 occurrences / 29 unique dictionary keys; 24 client facts after excluding trusted vessel context and four server-state fields, plus backlog execution phase = 25 facts.
- 13.04 design system paragraphs 10–25 and 26–34, SHA256 `83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c`: one primary question, current role/vessel context, full-page complex editor, human authority, progressive disclosure and no silent loss. Existing shared Button, EnterpriseActions, EnterpriseFormGrid, EnterpriseField and EnterprisePagination provide geometry; the form primary action is at the top right.

## Authored behavior and acceptance criteria

1. `WorkOrderWorkspace` supports `/app/work-order-center`, authorized vessel/tug selection, canonical `/{vessel}?id=UUID`, scoped server pages, actual current detail and immutable revision history. A local search explicitly covers only the returned page.
2. Optional `?planId=UUID&planRevision=N` opens an owning maintenance source context. The exact current server pin includes id/revision/actual uint version/hash/sourceVersion. The readonly plan/asset context never auto-fills unapproved equipment, job, criticality, dates or authority. Stale new origins block saving; existing historical pins remain explicitly retained.
3. The 25-field schema matches the domain contract exactly. Missing, Unknown and Known remain separate. Unresolved references/catalogs and the assigned_to person/date source conflict disable Known. Calendar dates stay DateOnly; declared decimal values remain lexical strings without guessed units, arithmetic or balance checks. Reversed planned/actual date ranges are rejected.
4. `RELEASED`, `IN_PROGRESS`, `EVIDENCE_SUBMITTED` and verification result text are unassessed declarations. The owning state remains DRAFT / REVIEW_REQUIRED; release, evidence adoption, verification, permit, safe-service and closure actions are unavailable. Required and conditional requirements remain visible blockers; incomplete drafts can be captured.
5. A draft UUID is stable across source revalidation. Mutations retain their exact key/body/base and original actor/tenant until the result is known. Session change hides private entries and blocks transfer. Post-receipt session failure retains the original command. After a confirmed original-session receipt, canonical identity is persisted before optional current reads; failed readback cannot create another resource. Only the fresh owning read establishes editability.
6. Shared components, grouped TR/EN labels, native keyboard controls, associated errors, semantic headings, guarded navigation and explicit offline/loading/stale/denied/conflict/unknown outcomes are authored. No fabricated charts or completion metrics.

## Files and test-source traceability

- [Workspace](../../frontend/src/product/WorkOrderWorkspace.tsx), [field form](../../frontend/src/product/WorkOrderFieldsForm.tsx), [record views](../../frontend/src/product/WorkOrderRecordView.tsx), [strict API adapter](../../frontend/src/product/work-order-api.ts), [source fields](../../frontend/src/product/work-order-fields.ts), [location](../../frontend/src/product/work-order-location.ts).
- [Workspace component sources](../../frontend/src/product/WorkOrderWorkspace.test.tsx): incomplete capture/current rehydration; confirmed receipt→GET503→same-id reopening; dirty discard; original-identity exact retry; postreceipt403; retained old plan pin; stale new origin; root picker.
- [Field component sources](../../frontend/src/product/WorkOrderFieldsForm.test.tsx): Unknown/multiline preservation, phase authority separation, unresolved Known disabled, date-only and associated readonly errors.
- [Adapter sources](../../frontend/src/product/work-order-api.test.ts): strict state/capability/pin/schema bounds, no arbitrary source references, lexical dates/decimals, explicit Missing/Unknown. [Location sources](../../frontend/src/product/work-order-location.test.ts): canonical round-trip and duplicate/private/mixed-selector rejection.
- [Genuine journey sources](../../tests/Poseidon.Browser.Tests/register-specs/work-order-cases.ts): `work-order-signed-unassessed-phase-exact-receipt`, `work-order-signed-confirmed-id-reopen-after-readback-loss`. The first uses a real response then aborts delivery and asserts exact replay/immutable history. The second uses actual linked plan capture, aborted current GET, durable identity/reload recovery and button geometry. No fulfilled synthetic business responses.
- [Private fixture loader](../../tests/Poseidon.Browser.Tests/register-specs/work-order-fixture.ts): `POSEIDON_BROWSER_WORK_ORDER_FIXTURE` must reference a bounded real file under `POSEIDON_LAB_PRIVATE`, schema `poseidon.work-order.signed-fixture.v1`, exact run/tenant/actor/vessel/plan identity and revision. Missing or invalid prerequisites FAIL; no skip. Fixtures were not provisioned and cases were not executed. Root owns central case registration and App/navigation dispatch integration.

## Verification and open gates

All tests, build, typecheck, lint, OpenAPI generation/parity, browser/lab/provider/CI and runtime activation are DEFERRED by the active user cadence. Source files and normal local commits are not PASS evidence. Independent target-commit QA and actual integration/visual checks remain required at the larger package/PR gate. Canonical screen/task/SP acceptance does not change.

Remaining source gates include approved catalogs/references and assigned_to type correction, qualified release/transition/evidence/verification/closure matrix, permit/risk policy, offline delivery, and independent package validation. No migration, seed, preview, provider, private user, push or PR was changed by this UI task.
