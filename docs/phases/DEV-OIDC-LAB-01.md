# DEV-OIDC-LAB-01

Current phase: coherent local developer lab implementation; independent QA pending, NOT ACCEPTED. Coordinator owns publishing and merge. Exact source/acceptance/configuration locators and safe operation: [task](../tasks/dev-oidc-lab-01.md).

## Independent rejection repair — 2026-11-01

Target rejected: `3a7801263cf48debb860786de358592f892f2065`. Started from that exact commit with a clean worktree; preserved the entire existing package, fixtures and operator data. Same DEV-OIDC-LAB-01/LAB-04 and source scope. Committed NAV prerequisite proof was read and retained. No delegation, publication, credential output or self-approval.

The prior ambient scrub missed the generic/web host providers' prefix stripping. Additional `DOTNET_Kestrel__Endpoints__...` or `ASPNETCORE_Kestrel__Endpoints__...` entries survived the JSON overlay and opened HTTP listeners. `environment()` now compares effective configuration keys after prefix/delimiter normalization, covering the same sensitive settings and direct/prefixed URL/port configuration. Unrelated environment variables remain available; no production code, auth pipeline, TLS validation, dependencies or API contract changed.

Tests were authored before the fix. The new actual-host regression **failed before repair**, observing two extra dynamic HTTP listener ports. After repair it **passed**: the real Release API loaded generated lab JSON through `Host.CreateDefaultBuilder`/`ConfigureWebHostDefaults`, HTTPS liveness passed strict CA/hostname validation, and every TCP listener owned by its PID had a loopback address and port5443. The child is terminated in `finally`; logs/child diagnostics are discarded. This verifies actual hosting behavior rather than only a dictionary or mocked configuration provider.

Affected checks after implementation:

| Command | Result |
|---|---|
| `py -3.13 -m unittest discover -s scripts -p test_oidc_lab.py -v` | 5 PASS,0 failed/skipped;0.150s. New matrix includes70 prefix/case/delimiter/settings combinations and preservation of unrelated environment. |
| `py -3.13 scripts/windows_oidc_lab_checks.py` | 2 PASS,0 failed/skipped;18.037s: actual-host listener isolation plus actual private bootstrap/restart and strict TLS/wrong-host denial. |
| `py -3.13 -m unittest discover -s scripts -p 'test_*.py' -v` | 132 PASS,0 failed/skipped;151.277s; full affected Python suite. Safe local log `.runtime/oidc-repair-python.log`. |
| `py -3.13 scripts/oidc_lab.py start`, `... browser`, `... stop` | All exit0;2 real Keycloak/HTTPS/PostgreSQL browser journeys PASS,0 failures; task-owned services stopped, data/configuration retained. Safe count evidence remains at the private locator. |
| `graphify update .` | PASS;2936 nodes/4558 edges/239 communities;11 existing zero-node warnings. AST only. |

Final repair handoff: all affected checks passed; no implementation blocker. `git diff --check` passed; task ports5443/8443/55432 have no remaining listeners after stop. Original integrated .NET/frontend checks below remain historical evidence for unchanged code, not newly executed repair results. No source, schema, generated OpenAPI/type, dependency or lock changes. Current phase: local rejection repair, awaiting coordinator independent target-commit QA; NOT ACCEPTED. Canonical8/970SP,2/121tasks,0/196screens and all external gates remain unchanged.

Delivered: pinned Docker Keycloak HTTPS provider and PostgreSQL; explicit random private bootstrap and server memberships; loopback HTTPS actual app/static product; Development-scoped private CA backchannel; no fake auth or production bypass. Authenticated browser suite and transport/bootstrap tests authored before executing the integrated gate. Existing profile precision/history/idempotency and NAV regressions retained.

Prerequisite: committed independent NAV proof approves exact `ff1e95ebf5f43ba5883f69e5e52b0cb2f205b775`; implementation started clean at `029d1ba`. No stopped-worker changes were discarded. No push, PR, merge, extra agent or external auth-account change. Only explicitly authorized synthetic local provider accounts were provisioned.

## Actual developer gate

