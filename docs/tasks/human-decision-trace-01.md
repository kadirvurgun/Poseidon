# HUMAN-DECISION-TRACE-01 — AUTHORED / UNVERIFIED / NOT ACCEPTED

Product code and meaningful test source are authored in the isolated worktree.
Execution is **DEFERRED / NOT RUN until REGULATORY-CHAIN-BUNDLE-01**. No publishing,
merge, independent QA, source acceptance, safety approval or canonical SP credit.
194/970 is the delivery target; the existing canonical8/970 ledger is unchanged.

## Source and predecessor

Active baseline: `docs/sources/active.json`, poseidon-v46.3-20261030. Immutable
prior sources and previous product/test source remain intact. The incoming tree
was clean at `3dba9c6`; its CONTROL-EVIDENCE-01 proof inventory was compared with
the incoming Git objects:29 source-byte matches,0 mismatches. This comparison is
not a product check or acceptance. Exact full incoming hash and compared paths:
[predecessor record](../phases/HUMAN-DECISION-TRACE-01.predecessor.json).
No MCP code graph tools were available; local Graphify scoped discovery was used.

Exact source text, hashes and locators are retained in
[source records](human-decision-trace-01.sources.json). Relevant mappings:

| Locator in current source | Requirement / contract |
|---|---|
|18.01 Backlog rows31/32/33|DEV-030/T-S3-005/8SP depends DEV-015,DEV-029; DEV-031/T-S3-006/8SP depends DEV-030; DEV-032/T-S3-007/8SP depends DEV-027:DEV-031. Dependencies are source declarations, not accepted predecessor gates.|
|14.01 paragraphs125–133,136|Evidence tenant/object/hash/provenance; Verification evidence/control/verifier/result/time/reason; HumanDecision subject/role/actor/reason/evidence/time; full source-to-decision chain.|
|14.02 `Read Model - Projection Gap` row30|Verification Gates has no controlled input fields; explicit query/action schema, scope, freshness and failure contract needed. TC-verification-gates-projection; DEC-DATA-07 remains REVIEW_REQUIRED.|
|14.02 `Data Dictionary 2316` rows1810–1823|Decision ID, vessel, type, trigger/context, read-only recommendation context, source trace, authorized_role, human_decision, decision_reason, decided_at, status. FR46/TC46-decision-center-{authorized_role,human_decision,decision_reason,decided_at,status}; placeholders/physical mappings remain unconfirmed.|
|11.01 `Rol Yetki` row9|Auditor/Verifier assigned authority and separation of duties.|
|11.01 `1342 Alan` rows571–576|FR/TC-regulatory-trace-{requirement_ref,owner_role,record_datetime,status,evidence_ref,verifier}; versioned references, immutable evidence, authorized human verifier and reason/transition prerequisites. No source placeholder becomes a production default.|
|11.01 `1900 Alan v46` rows1820–1821|FR46/TC46-decision-center-human_decision and decision_reason; human-only authority with server connectivity.|
|15.01 paragraphs98–102,288–291|POST decisions/verifications, Completed != verified; object/tenant/vessel/role scope, current onboard assignment and separate DPA escalation rights.|
|13.02 paragraphs1369–1372,1509–1512,2029–2032|verification-gates, audit-ready and decision-center screen contexts. This package is a composed trace/review consumer, not all196 screens or every Decision Center field.|
|19.01 paragraphs87–89,128,131–136,182|S-UAT-06 trace retrieval; unknown→REVIEW_REQUIRED; tenant/object isolation; shore/admin/AI cannot bypass Master/Tug Master; critical orphan=0 is a later gate, not an observed result.|
|09.01 paragraphs13–14,433–438,444–445|Applicability alone is not compliance; immutable old evaluation/evidence lineage survives changes; stale review and material conflict; bound human reason/expiry/actor and protected-data minimization.|

