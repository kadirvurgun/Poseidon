# S0 ADR: Events, transactional outbox and storage

Status: proposed engineering direction, awaiting review; transactional outbox and object storage are not implemented by this delivery.
Date: 2026-10-31. Traceability: S0-ADR-01 / DEV-004 / T-S0-004.
Owner role: Solution Architect. Approval identity/date/evidence: OPEN.

Source locators under `analiz/POSEIDON GUNCEL 30.10.2026/`, baseline from [active.json](../sources/active.json):

- `06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`, `Backlog` row 5, DEV-004 / T-S0-004.
- `05_TECHNICAL_ARCHITECTURE/14.01_POSEIDON_v46.3_Product_Wide_ERD_Data_Architecture_TR.docx`, §1 paragraphs 7–12 (tenant/version/immutability/lineage), §3 paragraphs 49–52 (controlled documents/object storage), 61–68 (evidence/integration ownership).
- `05_TECHNICAL_ARCHITECTURE/15.01_POSEIDON_v46.3_Product_Wide_API_Integration_Specification_TR.docx`, §6 paragraph 140 onward (event catalog); paragraphs 202–204 (reliable cross-domain propagation); §8 paragraphs 223–227 (event/outbox/import contracts); §10 paragraphs 271–274 (S3-compatible bytes; metadata/audit stays in POSEIDON).
- `07_TEST_PILOT_RELEASE/23.03_POSEIDON_v46.3_Deployment_Environment_Runbook_TR.docx`, paragraphs 8–17 (technology target, deployment, artifact promotion, secrets and release gates).
- [Architecture](../architecture.md), “Teknoloji ve sınırlar”; [security baseline](../security-baseline.md), audit and privacy sections; [seed policy](../seed-data.md).

Selected direction: EF Core with versioned migrations and module-owned PostgreSQL schemas. Domain ownership follows [module boundaries](s0-module-boundaries.md). Relational rows store tenant-scoped metadata, versions, hashes, provenance, verification/decision links and access policy; S3-compatible object storage holds document/evidence binaries. Bytes do not confer verified status. Published evidence/decisions are superseded or revoked by new records rather than edited in place. Retention/legal hold and deletion require approved policies, not a blanket promise of perpetual storage.

Transactional outbox is PROPOSED until an outbox entity/migration, atomic write path, dispatcher and failure tests exist. Proposed contract: the business mutation, attributed audit and versioned outbox record commit in one database transaction through audited SaveChanges. An outbox envelope carries event ID/type/schema version, tenant, aggregate ID/version, occurrence time, correlation/source, publication state and retry metadata. Events reference controlled evidence rather than embedding secrets or unrestricted personal data.

The future dispatcher delivers at least once after commit, with bounded retry/backoff and observable failed delivery. Consumers deduplicate by tenant/event ID, enforce schema/version handling and aggregate ordering where required, and authorize projections. Duplicate delivery is expected; exactly-once external side effects are not promised. A broker is optional infrastructure, not the transaction authority. Audit records and delivery state serve different purposes: retry must not mutate immutable audit history. No event can substitute for an authorized safety decision.

Business writes must not bypass the audited path via ExecuteUpdate/ExecuteDelete or raw SQL. A controlled migration/import needs equivalent audit and tenant enforcement. Administrative import context and actor construction must stay outside untrusted inputs. EF write auditing is partial coverage, not HTTP/read/download auditing or GDPR/KVKK acceptance.

Database and object storage do not share an atomic transaction. Proposed upload flow stages a tenant-isolated object, validates size/type/hash and malware policy, then commits its metadata/link; cleanup reconciles abandoned uploads. Downloads require server authorization; any temporary signed URL must be scoped and short lived. Backup/restore must reconcile database references, object versions/hashes and audit continuity. These flows and production encryption/keys remain implementation and operations gates.

Alternatives: synchronous cross-module calls are useful for immediate validation but do not provide durable asynchronous delivery; commit then publish can lose events; publish then commit can emit rolled-back events. Select outbox for reliable propagation. Database blobs simplify one-store transactions but complicate binary growth/backup; filesystem storage couples deployment and access control; select S3-compatible storage behind a port, with provider/site unselected.

Consequences: eventual consistency must be visible in projections; delivery backlog, retries and reconciliation require monitoring and runbooks. Module schemas require ownership/migration discipline. Future tests must inject failure before/after commit and after delivery-before-ack, prove rollback/deduplication/tenant isolation, exercise denied object access, tampered hashes, cleanup and restore. These are implementation acceptance requirements, not passing tests claimed here.

Implementation evidence: [DbContext](../../backend/Poseidon.Persistence/PoseidonDbContext.cs) has tenant filters, tenant-aware keys/write guards and generated SaveChanges audit; [initial migration](../../backend/Poseidon.Persistence/Migrations/20261030102326_InitialFoundation.cs) establishes foundation persistence. [SeedImporter](../../backend/Poseidon.Persistence/Seeding/SeedImporter.cs) and [synthetic fixture](../../backend/seed/synthetic-demo.json) provide foundation seed infrastructure. No outbox table/dispatcher or object storage adapter is present in the inspected backend. The existing context is not proof of all proposed schemas, security controls or deployment readiness.

Deployment reconciliation: current 23.03 paragraphs 10 and 17 specify on-premises/self-hosted browser access; cloud is for development/test. Earlier managed-cloud-primary wording is superseded. Select that product direction without selecting a host, country/site, object store region or provider. Versions in the source's historical embedded runtime are not current repository/test evidence; use the [dependency policy](../dependency-policy.md) and tested lockfiles. Artifact promotion, controlled migrations, smoke and rollback remain required release gates; this task deploys nothing.

Open approvals (reviewer/date/evidence URI all OPEN): architecture/outbox/schema design; operations storage provider/site and restore evidence; security key/access/retention design; legal data location/transfers/hold; IdP; D-STATE-01; Marine/Regulatory expert and pilot decisions. No production or legal compliance approval is inferred.

Related: [offline/AI](s0-offline-ai.md), [task evidence](../tasks/s0-adr-01.md).