All coherent implementation and tests were authored before the initial gate. Subsequent findings were repaired and affected checks repeated; no tests/assertions were skipped or weakened. Toolchain: shared repository `.tools/dotnet`10.0.403, `.tools/node/node-v26.13.0-win-x64`, explicit Python3133.13.13. .NET10 Microsoft.Testing.Platform commands below do not use VSTest filters.

| Check | Actual result |
|---|---|
| `dotnet tool restore`; `dotnet restore Poseidon.slnx --locked-mode` | PASS; dependencies/locks unchanged |
| `dotnet build Poseidon.slnx --configuration Release --no-restore` | PASS;0 warnings/errors |
| `dotnet test --project tests/Poseidon.Domain.Tests --configuration Release --no-build` |46 PASS;0 failed/skipped;4.725s |
| Same command for `tests/Poseidon.Persistence.Tests` (actual PostgreSQL) |25 PASS;0 failed/skipped;3m08.002s |
| Same command for `tests/Poseidon.Api.Tests` (actual PostgreSQL/protocol/OpenAPI) |91 PASS final delivery run;0 failed/skipped;3m54.887s (preceding membership-repair run also91 PASS). |
| `npm run lint`, `npm run typecheck`, `npm test`, `npm run build` with `--prefix frontend` |PASS;160 React tests;0 failed/skipped;58.44s;production bundle built |
| `POSEIDON_API_PORT=5480`, `POSEIDON_UI_PORT=4473`; `npm test --prefix tests/Poseidon.Browser.Tests` |22 PASS;36.3s. Existing smoke/UI contract suite; its mocked authenticated cases remain distinct from the real lab. |
| `py -3.13 scripts/oidc_lab.py start`; `... browser` |2 real Playwright library journeys PASS;0 failures, including final pinned-tools rerun after restart. Actual Keycloak26.8.1 signed tokens + HTTPS API/static product + PostgreSQL; no route mocks, fake issuer or test auth. |
| `py -3.13 -m unittest discover -s scripts -p 'test_*.py' -v` |131 PASS final full suite;0 failed/skipped;145.157s. Final portable fixture subset4 PASS;0 failed/skipped;0.040s. |
| `py -3.13 scripts/windows_oidc_lab_checks.py` |1 PASS; actual owner-private bootstrap, identical restart files, strict TLS success and wrong-host denial. Final focused bootstrap suite4 PASS;0 failed/skipped. The environment test isolates root-tool discovery so unit tests remain portable. |
| `dotnet ef migrations has-pending-model-changes --context PoseidonDbContext --project backend/Poseidon.Persistence --startup-project backend/Poseidon.DbMigrator --configuration Release --no-build` |PASS;no pending model changes |
| Actual strict HTTPS `/openapi/v1.json` export; `npm run generate:api --prefix frontend` |PASS;semantic parity and unchanged committed schema/generated types |

Real lab manager journey: OIDC credential login/code/PKCE and protected Secure/HttpOnly cookie; company create/edit; precise `9007199254740993` and `0.1234567890123456789012345678` profile saves; explicit Unknown retained; reload/deep link; dirty guard cancel retains input; second save retains immutable prior revision, including browser history display; scoped workspace search/native Back; missing-CSRF logout403; real logout/Keycloak confirmation; anonymous API401; subsequent login requires credentials again. Restricted journey: genuine second OIDC login; no company/search/create capabilities; company list empty/detail404; company/profile writes403; search403; ungranted same-tenant and foreign vessel/profile404; only granted vessel listed, read-only draft. Existing invalid/expired/signature/issuer/audience/nonce/state/correlation, mutation-CSRF, idempotency, precision/history, search/audit/scope tests remain in the full API gate.

## Findings and limits

Initial PowerShell certificate constructor required explicit algorithm types; strict Python3.13 TLS then identified a missing authority-key identifier. Both generator defects were fixed; no TLS check was relaxed. Certificate generation writes no system/user root. .NET custom trust checks expiry/EKU/root and preserves platform hostname validation; backchannel request and redirect guards reject HTTP, other hosts/ports and other realms, even if discovery metadata advertises them. Production/Test cannot enable this trust.

