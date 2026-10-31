# FOUNDATION-APP-01

Phase: preparation vertical slice implemented; integrated verification **NOT RUN — user deferred**. No DEV-013/014 acceptance, S0 completion or canonical story-point increment. Parent-owned project-status files are untouched. No push, PR, merge, other account/model or delegation.

## Sources and traceability

Active baseline: `docs/sources/active.json`, `poseidon-v46.3-20261030`, updated candidate, not expert sign-off. Architecture, security baseline, seed-data and dependency-policy govern this slice.

| Locator | Foundation acceptance / test map |
|---|---|
| `18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`, Backlog row 14, DEV-013 / T-S1-001 | Framework OIDC code+PKCE, token signature/issuer/audience/lifetime, nonce/state/correlation, secure bounded cookie, login/logout, safe returns: `FoundationIdentityTests`, `OidcProtocolTests`. DEV002/DEV006 external gates remain open. |
| Same source, Backlog row 15, DEV-014 / T-S1-002 | Server membership mapping, tenant-scoped list/detail, cross-tenant 404, missing role/mapping 403, anonymous 401, spoofing: `FoundationApiTests`. Depends on DEV-013 acceptance. |
| `14.01...Product_Wide_ERD_Data_Architecture_TR.docx`, paragraph 7; `15.01...Product_Wide_API_Integration_Specification_TR.docx`, paragraph 9 | Business entities remain tenant-scoped; client tenant alone is untrusted. Existing compound FK/query filters retained; no web administrative context. |
| `13.02...196_Ekran_Gorselli_Kullanim_Katalogu_TR.docx`, paragraph 94 (Fleet Overview); `13.04...Design_System_ve_UX_Standartlari_TR.docx`, paragraphs 68, 113, 129 | Preparation company/fleet views, empty/error/loading, keyboard focus and labels; `ProductApp.test.tsx`, `product.spec.ts`. Full canonical screen acceptance remains deferred. |
| `06.01...PRD_SRS_Gemi_Romorkor_TR.docx`, paragraph 194 | Status meaning is textual, never color alone. No operational readiness/approval projection. |
| `docs/security-baseline.md` HTTP/security audit table | `SecurityEventTests` and `FoundationApiTests`: independent append-only events, nullable anonymous tenant, fixed action/result codes, trusted actor/correlation/UTC, immutable triggers and tenant-filtered internal queries. |

Sources were retrieved with bounded `scripts/query_sources.py` queries; no corpus dump. Code graph MCP was unavailable and `graphify query` reported no graph in this worktree; targeted reads used instead.

## Delivered scope and decisions

- `/api/v1/auth/session`, GET `login`, POST `logout`. ASP.NET Core OpenIdConnect, authorization code + S256 PKCE, framework protected state/correlation/nonce and validated ID token; HTTPS metadata and issuer/audience/lifetime/signature checks. Only `openid` scope. OIDC ID/access/refresh tokens are neither persisted in cookies nor returned to JavaScript/API JSON. The session projection intentionally supplies only an antiforgery request token for logout. The encrypted HttpOnly session cookie contains minimal subject/issuer/internal actor claims and expires at the earlier of ID-token expiry and 30 minutes; no sliding refresh.
- Issuer+subject map to exactly one operator-provisioned actor GUID, tenant GUID and permissions. IdP role/tenant claims and request headers/body/query cannot choose scope. `DirectoryRead` / `DirectoryWrite` are server-owned policies; no write business route is exposed. Cookies are rechecked against current mapping on each request. Cookie authentication never redirects an API denial to the IdP.
- Secure `__Host-` cookies, Lax session and None transient OIDC correlation/nonce cookies, Strict antiforgery cookie. Logout requires authentication and framework antiforgery validation (header or form token). Safe return URLs stay under `/app`. Missing federation configuration gives safe login 503 and usable anonymous session/setup state; technical health/status remain database independent. No dev-login backdoor, local credential fixture, registration/reset/MFA implementation.
- PostgreSQL context registered only for authorized business requests; connection string required. No startup migration/import. Existing Company/Vessel synthetic fixtures, tenant filters, compound FKs, xmin and audited SaveChanges remain intact. Bounded stable list (default 25, max 100, offset max 100000), detail, and Ship/Tug filter. DTOs exclude evidence, safety decisions and tenant-selecting inputs.
- **Business mutations are next package**, including actual write role+CSRF enforcement, validation, xmin conflict 409 and UI forms. POST/PUT/DELETE company/vessel mutations are absent. No hard delete or safety/readiness approval.
- Separate `audit.SecurityEvents` migration and short-lived writer context/connection preserve real denial events outside rolled-back business transactions. Normal business context rejects direct event writes. UPDATE/DELETE/TRUNCATE trigger uses existing immutable-audit function. Successful reads/login/local logout require audit persistence; missing DB returns safe 503. Denials and failed/absent-setup login retain their safe response if audit persistence fails and emit a fixed diagnostic warning/error with generated correlation only. A five-second independent audit deadline bounds failure handling.
- Audit includes internal actor ID, nullable trusted tenant, fixed action/result, optional detail resource GUID, server-generated correlation and UTC. No bodies, query strings, raw URLs, IP, external subject, tokens, cookies or display names. No security-event projection endpoint. Existing business-write audit remains separate. Security events are observations and deliberately have no synthetic seed events. Tests generate actual observed events using synthetic identities.
- `/app` responsive TR/EN shell: session/workspace, sign-in/out, companies, fleet, Ship/Tug filter, list/detail, pagination, retry, loading/empty/401/403/unavailable/setup states. Real typed HTTP consumer with no runtime mock success. `/` and `/agents` remain. UI component mocks establish UI behavior only; Playwright setup/anonymous journeys use the real API.