13.04 responsive/accessibility rules and architecture, automation, security, seed,
dependency policy and current AGENTS cadence were read. Documents49/50 remain
proposals with real expert/lookup/permit gates; no approval/activation catalog was
derived from them. There is no dependency or lock update.

## Authored behavior and acceptance criteria for independent QA

| ID | Authored behavior | Test source, NOT RUN |
|---|---|---|
|HD-01 / DEV-030|Explicit server membership ActorKind=Human and exact HumanAuthority appointment: tenant membership, actual vessel edit/mode scope, evidence, control, operation, local role reference, capability, validity interval and unique appointment ID. Default empty. No IdP/client role/name/admin inference. Live reauthorization before receipt and commit.|API default-deny AI/service/admin, scope/role/object/operation/time/duplicate and revoked-retry cases.|
|HD-02 / T-S3-005|New immutable PENDING verification request; independent reviewer cannot be original submitter or evidence/control author. PENDING→VERIFIED or REJECTED; VERIFIED→REJECTED (reasoned revocation), EXPIRED after validity elapses, or SUPERSEDED with proven input change. REJECTED/EXPIRED/SUPERSEDED terminal: new review requires a new stream. No prior record overwrite.|Domain transition table, independent/freshness/expiry/supersession and API synthetic independent-human branch.|
|HD-03|VERIFIED additionally requires exact retained scoped evaluation/rule/requirement/source links, current profile/input revisions, positive existing file transport, live actual byte/checksum access, declared observation/from/until/freshness and independent appointed human. Conservative expiry=min(declared validity, declared freshness, appointment expiry). This is a human evidence-review record, never source qualification/effectiveness/permit authority.|Actual API/owned PostgreSQL/scanner/file test fixture with explicit synthetic evaluation, historical source edit and supersession; missing evaluation denial.|
|HD-04 / DEV-031|Human may request a context-bound decision or record reasoned REJECTED review outcome. Decision binds an exact verification revision in same operation/evidence/control scope. All approving/final/release states are denied409/source_admission_and_signature_unavailable. No runtime external-signature adapter exists; no names, arbitrary signature strings or hashes masquerade as signatures.|API pending decision, wrong verification and final approval denial; domain final-state table.|
|HD-05|Audited EF transaction covers head, immutable event, safe audit and exact outcome receipt. xmin on stream and input heads, including noops; source/rule/profile heads touched under RepeatableRead so concurrent changes conflict. Same actor/key/body returns original capture-asOf envelope; reused key conflicts. Unique/serialization/deadlock paths roll back before scoped receipt recovery; storage failures503. Receipt records committed xmin. Raw rationale/body/keys never copied to audit summaries.|API receipt-fault rollback/audit invariant, same-key race, stale input, replay/reused key, CSRF/duplicate/unmapped body; PostgreSQL xmin/rollback/tenant-FK and append-only guards.|
|HD-06 / DEV-032|GET regulatory-trace/verification-gates/audit-ready anchored to exact visible vessel/evidence/control revisions. Every hop scoped before response/paging. Real Source→Requirement→Rule refs, applicability unavailable segment, control/evidence hashes, retained evaluation hashes only, verification/decision events, provenance and declared freshness. Complete=false / REVIEW_REQUIRED always; missing source/evaluation/signature reasons explicit. Missing/protected/wrong context404; missing capability403 before inference/counts.|API wrong tenant/context, history/no-authorities, repeated query and no raw snapshot exposure; strict React wire tests.|
|HD-07|Changes to evidence/control/source/requirement/rule/profile cause a derived current review-required disposition while recorded State and original context hash stay immutable. AsOf selects old source revisions/events and historical input-change checks; historical mode offers no authority options. Event captures actor, role, operation, authority ID/hash and time; hash is an observation, never a cryptographic signature.|Domain state overlay and API historical source edit; PostgreSQL immutable records and FK isolation.|
|HD-08|Real TR/EN UI embedded in saved evidence/control links, accessible labels/buttons, responsive wrapping, immutable history links and server appointment picker. Loading/empty/error/offline/permission/conflict/dirty-context and ambiguous same-command retry. No browser-selected role or permission grants; no local READY inference.|HumanDecisionTrace.test.tsx; human-review-api.test.ts; registered genuine-host human-review-cases.ts. UI mocks are test-only and not backend evidence.|
|HD-09|No normal synthetic human appointment, credential, verification, decision, signature, receipt or evaluation is seeded. Those are actual lifecycle facts. Existing idempotent incomplete control/evidence fixtures remain. Test-only PostgreSQL fixtures explicitly model fictional review branches through audited SaveChanges; reseed preserves operator/session facts.|HumanReviewPersistenceTests seed-empty/reseed-preservation case and actual-host synthetic fixture.|

