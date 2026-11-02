# S0 current engineering decision candidate — 2026-11-02

**PROPOSED / NOT INDEPENDENTLY APPROVED.** DEV-004/005/006/011/116 retain
their literal active18.01 criteria. This reconciles historical implementation
inventories; it supplies no author approval, legal/Marine/source validation or
production/pilot signature. Independent review must cite the eventual exact
commit/tree and original source locators.

## Architecture and schema proposal

Keep the modular monolith, shared Tenant/Company/Vessel/Person/Asset roots and
owned versioned operational drafts. Domain policies remain inward-only. The API
currently orchestrates one shared persistence context; accept this as an explicit
foundation packaging decision, with later application-port extraction when a real
use case warrants it. Those ports and a general dispatcher are not implemented.

| Concern | Actual current evidence and proposed boundary |
|---|---|
| Core/IAM/Fleet | [Startup](../../backend/Poseidon.Api/Startup.cs), server-trusted membership/object scope, compound tenant keys/FKs. Caller claims do not grant roles. |
| Regulatory/evidence/review | [Control/evidence](../../backend/Poseidon.Persistence/ControlEvidenceStorage.cs), [human review](../../backend/Poseidon.Persistence/HumanReviewStorage.cs), [source qualification](../../backend/Poseidon.Persistence/RegulatoryQualificationStorage.cs) and mapped handlers now exist. Older blanket absence statements are historical; complete operational release and actual production authority remain separate. |
| Crew | Person/Position/Assignment, protected identity/profile/medical declaration, exact file/photo ownership, catalog governance, credential/source consumers are authored. Full clinical/work-rest/competence/contact/status semantics remain source-scoped later work. |
| Technical/Tug | Authorized equipment forest/current maintenance observations and calculations; Tug request revisions, draft plans, capacity bindings and current Crew/source consumers. Drafts do not grant fitness, permits or Master authority. |
| Offline | [Crew replay](../../backend/Poseidon.Persistence/OfflineApplication.cs), [Tug draft-window storage](../../backend/Poseidon.Persistence/TugOffline.cs), [Tug offline endpoints](../../backend/Poseidon.Api/TugOfflineEndpoints.cs) now exist. Enrolled encrypted exact-command/reauthorization/atomic-receipt/conflict/recovery paths do not implement all fields, binary or safety-release offline. |
| Storage/persistence/audit | Exact object version/hash/scan/owner scope; staged database/object recovery; audited SaveChanges, xmin, immutable revisions/receipts and compound FKs. HTTP audit differs from write audit, privacy compliance and a signed operational decision. |
| Events/integrations/AI | Synchronous transactions and durable receipts are actual. Transactional outbox is a later selected design, not an implemented dispatcher. Product RAG/provider/tool pipeline remains absent; scoped/cited/advisory/human authority rules apply before enablement. |

The [normalization plan](core-schema-plan.md) covers core entities, PK/FK, tenancy,
version/audit/object refs and migration strategy against14.01/14.02. Source screen
aliases are not196 tables. The inventory retains2316 active field rows and
UNCONFIRMED later mappings. S0 approval of the plan does not certify every later
field's legal meaning or every future module's production implementation.

The requested local preview applied current migrations/idempotent synthetic
fixtures and compiled API/DbMigrator/frontend. Full model/provider parity,
regression and security checks remain gate work. Old unapplied-migration or
passing-test statements must retain their original scope. Production recovery,
retention and privilege decisions stay explicit before enabling deployment.

## Threat, observability and ERP scope

The [threat register](../security/threat-model.md) retains all eight required
areas, abuse cases, controls, owners, verification and real P0 mitigation DEV IDs.
DEV-006 requires this model/backlog linkage, not completion of every later
S1/S4/S10 mitigation. A current exposed unresolved P0 blocks S0; future mitigations
remain attached to their enabling tasks and are not silently accepted risks.

The164 exact classifications in the [PR12 record](../phases/PR12-SEMANTIC-SOURCE-20261102.md)
have genuine prior independent approval and separately trusted scoped receipts.
They do not approve later changed bytes. Fresh current repo/built-client scans
remain required; detector/pin/coverage rules are unchanged. Old160-unresolved
counts are historical; source-classification approval is not product security.

DEV-011's [metric contract](../operations/observability-contract.md) defines
correlation/tenant/vessel/request context, closed API/error/latency/audit-health/
freshness/replay dimensions and unavailable device-backlog/integration semantics.
IDs do not become metric labels, and unavailable does not imply measured zero.
Full production exporters/dispatchers are separate source tasks.

DEV-116 retains the [ERP boundary](../../backend/Poseidon.Domain/ErpReferenceBoundary.cs),
[closed schema](../../contracts/integration/erp-reference.v1.schema.json) and
domain/EF guards. Finance/payroll/full procurement/PO/AP/AR/warehouse/chartering
remain OUT. Technical requisition/critical-spare references do not implement
runtime caller authorization, persistence or full ERP/provider delivery.

## Required evidence

DEV-003 preserves real environment binding/provisioning status. DEV-008 uses a
reviewed role-responsibility plan under the [independent scope interpretation](../reports/dev-008-source-scope-review-2026-11-02.md);
that interpretation is not approval of the final plan. Candidate versions,
OPEN legal review and blocked activation do not become authoritative current law
or actual pilot appointments.

Freeze after required S0 authoring; run source-definition, contract/unit/model/
security and required changed-branch checks, retaining failures. Obtain genuine
separate exact-target source/security/engineering review, then evaluate all
DEV-001–011 dependencies, DEV-116 and DEV-012 exit without missing/skipped checks
or current P0. An author hash/report is not approval. Canonical8/53S0 and8/970
overall remain unchanged until that evidence exists.
