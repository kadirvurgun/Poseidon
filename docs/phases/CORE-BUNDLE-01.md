# CORE-BUNDLE-01 — combined foundation developer gate

Date: 2026-11-01. Status: locally validated developer candidate, awaiting coordinator independent exact-commit QA and CI; NOT ACCEPTED. Target: the local commit containing this report (parent73b2352); final commit ID is supplied in the delivery message. No self-approval. Publishing, PR, merge and independent approval belong to the coordinator. Canonical progress remains **8/970 SP, 2/121 tasks, 0/196 accepted screens**.

## Scope and authority

Initial worktree was clean at `73b2352`. Reconciled the complete unverified range against independently approved base `93de7b6b376fb4592150c5c7b15a8c52e39bd4b3`, preserving Today `58b609d`, REG `73b2352`, cadence `5784b82`, review evidence and ignored private lab configuration. No extra agents, publication, credential rotation, production authorization changes or operator-data deletion.

Read AGENTS.md; QA/backend/UI skills; architecture, automation, security, seed/dependency policies; active sources; Today/REG source/task/phase contracts and OIDC handoff. The explicit user instruction opens this later combined gate and supersedes authoring-time deferral for this bundle only. Prior/interrupted evidence is historical, never this target's result.

Source/acceptance mapping remains [Today TW-01–06](../tasks/today-why-01.md) and [REG-SRC-01–08](../tasks/reg-source-draft-01.md). Bounded Python313 queries and locator reads confirm current Why API paragraphs327–333, ERD paragraphs119–124, DEV-026 Backlog row27 and19.01 test-gate paragraphs7–16. No source catalog adoption or external legal verification.

## Integration corrections

- Bind REG POST as a route handler so ASP.NET writes its result and retains OpenAPI metadata.
- Register fixed REG read/list/history/save audit action codes and source-revision denial code. Required audit continues to fail closed; no authorization permission is relaxed.
- Export built API OpenAPI and regenerate TypeScript with locked tools. REG boundary types now refine generated DTOs; request payloads compile against generated commands.
- Generate an actual EF model scaffold; empty Up/Down confirms semantic parity. Retain tool-generated snapshot/REG designer model with the existing migration identity; remove the temporary empty probe. Actual migrations, seed and immutable/audit/race behavior run against PostgreSQL.
- Give source/revision selectors explicit labels independent of option text; add a new requirement-selection component regression. Suppress workspace fetch on malformed routes; fix fetch-order-dependent component fixtures, accessible history selector, invalid Testing Library option and renamed disclosure focus locator. Render the Today error component without reading refs through an invoked render helper; retain focus behavior and control-character rejection without lint exemptions.
- Prepare REG capabilities only in explicit private lab server membership matched to the existing synthetic provider subject. Preserve credentials, restricted membership and all other grants. Login uses genuine signed provider sessions; no claim grants permission. Generate transient owner-private session states, consume the same REG case manifest/configuration projects, remove states in finally.
- Pinned Playwright1.64.1 writes automatic error-context artifacts even with trace/video/screenshots disabled. Authenticated execution therefore uses the committed direct library runner, never the test runner. Only bounded fixed case/project names, counts, timings and source line numbers persist. CLI is used for discovery only; ordinary mocked smoke artifacts are separate.

## Actual execution record

Pinned shared root tools: .NET SDK10.0.403 / EF10.0.14; Node26.13.0; locked npm dependencies; explicit `py -3.13` Python3.13.13. All backend projects use Microsoft.Testing.Platform `--project ... --configuration Release --no-build`, no VSTest flags or filters.

