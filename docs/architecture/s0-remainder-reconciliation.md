# S0 engineering reconciliation — 2026-11-02

PROPOSED / AUTHORED_UNVERIFIED. DEV004/T-S0-004 (18.01 row5, depends001) requires approved ADRs; DEV005/T-S0-005 (row6, depends004) requires approved core entities/keys/tenancy/version/audit/object refs/migrations. This matrix is an author proposal, not that approval. Existing ADR history and2316-field original inventory remain intact. Exact-target independent Engineering review/identity/date/decision evidence: PENDING.

| Concern | Actual implementation / current contracts | Proposed decision and exact unresolved boundary |
|---|---|---|
|Module/dependency direction|Domain pure policies; current API endpoints orchestrate shared PoseidonDbContext; golden consumers reference Domain directly|Keep modular monolith with owned aggregates. Admit current API/shared-context deviation; future application ports/event consumers are not already present. QA datasets/trace/task tooling stay read-only engineering inputs.|
|State/UNKNOWN|ApplicabilityObservation.cs, RequiredDataPolicy.cs, CredentialDeclaration.cs, SourceFreshness.cs; incomplete operational drafts|Preserve unknown/missing/stale/unverified/conflicting versus false/current. Primitive applicability and date observations grant no eligibility, current source validation or readiness. D-STATE-01 operational reconciliation remains open.|
|PK/FK/tenant|Existing EF tenant-compound keys/FKs/query/write guards; logical source entity names in trace.definitionChains|Source FleetVessels/CrewRoster/TugCommand etc are logical contracts, not proposals for duplicate tables. Actual Vessel/Person/Assignment/Asset/draft roots remain owners. Source physical mapping approval is still pending.|
|Version/audit|xmin, immutable revisions, ordinary command receipts/offline outcomes; EF audited SaveChanges; separate HTTP SecurityAudit/telemetry|No new business mutation path from tooling. QA fixture JSON is not EF seed or a receipt. Retain distinctions among write audit, HTTP/user actions, operational decision evidence and complete privacy compliance.|
|Evidence/object refs|SecureFiles exact scoped object version/checksum/scan/provenance; medical/private identity encryption|Dataset source scenario descriptions cannot manufacture verified evidence, health, credentials, photos or signatures. No object bytes or key/provider configuration added. ControlEvidenceStorage, HumanReviewStorage and RegulatoryQualificationStorage now author governed review/evaluation lineage; full operational release and real production authority remain separate.|
|Events/outbox/integration|Ordinary synchronous transactions and durable offline receipts exist; no general outbox dispatcher|Continue proposed transactional outbox only when a real asynchronous use case is selected; not claimed implemented. DEV011 unavailable retry/backlog adapters stay unavailable. No phantom healthy zero.|
|Offline/AI|Configured existing-Draft Crew watch_group and enrolled Tug draft-window capture/replay/conflict; public shell cache; product AI absent|Preserve exact supported surface and current rights. Golden offline conflict exercises the actual preflight policy, not full sync/authority. No source/permit/human decision automation. Other field/binary/safety and operational Tug-release offline stays a gap; the authored Tug draft-window adapter is retained.|
|ERP boundary|ErpReferenceBoundary.cs + schema + EF metadata tests, actual TechnicalEndpoints equipment/maintenance only|Retain reference-only decoder with exact expected context; no implied trusted runtime auth. Source11.01 row1870 technical-requisition-erp_reference belongs to a future unimplemented surface. No full finance/PO/AP/AR/payroll/warehouse/chartering expansion.|
|Physical evolution|Existing EF migrations/model and immutable source inventory; new golden/trace/task JSON/tooling|No new operational entity/table/migration is needed for read-only QA definitions. No seed edits. Future persistence must use reviewed EF migrations/tenant relationships/audited writes/idempotent synthetic fixtures. Provider/model parity and restore remain deferred.|

Original source identities:18.01 SHA09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674;14.01 SHAc3f5e612d69e33d2e5e3a1227644cb9a0ec8937bdcf74b20d2b5a60b968344ab paragraphs5–12/16–72/92–105/166–184;14.02 SHA3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018,2316 fields, physical mappings unconfirmed. Source-specific interpretations and actual previous evidence remain in the linked normalized plan and selected-source report, not erased by this matrix.

DEV011/116 scope decision proposed for independent review: literal18.01 row12 asks for a metric contract, row117 asks for the reference-only boundary and forbidden-table separation; neither alone mandates implementing the full future dispatcher, device fleet telemetry or ERP module now. Current actual counters, pure decoder and guard tests do not implement unavailable trusted adapters.23.05 paragraphs21–38 identify broader monitoring obligations;06.01 paragraphs308–310/15.01 paragraphs259–262/18.04 paragraphs27–30 permit selected reference context. No specifically required additional existing-surface runtime consumer was established in the examined source and actual endpoints. Preserve all unavailable descriptors and require source review of any disputed selected obligation before foundation acceptance. This is a bounded interpretation, not a waiver or a claim of complete integration.

All software/source reviews and execution are DEFERRED_NOT_RUN. Approve/reject these engineering decisions independently at the exact checkpoint later; Marine/legal/source owner/pilot authority remains separate and cannot be supplied by this author.

## Current S0 exit candidate scope — 2026-11-02

[Updated candidate](s0-current-engineering-candidate-20261102.md) is the controlling
engineering inventory for this review. Source/trace currently contains149 source
chains, the Today SOURCE_PROJECTION, events-learning capability9 and mixed-fleet
CAPABILITY_COMPOSITION. Old130-chain/missing-definition diagnostics are historical.
The164 exact source classifications were genuinely independently approved in the
PR12 closure and only scoped receipts installed; this is not a fresh full scan or
approval of subsequently changed source/client bytes. DEV-008 is being converted
to a typed role-responsibility plan under the [independent scope interpretation](../reports/dev-008-source-scope-review-2026-11-02.md),
which is not itself approval of that final plan. No future ERP/outbox/full AI
implementation or completed pilot is automatically added to literal S0 contract
criteria; actual enabling gates remain in their source tasks.
