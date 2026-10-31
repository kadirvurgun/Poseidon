# DIR-WORKSPACE-01 — draft delivery report

Date: 2026-11-01. Branch: `feature/directory-workspace`. State: **integrated local gate passed; independent QA pending; not canonical acceptance**.

Delivered code: server-provisioned tenant-wide/vessel grants; scoped company/fleet reads/search; company and vessel create/code-name update; strict validation/CSRF; explicit xmin expected version; transactional online command receipts; required attempt/denial security events and atomic committed EF audit; responsive TR/EN real-HTTP workspace with server capabilities, accessible forms, uncertain-save retry, retained conflict drafts and guarded local navigation.

Authored tests cover domain input rules, trusted scope resolution, real PostgreSQL/API transaction and receipt races/restart/rollback, scope/tenant/CSRF/role negatives, duplicates/stale edits, receipt append-only/seed behavior, mocked React forms/transport/draft preservation and real Playwright anonymous/setup boundaries. Existing OIDC negative tests remain. Authored tests are not passing evidence.

Historical native-author handoff (before parent gate): tests **NOT RUN**; build **NOT RUN**; lint **NOT RUN**; typecheck **NOT RUN**; EF CLI/model/migration checks **NOT RUN**; restore/export/generation **NOT RUN**. User explicitly deferred these until the parent integrated gate. No push/PR/merge or delegated agents in this package.

Historical handoff follow-up (completed by parent below): parent exports OpenAPI and generates TypeScript aliases, removes isolated temporary contract additions, validates hand-authored migration/designer/snapshot and project-only lock updates, runs the whole integrated gate, resolves findings and obtains independent exact-commit QA. External IdP/operator lifecycle, canonical operational role matrix, regulatory profile/provenance and full audit/privacy/retention gates remain open. Basic vessels intentionally have no invented regulatory profiles/measurements. Online directory retries do not implement critical offline commands. S0 threat model remains draft, approval OPEN.

PR #10 is actually merged at `0bf7914` (local ancestry), superseding old status text that still said foundation QA/CI pending. Its historical local test counts do not verify this package. Task/source/criteria/configuration and evidence details: [directory-workspace-01](../tasks/directory-workspace-01.md).

Canonical progress remains **8/970 SP = 0.82%; 2/121 tasks; 0/196 accepted screens**. Current phase: locally authored integrated subpackage awaiting parent verification, not complete canonical S1/S2 acceptance. Next step: parent generation and integrated gate, then independent QA. No regulatory/safety/compliance/expert sign-off is supplied.

Local implementation checkpoint: `33b08929d2b81f2ef97b4537166609e9c6ec3f19`. A following local finalization commit records contract-decoding/draft refresh refinements and this delivery documentation. Both remain unverified; hooks that might run tests are disabled for local package commits. Local Graphify AST update completed with no model call; it is derived navigation, not behavioral verification. Concurrent architecture/automation document edits were preserved outside this task's commits.

## Parent integrated gate — actual results

All code and authored tests were prepared before execution, as requested. Parent then
exported actual Development OpenAPI, regenerated TypeScript and replaced temporary
contract additions with generated aliases (Esra Luna follow-up). No assertion was
weakened or skipped. Earlier NOT RUN text above records the author handoff only.

| Gate | Actual result |
|---|---|
| Locked restore and Release build | PASS; zero warnings/errors |
| Domain unit | 22 PASS |
| Persistence, real PostgreSQL, writer validation | 25 PASS |
| Actual API/PostgreSQL/OIDC | 51 PASS |
| React component/transport | 63 PASS |
| Chromium desktop/mobile | 16 PASS |
| Python automation | 127 PASS |
| Lint/typecheck/frontend build | PASS |
| EF migration model parity | PASS |
| Current source verification | 269 files,34,958 chunks,0 mismatch |

All suites: zero failed/skipped. The writer validation unit was strengthened to use
a configured EF provider and assert the exact unknown-event error, then rerun alone
(1 PASS); no missing-provider exception is counted as validation evidence.
Browser scope: 4 real API connection/retry,6 mocked ops UI,6 real API product anonymous/setup/keyboard.
Authenticated forms have component plus real API/PostgreSQL coverage; external IdP/TLS
browser acceptance remains OPEN.

The initial gate found an unused pagination parameter, test API/analyzer mismatches and
missing fixed write codes in the security sink. Corrected production allowlist accepts
only new trusted literal codes; unknown code rejection remains tested. Backend rerun
passed all51 API tests including concurrent same-key and different-payload races,
rollback/restart, tenant/object scope and stale-version denials. Existing OIDC checks remain.

Local evidence: `.runtime/directory-{Domain-tests,final-Persistence,final-Api}.log`,
`.runtime/directory-ui-{test,typecheck,build}.log`,`.runtime/directory-browser.log`,
`.runtime/directory-python.log`,`.runtime/directory-writer-validation-test.log`.
These are derived local logs, not committed secrets or production approval evidence.
Independent exact-commit QA is pending; no push/PR/remote CI for this package by user decision.
Next: independent review and continued coherent product development before publication.

## Independent review repair

Initial independent Esra Sol review rejected exact `e7d5b83`: unchanged-value updates
could avoid SQL xmin enforcement. `2443596` explicitly marks a permitted scalar
modified for company/vessel updates; audited EF UPDATE now enforces expectedVersion
even for touches. Same-key replay still avoids a second write. Test-only reader
interception deterministically pauses original business SELECT, commits a competing
edit through the real API and verifies409/no receipt/no failed business audit; both
resource types and successful touch/replay are covered (4 new cases). Production
startup/auth has no test hooks. Full API rerun:51 PASS,0 failed/skipped,98.264s;
Release build0 warnings/errors. Fresh exact-commit review is still required.
