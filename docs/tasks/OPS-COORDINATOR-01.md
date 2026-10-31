# OPS-COORDINATOR-01 — bounded nightly coordinator

Source locator: user instruction OPS-COORDINATOR-01 (2026-10-31); governance:
AGENTS.md, docs/architecture.md, docs/automation.md. Product requirements remain
on docs/sources/active.json; this operations package does not accept product SP.

Owner: coordinator worker, feature/nightly-coordinator. Dependencies: imported
OPS-HOURLY-01 account workers; root-local Node 26 and .NET SDK 10; separately
provisioned OAuth profiles. Parent owns independent review and registration.

Acceptance / traceability:

- OPS-N01: trusted repo-owned Markdown queue, unique IDs, explicit source,
  acceptance, dependencies and file scope; disabled by default. One task per tick.
- OPS-N02: persistent pre-effect lease; interrupted execution blocks reconciliation;
  isolated worktree from freshly fetched origin/main; preserve partial work.
- OPS-N03: 85% reserve in every fresh profile window under lock; Esra development
  first, Kadir fresh-context QA first; own eligible quota only; bounded calls/jobs,
  eight-hour durable deadline and at most fifteen minutes per model call.
- OPS-N04: fixed full backend/source/frontend/browser gate; zero test projects,
  empty test runs, failures and unexpected changes fail closed. Commit generated
  artifacts, regenerate and require clean exact HEAD before independent QA.
- OPS-N05: strict exact-SHA QA JSON; publishing requires separate operator flag;
  reconcile exact PR branch/head; every CI check passes before head-matched normal
  merge. No safety sign-offs, automatic accepted SP, force push or admin bypass.
- OPS-N06: disabled ScheduledTask preview, Interactive/Limited, IgnoreNew, ten-minute
  repeat, 45-minute limit; no registration in implementation tests.

QA follow-up source: user findings on `6772cac` (2026-10-31), scoped to this
worktree. OPS-N04 now requires fresh structured Python unittest, Vitest and
Playwright summaries: positive passing counts, no skips/todo/expected failures,
and no missing/malformed reports, in addition to the existing .NET TRX checks.
Full Python discovery and all fixed functional gate commands are retained.
OPS-N05 requires all four named checks: persistence-and-security,
frontend-and-browser, windows-automation and nightly-installer-construction;
missing checks stay pending, and additional reported checks must also pass.
OPS-N06 adds validated installer MaxJobs 1–20 (default 4), MaxModelCalls 1–40
(default 8), Hours 0–8 (default 8), Timeout 1–900 (default 900), all passed as
explicit CLI arguments. Zero hours expires immediately. Windows mocks cover
defaults, endpoints, rejected limits and invariant decimal formatting.

Verification: scripts/test_nightly_coordinator.py (fake metadata/CLI/gh and local
Git only), existing account/watch tests, scripts/windows_nightly_checks.py on
Windows without platform skips, frontend lint/typecheck and browser-config tsc.
Runtime full gate is implemented separately; this implementation task must not
invoke real models, auth/login, GitHub effects, registration or full network gates.

Delivery is a locally validated operations subpackage. Parent must independently
review the exact delivery commit before enabling the scheduler/publishing. It is
not canonical sprint/release completion or evidence of unattended 24-hour operation.