## Concrete API and state contract

Commands: `POST /api/v1/verifications`, `POST /api/v1/decisions`, and
`POST /api/v1/{verifications|decisions}/{id}/events`, with exact `vesselId` query,
CSRF and Idempotency-Key. Strict closed16KiB JSON includes expectedVersion,
authorityId/operationId, evidence/control IDs/revisions/xmin, optional immutable
evaluation and verification pins, state, controlled reasonCode and private rationale.
Actor/role/tenant/time are resolved server-side. Duplicate/unknown JSON and unsafe
reason content fail400. No body ActorContext/adminseed/IgnoreQueryFilters.

Reads require existing graph/file/applicability/catalog scopes plus human.review.read.
Commands also require exact appointment and human.review.submit, human.verify or
human.decide. No appointments or actual memberships are installed by this package.
The operator must provision genuine human assignment/role/operation context through
the trusted server boundary; configuration is not itself a signature or legal review.
Final on-board appointments cannot be inferred from tenant-wide management.

The trace query requires vesselId, evidenceId/evidenceRevision,
controlId/controlRevision, optional offset and offset-bearing asOf. Pages25,
pins32, bounded request/rationale lengths are engineering bounds, not legal limits.
Unauthorized scope exposes no hidden counts or IDs. Ordinary vessel files only;
person/document/protected evidence needs its owning adapter and stays unavailable.
Historical reads reauthorize current visibility; they do not reconstruct old grants.
Returned Version is the current editing head token; exact saved revision and hash
are historical. Replayed receipts are original capture-asOf bytes, not live status.

PENDING requests may capture incomplete review context. They are draft review
requests, not submission/acceptance of required operational controls. VERIFIED
requires retained evaluation and current inputs; it cannot be obtained by the
normal current predecessor path because authoritative evaluate remains denied.
Ineligible/missing/unknown data cannot become No/Ready/NotApplicable.

## Open integrations and later gate

The predecessor source-admission/authoritative-evaluate write path remains
unavailable. Qualified evidence/freshness policies, source-specific acknowledgment
and external signature contract/validation, actual authority provisioning and final
operational approval adapter are not invented. No generic signing API or approving
outcome is enabled. New review streams are needed to attach newly available
evaluation context; existing bindings cannot be silently replaced. Full Decision
Center recommendations/AI/risk inputs and the broader regulatory source/control
qualification workflows remain outside this composed consumer.

EF runtime model and migration160000 are authored. Actual EF scaffold/designer/
snapshot/model parity, migration application, OpenAPI and generated consumers must
be reconciled at the single bundle gate. Every suite/build/lint/typecheck/restore/
browser/lab/provider/scanner/security scan and independent exact-target QA/CI is
**DEFERRED_NOT_RUN**. No pass/skip/discovery/duration counts or compatibility claim.
EF audit plus explicit read/security observations is not full HTTP/privacy/GDPR
compliance. Source/SME/pilot/legal acceptance stays separate from software QA.

Coordinator owns remaining integration, the combined gate, independent target QA,
publishing and merge. This package is a local UNVERIFIED product-authoring checkpoint.