## Configuration and external gates

Provide through trusted deployment configuration/secret storage:

```text
ConnectionStrings__Poseidon=<PostgreSQL connection string>
Authentication__Oidc__Authority=<HTTPS discovery authority>
Authentication__Oidc__Issuer=<exact validated issuer>
Authentication__Oidc__ClientId=<registered client>
Authentication__Oidc__ClientSecret=<only if required by chosen client registration>
Authentication__Oidc__Memberships__0__Issuer=<exact issuer>
Authentication__Oidc__Memberships__0__Subject=<provisioned subject>
Authentication__Oidc__Memberships__0__ActorId=<internal nonempty GUID>
Authentication__Oidc__Memberships__0__TenantId=<existing tenant GUID>
Authentication__Oidc__Memberships__0__Permissions__0=directory.read
```

`POSEIDON_DATABASE` remains an alternative connection configuration. Use the existing separate migrator to apply migrations, and the existing controlled synthetic importer only in Development/Test. Never expose these configuration values through request binding. Provision real memberships only after tenant creation. No credentials are committed.

Real IdP/client registration, TLS origin and callback URIs `/signin-oidc` and `/signout-callback-oidc`, end-session interoperability and DEV002/DEV006 remain external gates. Logout clears the local session and initiates IdP end-session with client ID; no ID-token hint is retained. Providers requiring an ID-token hint need a protected server-side token store follow-up. A logout Success event denotes local logout authorization/completion, not proof of federation-wide revocation. IdP owns account/MFA/reset and federated session revocation. No access-token refresh or downstream token proxy in this slice.

Use same-origin HTTPS for the authenticated browser UI/API. Technical pages and absent-IdP setup can run over local HTTP; secure authenticated cookies require HTTPS. Production needs protected persistent ASP.NET Data Protection keys shared across replicas, trusted proxy/TLS configuration and least-privilege application DB credentials separate from the migrator/table owner. No request forwarded-header trust is added here. Configure infrastructure access logs to exclude callback queries/secrets; application framework request/auth/EF diagnostic categories are suppressed to avoid raw sensitive details. Anonymous denied events may lack tenant attribution; never fabricate a tenant FK.

Application/table-owner separation, external immutable export/tamper detection, retention/legal hold, privacy rights workflows, session revocation store, IdP rate/MFA controls, operational monitoring and full accessibility/security review remain release work. This is not a GDPR/KVKK or complete HTTP audit compliance claim. If the audit DB is unavailable, denial events cannot be guaranteed durable; successful business data delivery is blocked and business write routes are absent.

## Execution at the foundation package boundary

The native developer authored the foundation and tests without running build/tests,
as requested. Parent then completed the integrated gate: Release build zero warnings/errors;
68 .NET, 46 React and 14 desktop/mobile Playwright tests passed. Lint, typecheck,
frontend build and EF model parity passed. Actual Development OpenAPI was exported,
committed and used to generate consumer aliases. Unknown Vessel kinds are rejected
with focused transport coverage. The first gate found host-configuration timing and
OIDC issuer claim-removal bugs; production code was fixed and the entire backend
suite rerun. No assertions were weakened. Two DbContexts now require explicit
`--context PoseidonDbContext` in EF CLI commands; CI/coordinator/docs include it.
Focused coordinator regression: 16 PASS. Full Python automation, independent exact-commit
QA and all four remote CI checks remain pending until separately recorded.

See the [phase report](../phases/FOUNDATION-APP-01.md). Synthetic OIDC verification
does not replace actual external IdP/TLS browser acceptance. Canonical story points
and source dependencies remain open. Business mutations are the next package.
