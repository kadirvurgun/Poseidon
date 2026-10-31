# S0 ADR: Offline replay and advisory AI boundaries

Status: proposed engineering direction, awaiting review; offline sync and product AI are not implemented by this delivery.
Date: 2026-10-31. Traceability: S0-ADR-01 / DEV-004 / T-S0-004.
Owner role: Solution Architect. Approval identity/date/evidence: OPEN.

Source locators under `analiz/POSEIDON GUNCEL 30.10.2026/`, baseline from [active.json](../sources/active.json):

- `06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`, `Backlog` row 5: DEV-004 / T-S0-004, approval required.
- `05_TECHNICAL_ARCHITECTURE/14.01_POSEIDON_v46.3_Product_Wide_ERD_Data_Architecture_TR.docx`, §1 paragraphs 9–12 (version, distinct states, lineage); §3 paragraphs 65–72 (replayable integration and advisory Intelligence).
- `05_TECHNICAL_ARCHITECTURE/15.01_POSEIDON_v46.3_Product_Wide_API_Integration_Specification_TR.docx`, paragraphs 214–219 (sync/SSO); §8 paragraph 228 (command/receipt/conflict); §9 paragraphs 229–245 (idempotency, base version, stale snapshot, material conflict, clock/evidence provenance); §10 paragraphs 251–254 (enterprise IdP).
- `05_TECHNICAL_ARCHITECTURE/17.01_POSEIDON_AI_Governance_Data_Strategy_TR.docx`, §1 paragraphs 5–13 (human authority, grounding, privacy); §2 paragraphs 40–44 (extraction pending verification); §11 paragraphs 209–217 (missing data, conflicting sources, prompt injection, tenant isolation and fallback).
- `07_TEST_PILOT_RELEASE/23.03_POSEIDON_v46.3_Deployment_Environment_Runbook_TR.docx`, paragraphs 8–17 (deployment constraints).
- `08_MANAGEMENT_CONTROL/25.02_POSEIDON_v46.3_Eksik_Kapatma_ve_Release_Hazirlik_Raporu_TR.docx`, paragraphs 38–39: D-STATE-01 remains unresolved.
- [Architecture](../architecture.md), unchanged business rules; [security baseline](../security-baseline.md), identity, audit and privacy; [required-data decision](ADR-DRAFT-REQUIRED-DATA.md), POL-DEFAULT-01.

Selected offline direction (proposal): a durable client command queue with UUID command ID, tenant/device scope, last-known aggregate version and payload hash. Capture local time/offset, server receipt UTC and evidence hash/source. Client storage technology, encryption/key lifecycle, device revocation and supported client platforms remain unselected; no browser storage choice is implied.

The server must derive actor/tenant from validated identity and reauthorize every replay for tenant/vessel/job scope. Idempotency receipts must be tenant scoped: the same command with the same payload returns the recorded outcome; reuse with another payload is rejected. A duplicate response must not execute the mutation again, bypass current read authorization or assert that an earlier permit is currently active. Reject or route stale base versions for conflict review. Safety/legal material conflicts require explicit authorized actor/reason and preserved local/server/resolved values; no silent last-write-wins. Only proven non-material additive evidence may auto-merge under an approved policy.

Queued, synchronized, completed, verified and effective are distinct. Offline capture/replay does not invent active permit authority, a Master/Tug Master signature, regulatory activation or READY. Expired/stale readiness/rule snapshots cannot create READY. Server reconciliation must re-evaluate current rule/evidence versions and require the real authorized human decision. Missing/UNKNOWN data remains explicit; drafts may be saved, submission is blocked when required data is missing/unknown. UNKNOWN must not become No, Ready or NotApplicable. D-STATE-01 blocks final TugJob/WorkOrder transition decisions; this ADR supplies no replacement state dictionary.

Offline alternatives: online-only avoids replay complexity but cannot serve supported disconnected workflows; last-write-wins is simple but loses material conflicts; replicating client state as authority enables stale decisions. Select command replay with explicit conflict resolution. Consequences include receipt retention, queue visibility, stale-data indicators, conflict UI and recovery tests; offline UX must not imply server acceptance before reconciliation.

Selected AI direction (proposal): Detect → Explain → Recommend → Authorized Human Decision. AI cannot issue final safety/legal/compliance decisions, approve permits, override applicability, publish regulatory rules or bypass Master/Tug Master/verifier authority. A suggestion can be rejected without breaking the non-AI workflow. Extracted fields remain PENDING_VERIFICATION until authorized verification.

Retrieval must apply trusted tenant/role/vessel/job and sensitive-field authorization before search/vector retrieval and again before returning evidence; post-generation filtering alone is insufficient. Index/cache entries need tenant/access/version metadata and controlled retention/deletion. Answers must cite source file/section or evidence ID/version and relevant context; missing or conflicting authoritative sources produce explicit uncertainty/review requirements, never invented compliance. Treat retrieved documents as untrusted data: embedded instructions cannot grant tool access or widen authorization.

Provider/model and embedding store remain unselected. Real customer data is not sent to a provider or reused for training by default. Security/legal review must cover minimization, lawful purpose, transfers, location, retention, subprocessors and logs; prompts/results must not leak credentials, health data or protected-report identities. Record minimal attributed model-run/provenance metadata under policy. AI failure must leave ordinary authorized workflows usable. This ADR authorizes no model calls or provider setup.

AI alternatives: autonomous action conflicts with human authority; unrestricted retrieval risks tenant/privacy leakage; citation-free answers cannot support evidence lineage; no AI remains a valid fallback. Select advisory, authorized and cited assistance, with evaluations required before enabling each use case.

Implementation evidence: [RequiredDataPolicy](../../backend/Poseidon.Domain/RequiredDataPolicy.cs) distinguishes missing/unknown/known and blocks the required-data gate without granting readiness. [API](../../backend/Poseidon.Api/Program.cs) has diagnostic endpoints only. No command queue/receipt/conflict service, product retrieval pipeline or AI safety evaluation is implemented in this package. Local document FTS retrieval by [query_sources.py](../../scripts/query_sources.py) supports development source lookup; it is not a tenant-authorized product RAG service. Historical embedded runtimes and UAT archives are not current passing evidence.

Future acceptance tests: replay duplicates, payload mismatch, reconnect after role revocation, cross-tenant receipt access, stale base version, lost acknowledgements and clock skew; material conflict and stale/expired permit replay must never activate authority. AI tests must cover cross-tenant retrieval/cache denial, missing/conflicting citations, malicious document instructions, redaction, unavailable provider and attempted human-authority bypass. These tests remain implementation gates, not claims of passing runtime coverage here.

Open approvals (reviewer/date/evidence URI all OPEN): D-STATE-01 with domain owners; IdP and replay identity policy with security; client storage/device policy with engineering/operations; data location/provider/retention with legal/security; per-use-case AI review and real Marine/Regulatory/pilot sign-offs. No expert, legal or operational approval is fabricated.

Related: [module boundaries](s0-module-boundaries.md), [events/storage](s0-events-storage.md), [task evidence](../tasks/s0-adr-01.md).
