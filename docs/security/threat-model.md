# POSEIDON threat model — S0 engineering draft

Date: 2026-11-01. Traceability: S0-THREATS-01 / DEV-006 / T-S0-006.
Responsible role: Security/Architect. Authorized security reviewer, review date and approval evidence: **OPEN**.

This is draft engineering evidence for review, not DEV-006 acceptance, production readiness, GDPR/KVKK compliance or expert approval. No safety sign-off, permit activation or canonical accepted SP is supplied. Controls described as required are unimplemented unless the limited current evidence below says otherwise. Role assignments identify responsibility, not completed review by a named person.

## Sources and evidence limits

The [active pointer](../sources/active.json) selects candidate/unverified baseline `poseidon-v46.3-20261030`. Source root is `analiz/POSEIDON GUNCEL 30.10.2026/`.

- Backlog source **B**: `06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`, sheet `Backlog`, row 7: DEV-006 / T-S0-006, P0, Security/Architect, dependent on DEV-004. Rows in the mitigation table below refer to this exact workbook/sheet. Its SHA-256 is `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
- [Security baseline](../security-baseline.md): identity/password, user-action audit, privacy and mandatory negative tests; [architecture](../architecture.md): tenant/vessel/job enforcement and human decision authority.
- S0 ADRs present in this checkout: [module boundaries](../decisions/s0-module-boundaries.md), [events/storage](../decisions/s0-events-storage.md), [offline/AI](../decisions/s0-offline-ai.md). Their text still says proposed/approval OPEN; inclusion in the checkout does not approve their engineering directions.
- `05_TECHNICAL_ARCHITECTURE/15.01_POSEIDON_v46.3_Product_Wide_API_Integration_Specification_TR.docx`, paragraph 291: protected identity restricted to designated reviewers.
- `05_TECHNICAL_ARCHITECTURE/17.01_POSEIDON_AI_Governance_Data_Strategy_TR.docx`, paragraphs 140–151 and 209–217: untrusted documents, scoped tools/retrieval, human authority, leakage and fallback tests.
- `07_TEST_PILOT_RELEASE/19.01_POSEIDON_v46.3_Test_Pilot_UAT_Release_Gates_TR.docx`, paragraph 136: protected identity excluded from generic analytics/AI.

Current evidence is scoped source inspection, not a fresh runtime security certification. The baseline and ADR implementation paragraphs predate some foundation code: [identity configuration](../../backend/Poseidon.Api/FoundationIdentity.cs), [startup](../../backend/Poseidon.Api/Startup.cs) and [directory endpoints](../../backend/Poseidon.Api/FoundationEndpoints.cs) now contain configured OIDC/session and directory reads. They do not implement every operational module. Historical 26.01 runtime/UAT is not current implementation or passing-test evidence.

## Assets, actors and trust boundaries

Assets are tenant/company/vessel/job records; crew/health and protected-report identity; evidence bytes, hashes and provenance; human decisions and rule versions; credentials/session keys; audit history; offline queues and retrieval indexes/caches.

Potential attackers include unauthenticated callers, a legitimate user outside an assigned tenant/role/vessel/job, a compromised device or service credential, a malicious document author and a privileged operator abusing an import path. A tenant administrator is not automatically a protected-report reviewer or a safety decision maker.

Boundaries are browser/device → API; external IdP → validated server identity and operator-provisioned membership; API → application/domain → EF/database; separate migrator/bootstrap → privileged database access; metadata → object store/scanner; client queue → authoritative replay; and authorized retrieval → model/provider/tools. Module ownership does not itself enforce authorization. Production hosting/site/provider, IdP choice, data location and retention policies remain open; the ADR's on-premises/self-hosted direction is not a deployed environment.

## Threat register

P0 means mitigation required before exposing the affected capability or pilot scope. These are engineering priorities, not new backlog rows or a claim that every source task has been accepted. Verification below is a required future case unless explicitly reported as executed in [task evidence](../tasks/s0-threats-01.md).

### TM-01 — Tenant isolation (P0)

- **Assets / boundary:** tenant/company/vessel/job records and projections; caller identity → server scope → EF/database and cache/search/object stores.
- **Abuse:** tenant A substitutes tenant B's company ID or supplies an actor/tenant header; a same-tenant user reads an unassigned vessel; a query, relationship or cached result drops its tenant predicate.
- **Current controls:** server membership supplies actor/tenant; directory policies and tenant-scoped EF filters, composite relationship keys and SaveChanges write guards exist in [DbContext](../../backend/Poseidon.Persistence/PoseidonDbContext.cs). Directory detail reads use 404 for an invisible ID.
- **Gaps / mitigation:** unimplemented operational vessel/job/object policies and product cache/search boundaries. Derive scope only from validated server identity; enforce every query, relationship and write, including indirect links and exports. Query filters alone are insufficient. Recheck permissions when data is returned.
- **Verification:** A/B tenant list/detail/write and linked-FK denial; spoofed header/body actor/tenant ignored or rejected; revoked membership denied; same-tenant wrong vessel/job denied; cache/index/object leakage negative cases. Existing [foundation API tests](../../tests/Poseidon.Api.Tests/FoundationApiTests.cs) cover only the foundation surface.
- **Owner:** Backend; QA independently verifies. Backlog: DEV-014, DEV-015, DEV-019.

### TM-02 — Authentication and authorization (P0)

- **Assets / boundary:** sessions, trusted actor permissions, critical state changes; IdP/browser → API policies → human decision handlers.
- **Abuse:** forged/expired token or duplicate membership escalates permissions; CSRF changes state; hidden UI is bypassed; platform admin impersonates a verifier or Master.
- **Current controls:** configured OIDC code/PKCE validates issuer/audience/lifetime/signature; secure HttpOnly session cookies, local return validation, server membership resolution, directory permission policies and logout antiforgery exist. A versioned Identity password-hash component is foundation evidence, not local account/reset/MFA implementation.
- **Gaps / mitigation:** operational role/vessel/job/object policies, separation of duties, deployment IdP/MFA/reset/rate-limit policy and session revocation assurance remain open. Validate identity server-side, deny by default and authorize each action; no custom password cryptography. Human safety decisions require the actual authorized actor and evidence/version/reason, never AI or admin substitution.
- **Verification:** invalid issuer/audience/signature/expiry, unmapped/ambiguous membership, unsafe redirect, CSRF, role escalation and post-revocation requests; unauthorized write leaves no mutation. [Protocol tests](../../tests/Poseidon.Api.Tests/OidcProtocolTests.cs) are test definitions, not evidence of production federation.
- **Owner:** Backend with Security/IdP operator. Backlog: DEV-013, DEV-015, DEV-019.

### TM-03 — File upload and evidence access (P0)

- **Assets / boundary:** object bytes, evidence metadata/checksum/provenance; untrusted file → staging/scanner → metadata and authorized download.
- **Abuse:** oversized or disguised executable exhausts storage; scan failure is treated as clean; an attacker swaps bytes after checksum validation or reuses another tenant's signed URL.
- **Current controls:** events/storage ADR proposes metadata/object separation and reconciliation; no secure upload/scanner/object adapter is evidenced by this package.
- **Gaps / mitigation:** pipeline unimplemented. Enforce server-side type/size/checksum and tenant-owned object identifiers; quarantine pending/rejected/unavailable scans, authorize upload/finalization/download, scope temporary signed access and reconcile abandoned uploads. Rejected files cannot mutate critical state. Stored bytes alone never imply verified evidence. Limits and URL expiry policy need review; no values are invented here.
- **Verification:** disguised/oversized/malicious input, checksum mismatch, scan timeout, interrupted retry, object substitution and cross-tenant/expired signed access; assert no state activation or unauthorized bytes delivered.
- **Owner:** Backend with DevOps storage/scanner operator; QA. Backlog: DEV-018, DEV-029, DEV-019.

### TM-04 — Protected reporting and sensitive personal data (P0)

- **Assets / boundary:** reporter identity/narrative, crew/health data; restricted reviewer projection → general lists, notifications, exports, analytics and AI.
- **Abuse:** a manager infers a reporter through filenames, narrative, logs or counts; a broad export/search or generated summary reveals protected fields despite a hidden UI field.
- **Current controls:** baseline and source restrictions define required visibility; no protected-report workflow/projection is implemented here.
- **Gaps / mitigation:** designated-reviewer policy, separate restricted projection and purpose/scope masking unimplemented. Minimize fields and deny identity to generic export/search/analytics/AI; test narrative as well as structured identity. Control download and access-event visibility. Approved inventory, retention/legal hold/deletion and backup/index/cache handling remain required; no fixed duration or legal compliance is asserted.
- **Verification:** reviewer/non-reviewer and cross-tenant matrix across detail/export/search/notification/AI; narrative identity redaction, denied object access, minimal access events and controlled deletion/hold tests when policies exist.
- **Owner:** Security/privacy owner with Backend; QA/Security for regression. Backlog: DEV-015, DEV-019, DEV-051 (crew privacy regression is related coverage, not proof of a complete protected-report workflow).

### TM-05 — Secrets and log disclosure (P0)

- **Assets / boundary:** DB/OIDC/object credentials, cookie/token/password/hash, sensitive PII; operator secret configuration → API/client bundle/logs/providers.
- **Abuse:** callback URL/query string, exception, request body or prompt exports credentials/medical data; a committed bootstrap password or overprivileged DB credential exposes all tenants.
- **Current controls:** API accepts server configuration and suppresses selected framework request/auth/EF diagnostic categories. [SecurityAudit](../../backend/Poseidon.Api/SecurityAudit.cs) emits a fixed failure message plus server-generated correlation ID, not raw database errors. This is narrow redaction coverage, not a proven secret-store deployment or complete scan.
- **Gaps / mitigation:** production secret store/reference, rotation and least-privilege operations unverified. Never log Authorization/cookies/tokens/passwords/hashes/connection strings, health details, full request/response bodies, search text or URL query strings. Allowlist event metadata; sanitize errors and prompt/model-run records. No real credentials in seed/client bundles. Secret scans and sink access/retention/export controls remain required.
- **Verification:** synthetic canary secrets/PII through failure, callback, query, body, upload filename and AI paths must be absent from all sinks; repository/client artifact scans, rotation/revocation drill and app-role privilege denial.
- **Owner:** DevOps with Security; Backend owns emission. Backlog: DEV-017, DEV-016, DEV-068.

### TM-06 — Audit integrity and bootstrap/admin boundaries (P0)

- **Assets / boundary:** attribution, business history and access/security events; HTTP → server actor → business transaction; separate security writer and privileged migrator → database.
- **Abuse:** a caller forges actor attribution or enables administrative import; bulk SQL bypasses audit; rollback removes a denial event; a privileged DB role tampers with history; unaudited downloads hide exfiltration.
- **Current controls:** EF SaveChanges generates minimal entity/action/actor/tenant/correlation/UTC metadata atomically with business writes and rejects direct audit edits. Migrations block UPDATE/DELETE/TRUNCATE on audit tables. Separate SecurityEventWriter records selected login/logout/session/directory-read/denial events independently of a business rollback. Required read audit failure prevents directory data delivery; best-effort denial/failure logging can lose durable events during sink failure.
- **Gaps / mitigation:** EF write audit is not HTTP read/security auditing. Selected HTTP events are not full product coverage; role/vessel/reason and approved redacted before/after fields required by DEV-016 are incomplete. Add explicit read/search/download/export/approval/security coverage, alert/recovery on sink failures and independent append-only/tamper monitoring. Do not log raw snapshots. Separate application/migrator DB roles; table owner/superuser is not an application role. DB-admin-proof immutability is not claimed.
- **Bootstrap boundary:** [migrator](../../backend/Poseidon.DbMigrator/Program.cs) uses explicit system seed actor and administrative context; synthetic seed is Development/Test only. Never bind `administrativeImport`, `IgnoreQueryFilters`, actor construction, membership provisioning or seed paths to untrusted request data. Business mutations must use audited SaveChanges; ExecuteUpdate/ExecuteDelete/raw SQL require a controlled migration/import design with equivalent audit and tenant enforcement. SecurityEventWriter is a constrained append-only infrastructure sink, not a general business-write bypass.
- **Verification:** write/audit commit and rollback atomicity; direct audit injection/update/delete/truncate denial; actor spoofing/admin-seed denial; security event survives business rollback; required read fails closed on sink failure and best-effort loss alerts; app-role cannot migrate or change audit triggers. [Write guards](../../tests/Poseidon.Persistence.Tests/WriteGuardTests.cs) and [security-event tests](../../tests/Poseidon.Persistence.Tests/SecurityEventTests.cs) define limited existing cases.
- **Owner:** Backend with DevOps DB privileges; Security reviews coverage. Backlog: DEV-014, DEV-016, DEV-017, DEV-019.

### TM-07 — Offline devices and replay (P0 before offline enablement)

- **Assets / boundary:** local evidence/queue, device credentials, receipts and current permit/decision state; lost device or stale queue → authoritative server.
- **Abuse:** stolen device exposes crew data; a revoked user reconnects; duplicate or payload-altered command creates a critical state twice; stale permit snapshot appears active after replay.
- **Current controls:** offline/AI ADR specifies command replay and explicit conflict handling; no product queue/receipt/device controls implemented here.
- **Gaps / mitigation:** encrypt/minimize durable local data with approved key/device revocation and retention policy; reauthorize every replay against current server tenant/vessel/job rights. Tenant-scoped command ID/payload hash/base version and receipts prevent duplicate effects. Preserve material conflicts for authorized resolution; no silent last-write-wins. Queued/synced/completed/verified/effective remain distinct; reconnect cannot fabricate a human signature or active permit. Client technology and D-STATE-01 remain open.
- **Verification:** lost device/revocation, restart/network loss/lost acknowledgement, duplicate/payload mismatch, cross-tenant receipt, stale version/clock skew and attachment retry; material conflict/stale permit must not activate authority.
- **Owner:** Frontend (device queue), Backend (replay), Full Stack (conflicts); QA. Backlog: DEV-033, DEV-034, DEV-035, DEV-038; identity/scope dependencies DEV-013–DEV-015.

### TM-08 — AI prompt/data threats (P0 before AI enablement)

- **Assets / boundary:** authorized evidence, citations, index/cache metadata, prompts/results and human decision authority; retrieved source data → provider/tools/output.
- **Abuse:** a PDF says “export all crew data”; retrieval/cache returns tenant A's procedure in B's session; a summary reveals reporter identity; fabricated citations or missing inputs create a false READY or bypass a Master's rejection.
- **Current controls:** ADR/source guardrails only; product RAG, model/provider/tool pipeline and evaluations unimplemented. Development `query_sources.py` is local source lookup, not tenant-authorized product retrieval.
- **Gaps / mitigation:** source documents are untrusted data, never executable instructions or authority to change tool policy. Enforce trusted tenant/role/vessel/job/sensitive-field scope before retrieval and again before evidence return; bind indexes/caches to tenant/access/version and approved deletion policy. Allowlist tools/object scope; document links cannot trigger arbitrary fetch or widen access. Cite source/evidence ID/version, surface missing/conflicting data, redact protected identities and keep customer data out of external AI/training by default pending authorized review. AI has no authority over safety, permits, regulatory activation or expert decisions. Extracted fields remain pending verification; UNKNOWN remains explicit, drafts may save and missing required data blocks submission. Preserve usable non-AI workflows.
- **Verification:** malicious document/tool instruction, tenant/cache crossover, role revocation, narrative redaction, missing/conflicting citations, provider outage, Master rejection and attempted automatic permit/sign-off. Use synthetic fixtures; no model call or customer-data transfer is authorized by this draft.
- **Owner:** AI engineering with Backend and Security/privacy; Marine/Regulatory authority stays human. Backlog mapping is the enabling security work DEV-006, DEV-014, DEV-015, DEV-019 and review gate DEV-068, not a claim those rows deliver a complete AI pipeline. Dedicated AI evaluation/tool-policy implementation assignment remains OPEN for coordinator triage.

## P0 mitigation backlog and residual gaps

Every DEV ID below was located with bounded `scripts/query_sources.py` queries and checked at its workbook row. These are existing backlog associations; proposed extensions are explicit. All source rows below are P0. Partial code does not establish task acceptance. Close each mitigation only with target-commit test evidence and independent authorized review.

| Threat / mitigation | Existing backlog IDs (B row; task) | Current state / remaining implementation | Responsible role / verification |
|---|---|---|---|
| TM-01 trusted tenant scope | DEV-014 (15; T-S1-002), DEV-015 (16; T-S1-003), DEV-019 (20; T-S1-007) | Foundation filters/guards/directory reads partial; operational vessel/job/object policies unimplemented | Backend + QA; tenant/role/scope negative matrix |
| TM-02 validated identity and action policy | DEV-013 (14; T-S1-001), DEV-015 (16; T-S1-003), DEV-019 (20; T-S1-007) | Foundation federation/session partial; production IdP/revocation/MFA assurance and operational policies open | Backend + Security; invalid identity, CSRF, escalation/revocation cases |
| TM-03 secure staged files and provenance | DEV-018 (19; T-S1-006), DEV-029 (30; T-S3-004) | Upload/scanner/signed access/evidence graph unimplemented | Backend; reject/tamper/timeout/tenant download cases |
| TM-04 restricted identity/projections | DEV-015 (16; T-S1-003), DEV-019 (20; T-S1-007), DEV-051 (52; T-S7-005) | Protected reporting unimplemented; DEV-051 is crew privacy testing, broader protected narrative/export coverage needs explicit review | Backend + QA/Security; purpose/scope export/search/AI denial |
| TM-05 secrets/redaction | DEV-017 (18; T-S1-005), DEV-016 (17; T-S1-004) | Fixed security diagnostic metadata partial; secret store/rotation/scans and complete redaction coverage unverified | DevOps + Backend; synthetic canary and repository/client scans |
| TM-06 attributed write and access audit; admin separation | DEV-016 (17; T-S1-004), DEV-014 (15; T-S1-002), DEV-017 (18; T-S1-005) | EF write and selected HTTP security events partial; full HTTP coverage/role/reason/approved before-after, resilient sink and production DB-role separation open | Backend + DevOps; atomicity/immutability/sink failure/admin denial; HTTP additions need coverage review under baseline |
| TM-07 secure queue/replay/conflicts | DEV-033 (34; T-S4-001), DEV-034 (35; T-S4-002), DEV-035 (36; T-S4-003), DEV-038 (39; T-S4-006) | Queue/encryption/device lifecycle/replay/conflicts/chaos pack unimplemented; device revocation policy assignment open | Frontend + Backend + Full Stack + QA; loss/retry/revocation/material conflict cases |
| TM-08 advisory, tenant-bound AI | DEV-006 (7; T-S0-006), DEV-014 (15; T-S1-002), DEV-015 (16; T-S1-003), DEV-019 (20; T-S1-007) | Threat specification and security prerequisites only; product retrieval/tool policy/evaluations unimplemented, dedicated implementation assignment OPEN | Security/Architect + AI engineering; injection/leakage/authority/fallback cases |
| All: pre-pilot independent security gate | DEV-068 (69; T-S10-005) | Unfulfilled gate: scoped tenant/RBAC/upload/escalation/secret checks and zero unresolved critical/high findings required by source | Security/QA; reviewed target-commit evidence, never self-certified by this document |

Review must reconcile scope extensions (protected-report narrative, full HTTP events, device lifecycle and AI evaluations) with these real backlog owners; no new DEV IDs or accepted work are invented. Unimplemented controls remain exposure blockers, not residual risks accepted by this draft.

## Review and closure

Open: authorized security review and threat prioritization; architecture approval/D-STATE-01; IdP/membership lifecycle; hosting/site/data location; secret/key/DB privilege operations; class-specific retention/legal hold/deletion and incident/restore evidence; protected reviewer policy; device policy; per-use-case AI/provider review; actual expert/pilot approvals. Legal applicability, DPA/subprocessors/transfers and any GDPR/KVKK assessment belong to authorized reviewers. No retention duration or compliance approval is set here.

Current phase: S0 documentation preparation. Next step: coordinator arranges independent review, assigns unresolved mitigation details and collects implementation verification before enabling each surface. Publication and canonical status/acceptance updates remain coordinator-only.