| Check | Result / duration |
|---|---|
| `dotnet tool restore`; `dotnet restore Poseidon.slnx --locked-mode`; both `npm ci --prefix ...` | PASS; locks unchanged. npm reports existing13 moderate frontend advisories; browser0. Restore durations not separately captured. |
| `dotnet build Poseidon.slnx --configuration Release --no-restore` | Latest PASS;0 warnings/errors;8.80s. |
| Domain full project |61 PASS;0 failed/skipped;4.541s. Unchanged domain code was not rerun after audit/model formatting fixes. |
| Persistence full project, actual PostgreSQL | Final29 PASS;0 failed/skipped;240.151s. |
| API full project, actual PostgreSQL/HTTP/protocol/contract | Final116 PASS/0 failed/skipped;774.507s. Initial105 PASS/11 FAILED/0 skipped;465.866s retained separately. |
| Actual built `/openapi/v1.json` export; `npm run generate:api --prefix frontend` | PASS; actual schema and generated types updated together, no manual schema editing. |
| `dotnet ef migrations has-pending-model-changes --context PoseidonDbContext --project backend/Poseidon.Persistence --startup-project backend/Poseidon.DbMigrator --configuration Release --no-build` | PASS after actual scaffold and final build; no pending changes. |
| Frontend lint / typecheck / build | Final PASS after source-label repair;0 lint warnings; production bundle built493ms (Vite portion). Lint/typecheck wall times not separately captured. |
| Full `npm test --prefix frontend` |192 PASS across10 files;0 failed/skipped;24.46s. After Today render refactor, affected Today file19 PASS;20.00s. After label repair/new case, affected REG file14 PASS;18.83s. This covers193 unique component cases; no full193 rerun is claimed. |
| Ordinary Playwright desktop/mobile | Initial22 PASS/2 Today selector failures; affected Today rerun2 PASS/27.2s. All24 configured cases covered; no unexecuted REG inference. |
| REG config `--list` |6 discovered in1 file:3 cases × desktop/mobile. Direct safe runner uses these same cases and configuration projects. Initial6 executed; affected citation2 rerun after the label fix. Final coverage is6/6,0 skipped; no unchanged cases repeated. |
| Real signed-provider HTTPS/PostgreSQL manager/restricted journeys |2 PASS/0 failed;38.510s. Includes real Today/Why and existing directory/profile/precision/history/scope/CSRF/logout assertions. Distinct from production IdP acceptance. |
| REG authenticated direct runner | Initial6 discovered,4 PASS/2 FAILED (draft-citation source selection on both viewports);50.040s. Affected citation rerun2 PASS/0 failed;29.660s driver duration (desktop5.843s/mobile1.368s case time). Four unchanged cases retain their actual initial passes. Safe lines/timings retained; no raw errors/DOM/state diagnostics. |
| `py -3.13 -m unittest discover -s scripts -p 'test_*.py' -v` |133 PASS;0 failed/skipped;235.463s. |
| `py -3.13 scripts/windows_oidc_lab_checks.py` |2 PASS;0 failed/skipped;26.460s. Actual loopback HTTPS host, ambient-listener isolation, private bootstrap preservation and strict TLS/wrong-host denial. |
| Active source-byte/chunk verifier |269 files/34,958 chunks;0 mismatches. Normal-path CLI hit Windows MAX_PATH; invoking the same verifier with extended absolute root passes. Existing source bytes were unchanged;0 files copied. |
| `graphify update .` |PASS;3190 nodes/5161 edges/261 communities;12 existing zero-node warnings. AST only, no semantic/model call. |
| Private-secret scan |Initial0 matches across287 changed/generated/bundle/graph files; final0 matches across293 staged/bundle/graph files; private credentials remain ignored. No values printed. |

## Coverage reconciliation and initial failures

All three backend test projects in Poseidon.slnx are present and run in full; no zero-discovery acceptance. Approved-base counts46 Domain/25 persistence/91 API grow to61/29/116:15/4/25 newly authored cases respectively, including theory rows. React160→192 includes Today19 and REG13 across all10 test files; the label repair adds one meaningful REG test (14), yielding193 unique covered cases. Ordinary Playwright22→24 adds Today on both viewports. REG is separately discovered/executed as6 real-host cases, never counted as mocked coverage. The existing2 real OIDC journeys remain a separate count.

Initial failures are retained, not promoted to success: build2 endpoint-overload errors; generation attempted while npm restore was still populating tools; EF short `-c` selected Debug instead of Release, corrected to explicit `--configuration Release`; initial frontend lint/typecheck and7 component failures; ordinary Today focus lookup2 failures; API11 failures (fixed audit allowlist plus anonymous-audit assertion querying tenant-hidden rows); source CLI MAX_PATH; REG2 exact-label failures (source search HTTP200 with matching rows; nested option text prevented the exact label lookup), repaired with explicit labels and covered by the new creation/selection component case and both real viewports. No tests were skipped, removed or weakened. Anonymous Today audit now asserts both fixed action codes through a restricted raw aggregate in the test only, preserving tenant projection isolation.

