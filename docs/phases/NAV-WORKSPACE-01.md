# NAV-WORKSPACE-01 — implemented product package

Current phase: coherent local implementation, integrated developer gate PASS; independent target-commit QA pending. **NOT ACCEPTED**. No push, PR, merge or extra agents. Coordinator owns publication and acceptance. Exact profile prerequisite approval is committed in [the review](../reviews/profile-draft-ee3339e.json).

Delivered: responsive TR/EN company/fleet/profile shell with real URLs, profile deep links, freshly authorized vessel context, native history guards, mobile focus management and authorized keyboard search. Search uses explicit server provisioning, scoped stable pagination, fixed durable audit and safe implemented destinations only. Dirty/uncertain drafts survive menu/language/search/history interactions; unknown directory writes now retain the navigation freeze through idempotent retry. Offline data is explicitly unverified. No new entity, migration, fixture, dependency, external font, cache or federation flow was added.

The clean isolated baseline preserved all prior profile work. [Task source/acceptance/test mapping](../tasks/nav-workspace-01.md) records exact active30.10.2026 citations and delivery decisions. MCP code graph tools were unavailable; Graphify query preceded targeted reads. Python313 bounded retrieval and exact source locators were used; archives were not treated as implementation or passing tests.

## Integrated gate

Code and authored tests were prepared together before the integrated gate. Actual results below are local developer evidence, not independent QA.

| Check | Result |
|---|---|
| Tool restore and locked solution restore | PASS; no dependency/lock changes |
| Release build | PASS; 0 warnings/errors; final workspace corrections rebuilt for final gate |
| Domain MTP | 46 PASS /0 failed /0 skipped;1.927s |
| Persistence MTP, actual PostgreSQL | 25 PASS /0 failed /0 skipped;56.214s |
| API MTP, actual PostgreSQL/OIDC/OpenAPI parity | 78 PASS /0 failed /0 skipped;4m16.648s |
| Actual Development-host OpenAPI export and locked TypeScript generation | PASS; workspace/session/search contracts exported from host on5386; task-owned host stopped |
| Frontend typecheck / lint / production build | PASS |
| Full React component/transport suite |160 PASS /0 failed /0 skipped;17.54s |
| Desktop/mobile Chromium |22 PASS /0 failed /0 skipped;37.0s, final bundle including dirty native Back and neutral capability heading |
| EF model/migration parity |PASS; no pending model changes |
| Python/source-byte gate |NOT RUN for NAV; scripts and controlled-source bytes unchanged; prior profile127 Python /269 files /34,958 chunks evidence retained, not reclassified as NAV passing tests |
| Graphify AST update |PASS;2,848 nodes /4,420 edges /232 communities;10 existing/config/data zero-node warnings; no model/API calls |
| Whitespace/JSON/host cleanup |PASS; JSON/canonical-count assertions and git diff --check; no listeners on task ports5386/5388/4388 |

Commands: `dotnet tool restore`; `dotnet restore Poseidon.slnx --locked-mode`; `dotnet build Poseidon.slnx --configuration Release --no-restore`; actual `dotnet run --project backend/Poseidon.Api --configuration Release --no-build --no-launch-profile --urls http://127.0.0.1:5386`, export `/openapi/v1.json`, then `npm run generate:api --prefix frontend`; final Release rebuild. MTP suites use `dotnet test --project tests/Poseidon.{Domain,Persistence,Api}.Tests/Poseidon.{Domain,Persistence,Api}.Tests.csproj --configuration Release --no-build` (one project per invocation, no VSTest flags). EF: `dotnet ef migrations has-pending-model-changes --context PoseidonDbContext --project backend/Poseidon.Persistence --startup-project backend/Poseidon.DbMigrator --configuration Release --no-build`. Frontend: `npm run {typecheck,lint,test,build} --prefix frontend`. Browser: `npm test --prefix tests/Poseidon.Browser.Tests` with task ports5388/4388. `graphify update .`; `git diff --check`. Existing installed locked modules were usable; npm ci was unnecessary. Actual.NET10/Node26 executables came from repository shared `.tools` prepended to process PATH; Python used explicit Python313.

Initial gate findings: frontend typecheck rejected unsupported Testing Library `exact` options; lint required a captured dialog node for cleanup. Initial React136 PASS/15 FAIL identified a shared-location test setup error, new field-label mismatches and the changed forbidden-link absence contract. Technical tests now reset to `/`; product tests explicitly start `/app`, and the restricted navigation assertion requires absence. The full suite passed after adding seven explicit search input/pagination/denial/malformed-link cases. Two later regressions verify a neutral heading before capability provisioning and preservation of a validated profile deep link through sign-in; final160 passed. API initial73 PASS/5 FAIL (78 total,4m36.695s) caught missing workspace event codes in the audit writer allowlist: endpoints safely failed503. Production allowlist corrected. Second API77 PASS/1 FAIL (78 total,4m28.454s) then exposed an ArgumentOutOfRangeException in the runtime LINQ skip/concat array optimization at the destination/vessel pagination boundary. Explicit bounded assembly replaces that path. The same pagination assertions remain unchanged for the final full API rerun, which passed78/78. No tests were skipped, removed, marked expected-failure or loosened to conceal a product failure.

Browser evidence separation:22 comprises4 real connection/retry,8 real anonymous/setup/deep-link/keyboard,6 mocked technical dashboard and4 synthetic authenticated profile/workspace cases across desktop/mobile. Authenticated UI mocks do not prove external IdP/TLS login; actual endpoint scope is independently exercised by PostgreSQL/API tests. No production test-auth exists.

Remaining gates: independent QA of final NAV commit; existing frontend advisory remediation/release review (prior13 moderate; no dependency changes or fresh advisory scan); canonical S0/S2 dependencies, operational roles, approved lookups, mandatory/lifecycle policy, real external IdP/TLS browser validation, privacy/retention and expert/pilot/release approvals. This shell grants no human sign-off or operational readiness. Canonical **8/970 SP (0.82%),2/121 tasks,0/196 accepted screens** unchanged. Next: coordinator obtains independent target-commit QA and controls any publication/merge.

Final local diagnostics are in ignored `.runtime/nav-{domain,persistence,api,build-final,ef,ui-typecheck,ui-lint,ui-test,ui-build,browser-final,graphify-final}.log`; initial API failures remain in `nav-api-initial.log` and `nav-api-second.log`. These logs are developer diagnostics, not approval records. No code changes followed the passing final affected suites; subsequent edits package this report and status mapping. Independent QA must review the final local commit, not an earlier preparation or profile commit.
