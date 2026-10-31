# DIR-WORKSPACE-01 — company and fleet management workspace

State: implementation prepared on `feature/directory-workspace`; **NOT RUN / not accepted**. Native Esra development. No push, PR, merge, test, build, lint, typecheck, EF CLI or build-executing restore/generation is authorized during this package. Local commits are checkpoints only. Parent owns the integrated gate and independent target-commit QA.

## Requirement authority and traceability

Active candidate baseline: `poseidon-v46.3-20261030`, selected by `docs/sources/active.json`. Sources live under `analiz/POSEIDON GUNCEL 30.10.2026/`; prior source versions and historical 26.01 runtime/UAT are not current acceptance evidence. Source lookup used the explicitly supplied Python 3.13 executable, UTF-8, bounded query results and streaming filters for the cited locators.

| Source locator | IDs / package acceptance criteria | Implementation and authored evidence |
|---|---|---|
| `06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`, `sheet:Backlog/row:15` | DEV-014 / T-S1-002: queries and writes remain tenant-scoped | Membership/ActorResolver, tenant EF filters/composite FKs, commands; FoundationApiTests and DirectoryWorkspaceTests |
| Same workbook, row 16 | DEV-015 / T-S1-003: server role/vessel/object policy; denied mutation audit | Explicit engineering scope grants, restricted vessel projection/update, no indirect company metadata; authorization result handler; actor and real API negative tests |
| Same workbook, row 17 | DEV-016 / T-S1-004: attributed immutable audit foundation | Atomic audited SaveChanges for entity and receipt; separate required attempt/denial writer; rollback/immutability tests. Full canonical before/after/reason/operational-role audit remains open |
| Same workbook, row 20 | DEV-019 / T-S1-007: security regression definitions | Same-tenant vessel, cross-tenant, role, CSRF, spoofing, receipt/sink failure tests; NOT RUN |
| Same workbook, row 21 | DEV-020 / T-S2-001: vessel master preparation only | Concrete id/code/name/kind/company flow; no regulatory fields/provenance/freshness acceptance |
| `05_TECHNICAL_ARCHITECTURE/15.01_POSEIDON_v46.3_Product_Wide_API_Integration_Specification_TR.docx`, paragraphs 8, 9, 10 | Idempotent commands, trusted tenant/context, optimistic concurrency | Online receipts, server actor, explicit expectedVersion; concurrent retry/lost-response/stale tests |
| `04_UX_PRODUCT_DESIGN/13.04_POSEIDON_v46.3_Design_System_ve_UX_Standartlari_TR.docx`, paragraphs 68, 113, 129 | Empty state; visible keyboard focus; labels/errors/contrast | Responsive TR/EN shell, native controls, focused validation, error associations, draft confirmation and component tests |
| `04_UX_PRODUCT_DESIGN/13.02_POSEIDON_v46.3_196_Ekran_Gorselli_Kullanim_Katalogu_TR.docx`, paragraph 94 | Fleet Overview | Real list/detail/filter/search and management forms, not canonical screen acceptance |
| `05_TECHNICAL_ARCHITECTURE/14.01_POSEIDON_v46.3_Product_Wide_ERD_Data_Architecture_TR.docx`, paragraphs 22, 24–28 | Tenant/company and shared Vessel boundaries | Existing concrete entities and tenant/company relationships retained |

Architecture, automation, seed, dependency and security baseline documents and S0 threat-model draft `48613f5` were read. Applicable skills: `.agents/skills/poseidon-backend/SKILL.md` and `.agents/skills/poseidon-ui/SKILL.md`. Codebase MCP graph tools were not exposed; local `graphify query` was used before targeted code reads.

## Controlled engineering scope configuration

Memberships remain operator-provisioned under `Authentication:Oidc:Memberships`. ActorResolver resolves validated issuer+subject on each request; neither browser context nor OIDC role/vessel claims provision scope. Existing membership without new scope grants denies directory access by default.

Example shape (placeholder IDs, no account or credentials):

```json
{
  "Issuer": "https://configured-issuer.example",
  "Subject": "operator-provisioned-subject",
  "ActorId": "11111111-1111-1111-1111-111111111111",
  "TenantId": "22222222-2222-2222-2222-222222222222",
  "Permissions": ["directory.read", "directory.write"],
  "TenantWideRead": false,
  "TenantWideManage": false,
  "VesselGrants": [
    { "VesselId": "33333333-3333-3333-3333-333333333333", "CanEdit": true }
  ]
}
```