## Remaining gates

All local checks above completed. Cleanup confirms0 task-port listeners (5443/8443/55432/5480/4473/5481),0 authenticated diagnostic artifacts and0 transient REG state files. Only owned services were stopped; private provider/configuration files and volumes/operator data were preserved. No independent QA or CI was performed by this worker, which also repaired implementation. Coordinator must review the exact eventual commit and run required CI before any future publication/merge. Historical OIDC review approves only its original exact commit.

Production IdP/MFA/revocation, DB role separation, retention/privacy/legal assessment, canonical dependencies and source admission, approved lookup/lifecycle policies, operational readiness/safety permits, expert signatures, pilot/release and existing dependency advisories remain OPEN. This bundle neither activates law nor manufactures verified evidence, human approval, GDPR/production acceptance or canonical progress.

## Reproduction and evidence

Ignored local logs: `.runtime/core-bundle/` contains tools/restore/npm logs; `build-model-final.log`, `domain.log`, `persistence-final.log`, `api-final.log`, `generate-final.log`, `ef-final.log`, `lint-register-repair.log`, `typecheck-register-repair.log`, `react-final.log`, `react-today-refactor.log`, `react-register-repair.log`, `ui-build-register-repair.log`, `browser.log`, `browser-today-repair.log`, `register-discovery.log`, `register-initial-counts.json`, `lab-stages.json`, `register-repair-stages.json`, `python.log`, `windows-lab.log`, `sources-extended.json`, `graph-final.log`, `cleanup.json` and safe scan counts. Initial failures remain in their original logs. Raw authenticated child output was discarded, never logged.

Reproduce backend with the pinned root tools: locked restore, Release build, then `dotnet test --project tests/Poseidon.Domain.Tests --configuration Release --no-build` and the same for Persistence/Api projects. Export the actual Development host OpenAPI before `npm run generate:api --prefix frontend`, strict API snapshot/consumer checks. Frontend commands are `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`, each with `--prefix frontend`. Ordinary browser command uses `POSEIDON_API_PORT=5480`, `POSEIDON_UI_PORT=4473` and `npm test --prefix tests/Poseidon.Browser.Tests`. Affected rerun appends `-- specs/today.spec.ts`.

Real lab commands (private data stays ignored): `py -3.13 scripts/oidc_lab.py prepare_register`, `... start`, `... browser`, `... register_browser`, `... stop`. Use finally cleanup. Only `draft-citation` needed rerun: `POSEIDON_REGISTER_CASE=draft-citation` selects both configured viewports; unset it for the full6-case run. Discovery uses `npm test --prefix tests/Poseidon.Browser.Tests -- --config register.playwright.config.ts --list` with direct-driver environment/HTTPS URL and private state paths; listing reads no sessions. Actual execution must use the direct driver, since CLI error artifacts cannot be safely disabled in this pinned runner. The direct driver consumes the same manifest/configuration, rejects zero selected cases and generates fresh states through normal provider login. Browser TLS relaxation is confined to synthetic test contexts; driver readiness uses strict fixture CA/hostname checks.

Source verification ran `verify_sources.verify(extended_root)`, with the Windows extended-length absolute worktree path under explicit Python313 to address Windows MAX_PATH;269 byte hashes and34,958 chunk lineage records matched. The finite probe emitted only status/count/type/boolean diagnostics and confirmed the label defect; it is not acceptance evidence.

Current phase: completed local combined foundation developer gate, awaiting independent exact-commit QA. Next step: coordinator review/required CI, with no publishing by this worker. Canonical and external gates above stay unchanged.

Changed-code evidence against the approved base: 47 staged files under backend/frontend/scripts/tests/contracts. SHA256 of the sorted compact JSON manifest (`path`, SHA256 of staged Git blob; JSON keys sorted) is `29da527e691e2de6c7e707e3a3a72e7bb72dd2daf4f47d9c027a240f390b613e`. Full bounded manifest is retained at `.runtime/core-bundle/code-evidence.json`; documentation is excluded from this fingerprint. Final local commit identifies the complete candidate tree.
