# OPS-COORDINATOR-01 — bounded queue integration

User authority: overnight development, separate account quotas, push/PR and merge after tests plus independent QA. Engineering trace: [task contract](../tasks/OPS-COORDINATOR-01.md); [implementation QA follow-up](OPS-COORDINATOR-01-QA-FOLLOWUP.md).

The durable coordinator uses one phase per tick, isolated branches/worktrees, pre-effect leases, exact-commit independent QA, complete expected CI checks and server-side matching merge. New models run only after fresh eligible quota metadata. Confirmed stopped metadata failures may check the alternate profile independently; uncertain termination retains its profile lock. Exceptions in dispatched work propagate without retrying another account. Default publishing and dispatch remain off. Medium reasoning, scoped reads, no nested agents, 4 jobs / 8 calls / 8 hours bound the activation trial.

Trusted queue: S0-ADR-01; S0-THREATS-01 depends on merged ADR preparation; UI-REQUIRED-DATA-01 independently prepares an accessible TR/EN component. These are preparation subpackages; no automatic canonical SP, screen or expert acceptance. IdP, complete action audit, operational lifecycle and pilot/legal sign-offs remain open.

Integrated local verification: Release build 0 warnings/errors; locked restore; 43 .NET tests (0 failed/skipped), 12 React tests, 4 real-API Chromium desktop/mobile Playwright tests, lint/typecheck/build and generated API drift passed. All three .NET projects produced nonempty TRX. Vitest/Playwright structured reports passed the same controller parser. Source verification: 269 files / 34,958 chunks / 0 mismatches. Migration has no pending model changes. Windows installer Preview/mocks: 6 nightly and 1 hourly tests passed, no registration. Integrated generic Python run: 93 passed, 0 failed/skipped. Windows checks are separate: 7 passed.

Current phase: locally validated integration; independent target-commit QA, remote CI, actual registration and first complete queued delivery remain activation gates. The existing hourly task is active, but is not the development scheduler. No claim of verified 24-hour operation.

Next: validate exact committed integration independently, merge after four CI checks, install bounded scheduler and exercise a real queued task through development, fixed gate, QA, PR, CI and merge. Each tick writes numeric evidence and a readable local report; the main status file links current execution evidence separately from accepted backlog.
