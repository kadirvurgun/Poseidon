# DEV-OIDC-LAB-01

Status: authored local developer package; verification recorded in the phase report. NOT ACCEPTED. No publishing/merge, account changes or extra agents. Independent target-commit QA belongs to the coordinator.

Clean starting commit `029d1ba`; no stopped worker changes were present. Prerequisite proof `docs/reviews/nav-workspace-01-ff1e95ebf5f4.json` approves exact `ff1e95ebf5f43ba5883f69e5e52b0cb2f205b775`. Foundation, profile and NAV implementations/reports are preserved.

Read AGENTS, backend/UI/QA skills; active baseline `poseidon-v46.3-20261030`; architecture, automation, security, dependency and seed rules; foundation/profile/NAV task and phase reports; current foundation ADRs `s0-module-boundaries.md`, `s0-events-storage.md`, `s0-offline-ai.md` including their OPEN approval fields. Their historical diagnostics-only API descriptions are not current implementation claims. MCP discovery unavailable; local Graphify query used before targeted source reads.

## Controlled locators and acceptance

Source paths relative to `analiz/POSEIDON GUNCEL 30.10.2026/`:

| Source locator | Scope and verification |
|---|---|
| `06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`, `sheet:Backlog/row:14`, DEV-013/T-S1-001 | LAB-01: real browser authorization-code/PKCE against signed provider tokens; bounded secure session and logout; `lab/journeys.spec.ts`, retained `OidcProtocolTests` invalid/expired/signature/issuer/audience/nonce/state/correlation coverage. DEV-002/006 canonical gates remain open. |
| Same file rows15–17, DEV-014/015/016 | LAB-02: server-controlled issuer/subject memberships; explicit tenant/vessel scope; real restricted browser/API denial and retained directory/profile/search/audit integration tests. Fixture manager is not an operational safety role. |
| `05_TECHNICAL_ARCHITECTURE/14.01_POSEIDON_v46.3_Product_Wide_ERD_Data_Architecture_TR.docx`, paragraphs7,10,21–24,77–79 | Tenant boundary, Core/IAM ownership, immutable audit. No new operational entity, schema, credential table or evidence seed. |
| `05_TECHNICAL_ARCHITECTURE/15.01_POSEIDON_v46.3_Product_Wide_API_Integration_Specification_TR.docx`, paragraphs9,64–67,287–295,296 onward | OIDC/OAuth2, IdP MFA/SSO ownership; server-derived scope; object authorization and separate human authority; bounded security observations. Existing API pipeline remains authoritative. |
| Current foundation decisions `docs/tasks/foundation-app-01.md`, Delivered scope and decisions / Configuration and external gates | Existing BFF cookie/OIDC adapter, minimal claims, no token persistence, operator memberships, HTTPS, no ID-token-hint retention. Lab adds scoped Development trust and static assets only; production IdP selection is unresolved. |
| Profile/NAV task and phase contracts | LAB-03: actual company mutation, exact decimal + Unknown profile save, immutable history, refresh/deep-link/search/native history/dirty guard; preserve existing idempotency and scope regression suites. |

LAB-04: random private credentials/configuration, loopback services, restart preservation, task-only stop without data deletion; `test_oidc_lab.py` and actual bootstrap/TLS `windows_oidc_lab_checks.py`. LAB-05: explicit Development-only CA trust with hostname/expiry/server-EKU/root validation, HTTPS local-realm request guard and no redirect trust; `DevelopmentOidcTransportTests`. Browser evidence contains counts and bounded source line numbers only, with no sensitive artifacts. Native Node26 executes the typed Playwright library journeys in `tests/Poseidon.Browser.Tests/lab/run.ts`; no test-runner credential artifacts are enabled.

The current14.02 data dictionary was consulted as a UI field-contract source (including its header and `sheet:Data Dictionary 2316/row:1836` user lookup context); it does not grant an enterprise IdP or identity default. IAM authority in this package follows the exact14.01/15.01/18.01 locators above. POL-DEFAULT-01 and all unresolved lookup/role/mandatory/expert decisions remain unchanged.

Real-browser discovery also exposed a provider-independent foundation defect: constructor configuration binding silently omitted the manager entry containing an explicit empty grant array. `Membership` now provides fail-closed default property binding, preserving that manager and the restricted reader; malformed configured membership entries fail startup rather than silently disappearing. `FoundationIdentityTests` reproduces JSON binding, manager capability preservation and malformed configuration rejection. No request or IdP role can construct these memberships.

## Development fixture decision

