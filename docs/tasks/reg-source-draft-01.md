# REG-SOURCE-DRAFT-01 — tenant-local source and requirement preparation

Status: AUTHORED / UNVERIFIED; NOT ACCEPTED. Baseline `5784b82`; prior Today checkpoint `58b609d` is preserved. Direct user instruction defers every backend, UI, Playwright and Python suite, build, lint, typecheck, actual OpenAPI/schema export and independent QA to CORE-BUNDLE-01. No publishing, accounts, extra agents or acceptance changes are authorized in this task.

## Controlled sources and decision bounds

Active baseline: [active.json](../sources/active.json), `poseidon-v46.3-20261030`, rooted at `analiz/POSEIDON GUNCEL 30.10.2026`. Bounded retrieval used explicit Python313 `scripts/query_sources.py "DEV-026" --limit 4`, `"source_id" --limit 4`, `"Requirement Master" --limit 4`, followed by bounded locator reads from the local SQLite source index. No whole chunk corpus was loaded into model context. Code-memory MCP tools were unavailable; local Graphify query located the profile, command, scope and shell implementations.

| Current document | Exact locator and meaning |
|---|---|
| 18.01 Development Backlog | `sheet:Backlog/row:27`, DEV-026 / T-S3-001, Requirement/source/version; current source must not silently overwrite. DEV-005/DEV-008 dependencies remain OPEN. SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674` |
| 14.01 Product Wide ERD Data Architecture | `paragraph:9` effective dating; `paragraph:119–124` RegulatorySource/source_id/authority/title/locator/version/lifecycle/effective_dates and RuleVersion; `paragraph:136` lineage; `paragraph:138–143` UUID keys, tenant/global ambiguity, xmin, audit, privacy. SHA256 `c3f5e612d69e33d2e5e3a1227644cb9a0ec8937bdcf74b20d2b5a60b968344ab` |
| 14.02 Product Wide Data Dictionary | `sheet:Data Dictionary 2316/row:15–24` requirement-master fields and FR/TC IDs; types, lengths and approved reference lookups explicitly unresolved. `sheet:Read Model - Projection Gap/row:31` regulatory-source-governance has no controlled input fields and requires schema/scope/freshness/forbidden/failure projection contract; `sheet:Screen Coverage 196/row:36`. SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018` |
| 13.02 Screen Field Action Specification | `paragraph:1354–1357` requirement-master (10 fields/1 action); `paragraph:1374–1377` regulatory-source-governance (0 input fields/1 action). These screen projections do not approve a global source catalog. |
| 15.01 API Architecture Integration Contract | `paragraph:45–47` controlled source ingestion; `paragraph:61–81` API version/JSON/UTC/OIDC/server scope/idempotency/concurrency/pagination/errors/language/audit; `paragraph:531–534` requirement-master projection. |
| 13.04 Design System UX Standards | `paragraph:19–25` role/context/human authority/no silent loss; `paragraph:30–40` contextual responsive navigation and complex editing; `paragraph:94–95` persistent conflict/failed states. |

The 14.01 optional global `tenant_id=NULL` statement is **not** adopted here. DEC-REG-DRAFT-01: all register rows and revisions have a non-null tenant, derived from a trusted membership. This is a tenant-local declaration work area, separate from admission/canonical source-index authority. Local analysis source-index locators are used for traceability; user-entered references do not enroll documents into that index. No URL or filesystem fetch occurs. DEV-005/008, canonical DEV-026 lifecycle, approved role/lookup catalogs and documents49/50 expert/permit proposals remain unresolved.

## Implemented field contract (structural limits, not approved business policy)

All declared fields use explicit Missing/Unknown/Known with null values for Missing/Unknown. Known requires nonblank bounded text without control characters. Values are not silently converted to No/NotApplicable. No measurement fields are introduced; existing profile exact-decimal behavior is preserved.

| Preparation field | Bound / mapping |
|---|---|
| Source title, authority | 200 characters each; 14.01 paragraph120. Declared authority is text, never a grant. |
| Source locator | 1000 characters; 14.01 paragraph120. Untrusted text only, rendered as text. |
| Requirement instrument | 200 characters; FR/TC-`requirement-master-instrument` (row16). |
| Amendment/version declaration | 200 characters; draft-only declaration related to FR/TC-`requirement-master-version` (row17), **not** an approved version lookup. |
| Declared effective date | ISO date; FR/TC-`requirement-master-effective_date` (row19); does not activate anything. |
| Declaration/provenance text | 1000 characters; attributed declaration context, never a signature. |
| ObservedAt | Offset ISO timestamp, max40; independently declared Missing/Unknown/Known. SavedAt is system operation time and confers no freshness. |
| Requirement ID / source ID | Server-generated UUID; FR/TC-`requirement-master-requirement_id` (row15), ERD paragraph138. No client business-ID, tenant, role or actor construction. |
| Exact source citation | Nullable paired SourceId/SourceRevision; both absent is an incomplete draft; both present must resolve to an immutable tenant-correct Source revision. Never resolves arbitrary text to a source or upgrades an unknown reference. |
| Lifecycle | Response constant DRAFT, not request-bound. Only draft preparation under FR/TC-`requirement-master-legal_state` (row18). |

Requirement title/authority/locator must be Missing: these come from the explicitly selected source revision. Source instrument must be Missing. Applicability_rule/responsible_role/control_id/evidence_type/verification_method (rows20–24) are not invented editable lookups; their controlled implementation remains open. No applicability evaluation, adoption, publication, InForce/Verified/Effective state, permit or expert signature endpoint exists.

