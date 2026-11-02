# S0 ADR: Module boundaries and dependency direction

## S0 closure candidate reconciliation — 2026-11-02, pending independent review

This is the current engineering proposal for DEV-004/005/006; earlier dated
implementation inventories below are historical. The requested morning S0 target
does not approve it. [Exact current candidate](../architecture/s0-current-engineering-candidate-20261102.md)
separates literal S0 contract approval from later operational deployment,
source/Marine/legal authority and unimplemented future modules. No author approval,
new accepted SP or passing suite is asserted here.


Status: proposed engineering direction, awaiting review; not approved or DEV-004 accepted.
Date: 2026-10-31. Traceability: S0-ADR-01 / DEV-004 / T-S0-004.
Owner role: Solution Architect. Approval identity/date/evidence: OPEN.

Sources use the candidate baseline `poseidon-v46.3-20261030` selected by
[active.json](../sources/active.json). A source's recorded status is not approval of this ADR.

Source locators (paths relative to `analiz/POSEIDON GUNCEL 30.10.2026/`):

- `06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`, sheet `Backlog`, row 5: DEV-004, dependency DEV-001, 5 SP; requires approved boundary/event/storage/offline/AI ADRs.
- `05_TECHNICAL_ARCHITECTURE/14.01_POSEIDON_v46.3_Product_Wide_ERD_Data_Architecture_TR.docx`, §1 paragraphs 5–12 and §3 paragraphs 16–72: lifecycle ownership, shared roots, domain groups and systems of record.
- `07_TEST_PILOT_RELEASE/23.03_POSEIDON_v46.3_Deployment_Environment_Runbook_TR.docx`, architecture row paragraph 8: modular-monolith-first.
- `08_MANAGEMENT_CONTROL/25.02_POSEIDON_v46.3_Eksik_Kapatma_ve_Release_Hazirlik_Raporu_TR.docx`, paragraphs 38–39: D-STATE-01 is blocking and unresolved for conflicting TugJob/WorkOrder states.
- [Architecture](../architecture.md), “Teknoloji ve sınırlar” and “Değiştirilmeyecek iş kuralları”; [security baseline](../security-baseline.md), “Her kullanıcının işlem izi”.

Selected direction (proposal): one modular monolith with explicit aggregate ownership, not a service/table per screen. The following ownership map is the intended boundary, not a list of implemented modules.

| Module | Owned concepts and boundary |
|---|---|
| Core/IAM | Tenant, Company, identity/role/delegation mappings; trusted authorization context |
| Fleet | Shared Vessel root, VesselProfile, TugProfile, registry/capability |
| Crew | Person, assignment, competence, work/rest; restricted medical projections |
| Technical | Asset, equipment, maintenance, WorkOrder, defect, inventory |
| Ship Operations | ShipOperation, voyage context, ship permits; no port traffic control |
| Tug Operations | TugJob, assignment, towage plan/briefing/execution/log |
| Safety | SafetyEvent, investigation, risk/barrier, stop-work, MOC |
| Knowledge | Controlled Document/DocumentVersion, copy and acknowledgement; object bytes through storage port |
| Assurance | Inspection, finding, survey, certificate and class evidence |
| Regulatory | Source, requirement, RuleVersion, applicability evaluation and controlled activation |
| Evidence/Decision | Evidence links, verification, signatures, human decisions and audit lineage |
| Integration/Offline | Connectors, external maps, delivery, command receipts/conflicts |
| Intelligence | Advisory signal/recommendation, model-run explanation and feedback |

Knowledge and Intelligence make explicit the groups in 14.01 §3 that the architecture summary does not enumerate separately. Final packaging/naming remains an architecture review item. Shared Vessel/Person/Asset/Evidence/Audit identities do not grant arbitrary cross-module writes: the owning module exposes contracts; consumers retain IDs or authorized projections.

Dependency direction is inward for compile-time references:

```text
Application -> Domain
Infrastructure (including EF persistence) -> Application ports + Domain
API -> Application contracts
API composition root -> Infrastructure registrations
Domain -> no Application, Infrastructure, API, EF Core or DbContext
```