`directory.read` plus explicit `TenantWideRead` or `TenantWideManage` permits all same-tenant vessels and companies. `TenantWideManage` plus both read/write permissions permits company create/edit, vessel create and same-tenant vessel edit. `TenantWideRead` alone never grants writes. A restricted membership needs explicit vessel grants: grants allow same-tenant vessel reads; `CanEdit` additionally needs `directory.write`. Foreign-tenant grant IDs cannot escape tenant filters. Restricted users receive no company list entries or company detail metadata, including for the parent of their permitted vessel. They may see that vessel's relationship ID, not its company name/code. Restricted editors cannot create vessels/companies, edit companies or move vessels. Session exposes `canReadCompanies`/`canCreate`; record projections expose `canEdit`. No grant inventory is returned.

These are controlled engineering permissions, **not** the canonical operational role/delegation/job/protected-object catalog. Real IdP selection, provisioning/revocation operations and expert authority gates remain open. The resolver reads current in-process trusted settings per request; production configuration refresh/distribution and operator lifecycle are not implemented here. No test authentication handler is registered by the product.

## HTTP behavior

- GET `/api/v1/companies` and `/api/v1/vessels`: bounded offset/limit, optional `q` (up to 100 characters; ordinal database substring semantics, case-sensitive), and explicit vessel `kind=Ship|Tug`. Search is applied after scope. No search text enters security audit metadata.
- GET detail returns version and server edit capability. Foreign/invisible objects return 404. Existing process health, anonymous setup/session, `/` and `/agents` remain separate surfaces.
- POST collections creates actual rows; PUT `/{id}` edits code/name. Company create DTO has code/name; company update additionally requires expectedVersion. Vessel create requires explicit Ship/Tug and a non-empty same-tenant companyId. Vessel update accepts only code/name/expectedVersion. Kind/company association changes and hard delete are unavailable.
- Strict DTOs reject unmapped properties. Code/name are required, trim structural whitespace, bounded at 64/200 raw characters and reject control characters. JSON is bounded to 4096 bytes/depth 4. Invalid enum/Guid references and unknown version do not become defaults. No DTO-to-entity mass assignment.
- All writes require authenticated directory scope, action capability, valid antiforgery token and a validated Idempotency-Key (16–80 ASCII letters/digits/hyphen). Expected version is the exposed opaque numeric version; update conflicts return 409 `version_conflict`, never automatic overwrite. This package uses explicit expectedVersion rather than If-Match.
- Errors use safe codes and ProblemDetails/field errors: invalid 400, unauthenticated 401, role 403, invisible/cross-tenant lookup 404, duplicate business code 409, altered idempotency payload 409. Required audit/database outage is 503, including a denied command if its mandatory audit cannot persist. No raw database details, bodies, values, search, secrets or credentials are written to security events. List pagination/filter errors retain existing safe JSON codes.
- Creates and updates return 200 with the representation. OpenAPI response/request metadata is authored; export/generation is deferred.

## Transactions, retries and receipts

`core.CommandReceipts` is hand-migrated with a tenant+actor+key primary key, fixed action, SHA-256 fingerprint of normalized action/resource/input, resource ID and commit timestamp. Keys are bound to actor and tenant, and action/resource are validated through action+fingerprint. Receipts store no request/response body. Reusing a key for a different action/resource/payload conflicts. All commands validate fresh current permissions/object visibility before receipt lookup. A matching retry returns the current authorized representation with `Idempotency-Replayed: true`; it does not promise a historical response snapshot. UI takes version from that fresh representation.

Entity SaveChanges (including xmin and business audit), receipt SaveChanges (including receipt audit), and commit share one explicit PostgreSQL transaction. PostgreSQL uniqueness and xmin serialize competing requests; unique/concurrency failure rolls back and checks the winning receipt before reporting duplicate/stale conflict. No second successful business effect/audit is generated by replay. A process failure before commit leaves no durable receipt or write; committed write with lost acknowledgement is recovered by a fresh host/session using the same key. Uncertain commit transport exceptions may return 503; retry resolves durable state. No raw SQL/ExecuteUpdate/ExecuteDelete business bypass is used.