## Engineering criteria and authored evidence

| Local criterion | Authored implementation/tests (all NOT RUN) |
|---|---|
| REG-SRC-01: tenant-local read/write default deny | `FoundationIdentity` whitelists `regulatory.draft.read` / `.write`; directory.read plus tenant-wide scope is required. Write additionally requires explicit regulatory write. IdP/body roles, generic directory.write and vessel grants confer no regulatory authority. `FoundationIdentityTests.Regulatory_capabilities_require_explicit_whitelisted_server_grants_and_tenant_wide_scope`; `RegulatoryRegisterTests` denial/cross-tenant/anonymous/read-only cases. |
| REG-SRC-02: incomplete drafts, bounded fields and explicit states | `RegulatoryRegisterPolicyTests`; `RegulatoryRegister.test.tsx`; no lifecycle request property. |
| REG-SRC-03: exact immutable current-source lineage | `RegisterDraft`/`RegisterRevision`, tenant/kind/ID/revision compound FK and SQL check; SourceKind constrained to Source for citations; context and PostgreSQL append-only guards; domain/API/persistence lineage and mutation cases. |
| REG-SRC-04: xmin, no-op, key reuse, race and atomic audit | Register command handler follows audited SaveChanges within a transaction with receipts; permissions and CSRF precede receipt replay. `RegulatoryRegisterTests` no-op/replay/race/concurrent-create/failure cases; `RegulatoryRegisterPersistenceTests` rollback cases. Losing transactions retain independent security observations but no business audit/history/receipt. |
| REG-SRC-05: fictional idempotent fixtures | Importer adds one Source+Requirement and their initial revisions per tenant, explicitly SyntheticUnverified/fictional. No law text, credentials or signatures. Reseed preserves existing drafts and revisions. PostgreSQL fixture tests authored; prior seed totals updated for eight actual import writes (17→25 initial audits). |
| REG-SRC-06: real editor/register/history/search/navigation | Real fetch transport only; TR/EN, loading/empty/errors/401/403, responsive fields, source revision selection and preview, immutable history pages, shell/Today links and guarded navigation. `RegulatoryRegister.test.tsx` mocked **test-only** transport; production has no mock API/login. Global workspace search exposes only implemented authorized destinations; register content search independently uses tenant-filtered SQL. |
| REG-SRC-07: retain dirty and uncertain commands | Frozen key/body retries after uncertain network/502/503; no double submit; conflict retains edits and requires explicit discard/reload through shell guard; offline reconnect does not overwrite dirty editor. React cases and real-host Playwright cases authored. |
| REG-SRC-08: read/denial audit fail closed | Register read/history/list audit; denied regulatory routes use durable SecurityAudit before denial; no query/reference/body logged. API cases author security-audit outage and rollback. This is not a declaration of complete HTTP auditing or privacy compliance. |

Projection mapping: `TC-regulatory-source-governance-projection` (14.02 projection-gap row31) maps to scope/field-state/search/failed-read/history cases. Additional local REG-SRC IDs express this preparation decision; they do not replace canonical FR/TC acceptance.

Routes: `/api/v1/source-drafts` and `/api/v1/requirement-drafts` provide GET/POST, `/{id}` GET/PUT, `/{id}/history` GET; bounded stable paging. Create requires expectedVersion0; update requires current xmin. All commands require CSRF and Idempotency-Key. Every later save (including a no-op) creates a revision; exact same-key replay creates none. Register replay rechecks current write authority and returns the exact committed immutable revision and xmin pinned by that command receipt. Later saves never replace a retried command result. Historical receipts without the original xmin fail closed (503); no result is reconstructed from the live draft. Profile/directory replay behavior is outside this repair scope. Hash covers stored declared content only; integrity is not legal authority.

## Deferred combined gate

CORE-BUNDLE-01 must build and export actual-host OpenAPI, regenerate consumer types (existing generated DTOs were **not** edited), replace/align the authored register boundary adapter with the generated contract, verify migration/designer/snapshot parity, apply migrations to real PostgreSQL, then run combined backend/domain/persistence/API, React/transport, frontend lint/typecheck/build, browser/real IdP, Python/source and security regression checks. The migration/designer/snapshot were authored with one local scaffold, not claimed as verified EF tool output. No dependency update occurred.

Real register Playwright cases are in `tests/Poseidon.Browser.Tests/register-specs/drafts.spec.ts` with `register.playwright.config.ts`, outside the anonymous default suite. Coordinator supplies an actual HTTPS host and **existing** private synthetic writer/restricted session states via POSEIDON_REGISTER_URL / POSEIDON_REGISTER_WRITER_STATE / POSEIDON_REGISTER_RESTRICTED_STATE. Writer membership must already have explicit grants; this worker did not provision/change accounts or lab memberships. Missing prerequisites fail, never skip/pass. The response-loss case forwards the command to the real server before dropping its response; it does not fabricate API data. No runner, host or lab was started here.

Independent QA of the final bundle target and required CI remain mandatory before coordinator publication/merge. This local checkpoint neither accepts this package nor completes canonical DEV-026/S3. Canonical progress stays 8/970SP,2/121tasks,0/196screens. Production IdP/roles, approved lookups, mandatory/lifecycle policies, experts, privacy/retention, pilot and release gates stay OPEN.