Domain → application → infrastructure/API describes the conceptual layers from inside to outside, not permitted outward references. At runtime the API invokes application use cases, which invoke domain behavior and ports; infrastructure implements those ports. No domain aggregate accepts a DbContext, persistence entity, HTTP request or another module's repository/context. Application orchestration crosses modules through public use-case contracts and versioned events, not another module's tables. Read models may compose authorized projections without obtaining write ownership. A minimal shared kernel needs explicit review; it must not become a global business model.

Alternatives considered: a single unrestricted model/context is initially easy but obscures lifecycle and tenant ownership; microservices add distributed transaction and operating costs before proven scale/team need; screen-based services duplicate entities. Select modular-monolith-first and reconsider extraction only with measured needs and a migration ADR.

Consequences: ownership and contract tests are needed before module expansion. Domain tests must reject outward dependencies; integration tests must cover tenant/vessel/job policy, cross-module contract behavior and transaction boundaries. D-STATE-01 must be resolved by authorized reviewers before canonical TugJob/WorkOrder enums/transitions are implemented. No guessed reconciliation is selected here.

Implementation evidence: [Domain project](../../backend/Poseidon.Domain/Poseidon.Domain.csproj) has no project/package references; [RequiredDataPolicy](../../backend/Poseidon.Domain/RequiredDataPolicy.cs) is an isolated draft-data rule. [PoseidonDbContext](../../backend/Poseidon.Persistence/PoseidonDbContext.cs) currently owns foundation entities in `core` plus `audit`; this is not implementation of every module or an application layer. [API Program](../../backend/Poseidon.Api/Program.cs) exposes diagnostics only and registers no database/business endpoints. Module schemas, application ports and inter-module contract enforcement remain future work. This ADR changes no runtime behavior.

Open approvals: Solution Architect boundary/dependency approval OPEN; D-STATE-01 domain owners/Marine SME decision OPEN; IdP/security mapping OPEN; data location/legal and operational review OPEN. Reviewer, date and evidence URI remain unfilled for each. Human safety decisions and pilot sign-offs cannot be supplied by this document.

Related: [events/storage](s0-events-storage.md), [offline/AI](s0-offline-ai.md), [task evidence](../tasks/s0-adr-01.md).


## Current implementation reconciliation — 2026-11-02

Current monolith implements real authenticated API/React families, tenant/vessel-scoped Crew identity/profile/files/photo/import/offline, Technical observations/maintenance, Tug draft planning/history/board and role-context/navigation/search. Earlier diagnostics-only/current-core-only text is historical. `backend/Poseidon.Api/Startup.cs`, module endpoint files and the [normalized plan](../architecture/core-schema-plan.md) identify actual owners. Domain remains inward-only, while API currently orchestrates one shared persistence context across use cases; the separate application-port packaging in the proposed dependency diagram is not fully implemented. This concrete deviation must be decided by independent engineering review, not hidden as already conforming architecture. Operational WorkOrder/defect/safety/towage/verification/decision/AI modules remain future scope. D-STATE-01 remains unresolved for operational state transitions; versioned incomplete draft plans do not settle it.

Proposed engineering decision remains modular-monolith-first with owned aggregates, versioned immutable lineage, S3-compatible bytes, transactional outbox for future durable asynchronous delivery, scoped offline replay and advisory AI. Independent engineering reviewer/decision/date/exact-target evidence remain PENDING; author cannot approve this ADR. No real pilot is required merely to review a technical architecture unless an actual source criterion says so. Marine/legal/source/pilot operational decisions remain separate. All current verification is DEFERRED_NOT_RUN. See [continuation report](../phases/S0-CONTRACT-CONTINUATION-2026-11-02.md).

## Foundation remainder contract reconciliation — 2026-11-02

[Concrete current-code/contract decision matrix](../architecture/s0-remainder-reconciliation.md) supplements this proposal: golden consumers use actual pure policies; trace logical entities remain distinct from physical EF aggregates; task definitions/evidence are read-only QA inputs; no outbox, source authority or runtime ERP authorization is implied. Current shared-context/application-port deviation, limited Crew offline scope and unavailable production adapters remain explicit. Independent exact-target Engineering approval is PENDING; no approval, tests or operational sign-off is supplied by authoring these definitions.