The separate SecurityEventWriter records `Attempted` before any mutation work; failures/denials are persisted independently before delivery. The authorization result handler records denied directory commands before returning 401/403. Missing required audit prevents writes/delivery. There is no independent HTTP `Success` event before commit or a fallible success-audit append after commit; committed evidence is the atomic EF audit. Selected legacy read/login/logout security events retain their existing semantics. This is not comprehensive HTTP audit or GDPR/KVKK compliance.

Receipts are append-only through EF and PostgreSQL triggers using the existing audit reject function. Receipt audit stores resource ID rather than the potentially long actor/key composite. Retention/controlled archive policy remains an external engineering/privacy gate. These are online master-data retries, not offline critical commands, device lifecycle or operational safety transitions.

## Seed and unknown data

Existing idempotent Company/Vessel/profile seed manifests are unchanged and operator edits remain preserved. No zero-valued profile, readiness or approval is invented when creating a basic vessel: absence of regulatory measurement/profile remains unknown. Existing synthetic fixtures that deliberately contain profiles stay valid. Receipt rows are observations of actual synthetic operations in tests, never prepopulated evidence/credentials or fake verified seed. Seed integration tests now assert an initially empty receipt table, tenant isolation, append-only protection and reseed preservation. Schema migration is separate from normal API startup.

## UI behavior and evidence boundaries

TR/EN management forms use real HTTP, server CSRF and ephemeral command keys. Company selection pages the real allowed companies. Create/edit form validation, loading/disabled state, duplicate-click guard, safe server/field errors and confirmations are explicit. Lost/ambiguous response locks the submitted payload and offers a user-initiated retry with identical key/payload. Conflict retains the draft and offers explicit reload/cancel. Local tab/filter/search/row/pagination/close/sign-out/link navigation confirms before discarding a dirty draft; native dialog restores focus. No browser beforeunload test bypass is used. Drafts are in memory and are not promised across browser reload/crash. Successful save refreshes real list and detail.

UI component tests mock HTTP and are labelled accordingly. Real Playwright additions verify only absent-IdP setup, anonymous scope and absence of management controls with a real API. They do not claim external login or authenticated browser acceptance.

## Parent follow-up and exact verification state

**All authored tests, build, lint, typecheck, EF migration/model validation, restore and OpenAPI generation: NOT RUN. No passing counts are claimed.** Source inspection and local Git operations are not a runtime gate.

Parent must export the actual host OpenAPI to `contracts/openapi.v1.json`, regenerate `frontend/src/generated/api.d.ts`, and replace the isolated temporary additions in `frontend/src/product/directory-contract.ts` with generated aliases for version/capabilities/commands. Do not disable the existing strict OpenAPI snapshot test. The hand-authored migration, designer, snapshot and project-only lockfile additions require parent EF parity/migration and locked-restore checks. No new external package version was added.

Authored coverage: Domain DirectoryInputPolicyTests; FoundationIdentityTests scope/revalidation; DirectoryWorkspaceTests real host/PostgreSQL commands, concurrency, retry after host restart, audit/receipt trigger failure rollback, roles/CSRF, same-tenant restriction, foreign relationships, normalization, strict DTOs, duplicates, stale writes, invalid keys, actor/tenant receipt boundaries and search; PostgresSeedTests receipt isolation/immutability/reseed; DirectoryWorkspace.test.tsx forms/transport/errors/conflict/draft/refresh; prior foundation fixtures declare explicit scope; Playwright real setup assertions. Parent must run these alongside all existing OIDC negative tests and the full integrated gate, then independent QA for the exact target commit. Crash/lost-response test models restart after a durable response rather than forcibly killing PostgreSQL at a commit boundary; broader fault injection remains a follow-up.

Canonical acceptance remains **8/970 SP (0.82%), 2/121 tasks, 0/196 screens**. Full S1, regulatory vessel master, operational authority catalogs, critical offline workflows, compliance and expert/pilot sign-offs remain open. S0 threat-model draft is preserved with expert approval OPEN; interrupted QA is not accepted. Current phase: integrated package development prepared for parent verification. Next step: parent contract generation and integrated gate, then independent review; no publication in this task.