Keycloak is a **DEVELOPMENT ONLY** interchangeable OIDC fixture. Official [downloads](https://www.keycloak.org/downloads) identified stable26.8.1 on2026-11-01; Docker pull verified digest `sha256:3f3c5f8e03c614774b4bd9173b88391fefa4153bfc010d8012d59ad825377332`. Compose pins version+digest. [Container](https://www.keycloak.org/server/containers), [TLS](https://www.keycloak.org/server/enabletls), [hostname](https://www.keycloak.org/server/hostname), [realm import](https://www.keycloak.org/server/importExport) docs govern HTTPS PEM configuration, exact public issuer and import-on-first-start (existing realm skipped). PostgreSQL18.6 also pins the locally verified repository digest. No added npm/Python/NuGet dependency.

Provider uses private persistent dev-file storage; actual product uses PostgreSQL, migrations and the existing idempotent synthetic importer. Provider and app databases are exclusively synthetic. Bootstrap realm/client/users and membership configuration are separate explicit lab operations, not production seed import. No roles are trusted from IdP claims. No safety evidence/verified status/signature is generated.

## Safe operation

Windows PowerShell7; use shared repo root `.tools` pinned .NET10.0.403/Node26.13.0 and Python3.13.13 (the explicit Python313 installation used by `py -3.13` here). Docker must be running. The driver resolves .NET/Node only from this worktree or its ancestor repository's `.tools`; it requires Python3.13.13 and never installs or changes tools/accounts. Build the coherent package first using `docs/development.md` locked restore, Release build, frontend install/build, browser install and Chromium install. Lab consumes Release output and frontend/dist; it never silently builds a different package.

From this isolated worktree:

```powershell
py -3.13 scripts/oidc_lab.py init
py -3.13 scripts/oidc_lab.py start
py -3.13 scripts/oidc_lab.py browser
py -3.13 scripts/oidc_lab.py stop
```

Start app at `https://localhost:5443/app`, provider at `https://localhost:8443`, DB loopback55432. No wildcard interface or HTTP provider listener. Ports must be available; do not stop unrelated listeners. Compose project name is worktree-derived. `start` reuses its own running app and existing realms/data; importer adds missing fixtures without overwriting operator edits. `stop` checks process command line against this worktree's exact DLL/marker and runs only this compose project's `down`; volumes/private files remain. Never use volume prune or delete other projects. Partial initialization fails closed and preserves files for operator inspection. Stop the lab before rebuilding the loaded API DLL. Restart after rebuild to exercise the new code.

Private credential locator: `.runtime/oidc-lab/credentials.json` (gitignored, inherited owner-only ACL). It contains the two synthetic accounts `lab-manager` and `lab-restricted`. Open it locally to enter credentials; do not paste its contents into terminals, issues or reports. Generated client/bootstrap/DB secrets, realm import and TLS key are in that same restricted directory. No fixed password is documented. Configure additional lab memberships only by operator editing this private server configuration and restarting; client claims cannot grant permissions. Never point these fixtures at an operator/production database.

Generated CA is valid30days; server certificate29days. No root is installed or globally trusted. API OIDC backchannel uses explicit local CA at exactly `https://localhost:8443/realms/poseidon-lab` only in Development; redirects and arbitrary hosts fail closed. Browser test context alone permits local synthetic TLS errors. For manual browser use acknowledge the localhost certificate interstitial in a dedicated development profile; do not change the system trust store or browser-wide TLS policy. Expired fixture certificates require deliberate operator regeneration after stopping; init does not overwrite them or data.

Do not enable Playwright tracing/screenshots/videos/HAR, DEBUG network/auth logging or attach private configuration. The direct Playwright library runner writes `.runtime/oidc-lab/browser-counts.json` with pass/fail counts and bounded test-source line numbers only; raw child stdout/stderr are discarded. It deliberately avoids the test runner, whose automatic error-context DOM snapshot can include entered fields even with tracing/screenshots off. API lab logging and Docker service logging are disabled. Public technical checks and the existing minimal attributed audit remain; no full login URLs, token redirects, query strings or secrets are persisted as diagnostics. No bearer tokens in browser storage. Logout clears local session then confirms provider end-session in Keycloak; it does not claim enterprise-wide revocation. Graphify explicitly excludes `.runtime`/`.tools` as well as controlled source corpora.

Production IdP/data-locality/MFA, protected shared Data Protection keys, session revocation, deployment TLS, application/migrator DB privilege separation, retention/legal hold, immutable external audit, privacy/legal assessment, full canonical dependencies and expert/pilot/release gates remain OPEN. Local lab success cannot accept DEV-013 or an enterprise IdP decision. Canonical progress stays8/970SP,2/121tasks,0/196screens.