Initial real-provider journeys failed at Keycloak's first-login profile requirement. Fresh fixtures now include synthetic required name/email fields, with emailVerified=false; the already-created synthetic accounts completed that normal form using only synthetic values. Credentials/data were preserved. Manager then failed the product's real membership check. Bounded boolean/count diagnostics located constructor binding silently dropping the empty-grant entry; a focused JSON binding test reproduced expected2/actual1. Explicit property binding fixed it and strict binding rejects malformed entries. Temporary diagnostic instrumentation was removed before final build/full91-test gate. Test-only selector corrections match the actual “Regulatory profile draft” heading and scope Search to its dialog; no product assertion was removed.

The initial Playwright test runner wrote automatic error-context DOM snapshots despite screenshots/traces being off. Those two task-owned files were removed without outputting them. Final lab tests use the direct Playwright library, which has no test-runner artifact recorder; no trace/screenshot/video/HAR/network log or DOM snapshot is created. Only bounded pass/fail counts/source line metadata persist at `.runtime/oidc-lab/browser-counts.json`. Child stdout/stderr and raw sensitive exceptions are discarded. Scan of generated graph artifacts found0 private-secret matches; `.runtime`/`.tools` are now explicitly excluded. Historical ordinary smoke reports remain separate and contain no lab login data.

The lab uses a worktree-specific Compose project and loopback5443/8443/55432. Start preserves existing realm/operator data and reuses the task-owned app; stop verifies exact worktree DLL/process marker, removes only this project's services and retains volumes/private configuration. Partial bootstrap fails closed rather than regenerating credentials. Public files serve the actual built `/`, `/agents`, `/app` product; unknown API paths remain404. Production static hosting is unchanged. Private account locator and safe start/stop commands are in the task; no passwords are committed or printed.

Dependency limitations: no new npm/NuGet/Python package; earlier13 moderate frontend advisory report remains a separate release-remediation item, not resolved or freshly rescanned here. Production app/migrator DB privilege separation, protected shared DP keys, IdP/data locality/MFA, session revocation/retention/privacy/legal assessment, external immutable audit and all canonical/expert/pilot/release gates remain OPEN. This is neither an enterprise IdP choice nor complete HTTP-audit/GDPR/KVKK approval.

Local ignored evidence: `.runtime/lab-{build-delivery,api-delivery,domain,persistence,react,lint,typecheck,ui-build,smoke-browser,python-delivery,bootstrap-delivery,windows-bootstrap-delivery,ef-delivery,generate-delivery,graphify-final}.log`; lab-safe browser counts at the private locator above. Failed initial gates are documented as failures, not acceptance evidence.

Canonical8/970SP (0.82%),2/121tasks,0/196screens unchanged. Production IdP/data locality/MFA and all canonical prerequisites, expert/pilot/release gates remain OPEN. Next: actual integrated gate followed by coordinator independent review of exact local commit; no self-approval.


## Final local handoff

Actual restart check: read-only PostgreSQL aggregate counts from `core.Companies`, `core.RegulatoryDraftRevisions`, `audit.AuditEvents` and private configuration hashes were captured in memory; `stop`, `start`, repeated `start` left all unchanged. The same rebuilt app then passed both real browser journeys. Final CLI-only `start → browser → stop` also exited0, using the pinned root tools and ambient-configuration scrub. No unrelated process/volume was removed; task data/configuration remain private and reusable. Final listeners on5443/8443/55432/5480/4473:0.

Final driver excludes inherited OIDC memberships, connection strings, Kestrel listeners and lab settings before setting its explicit Development configuration. This prevents an ambient production membership array or additional listener from extending the fixture. No tool/global account settings are changed.

Final scans:0 generated private-password/client/bootstrap/DB-secret matches in staged files, frontend bundle or derived graph; credential file is gitignored;0 sensitive browser artifact files. `git diff --check` and staged whitespace checks PASS. Graphify AST update PASS (2934nodes/4556edges/239communities;initial update reported11 existing zero-node warnings;no model/API call). Source originals and locks unchanged; no new source corpus extraction or expert verification claimed.

All required local developer checks above passed. No implementation blocker remains. This is a locally validated developer environment package, **not accepted** and not a canonical sprint/release gate. Next: coordinator independent QA against the final local commit; coordinator controls any publication/merge. Canonical8/970SP,2/121tasks,0/196screens remain unchanged; all external gates listed above remain OPEN.
