# Bounded nightly automation

OPS-COORDINATOR-01 is a standard-library Python coordinator. Nothing is scheduled
or dispatched by installation of these files. The checked-in queue is empty.
Publishing and development both default to disabled. The parent owns activation,
review and any GitHub registration, push or merge of this implementation.

The operator edits `automation/nightly-queue.json` with trusted repo-owned Markdown
tasks. Each item has exactly `id`, `task` (relative Markdown path), `source` (exact
locator), `acceptance` (nonempty strings), `dependencies` (queue IDs), and `scope`
(relative file/glob allowlist). No shell commands come from queue metadata. Commit
the queue and task files before use. Dependencies must be merged by this queue;
cycles remain undispatched. Example metadata:

```json
[{"id":"OPS-EXAMPLE-01","task":"docs/tasks/example.md","source":"controlled source:section",
  "acceptance":["Specified behavior passes its tests"],"dependencies":[],
  "scope":["backend/Example/*.cs","tests/Example/*.cs","frontend/src/generated/api.d.ts"]}]
```

`py scripts/nightly_coordinator.py --once` validates/reports without dispatch.
An operator may add `--enable` for development and separately `--publish` for
push/PR/CI-gated merge. Bounds are `--max-jobs` (1–20, default 1),
`--max-model-calls` (1–40, default 2), `--hours` (>0–8, default 8), and
`--timeout` (1–900 seconds, default 900). Every invocation advances at most one
queue item through one phase. Existing limits in durable state cannot be raised
by later flags. The deadline starts with the first invocation, including a disabled
preview; beginning a new overnight session requires operator reconciliation and
archiving the old state. No automatic budget reset or repair loop exists.

State, numeric usage with profile, command exit codes, exact commit and PR URL live
in `.runtime/nightly/state.json`, `report.json` and readable `report.md`. Raw CLI,
tool, account and authentication output is never persisted. Statuses are
`developing`, `verifying`, `awaiting_qa`, `awaiting_ci`, `merged`, `blocked`, and
`waiting_for_account`. A coordinator lock and pre-effect lease protect dispatch.
Stale locks are never automatically deleted. Reconcile processes, the preserved
worktree, local/remote head and PR before manually recovering an interrupted job.
An interrupted model, gate or commit becomes blocked with reconciliation needed.
Unknown PR creation is queried by exact branch/head; absence after an uncertain
operation blocks rather than creating a duplicate. Known PR/CI reads can resume.
Blocked jobs never automatically resume and unrelated account-worker jobs do not
gate this queue. Generic old smoke tasks are never resumed.

EsraPro is preferred for development; KadirPlus for independent light review.
Fresh `account/read` and `account/rateLimits/read` metadata is checked under the
selected profile lock immediately before calls. Every numeric window must be
known, finite and below 85%; an alternate profile requires its own verified quota.
No credential files are read or copied by coordinator code, no login is attempted,
and API keys and inherited Codex/session/tool identifiers are stripped. Model
completion alone does not accept a task. A failed/quota-interrupted call preserves
its worktree and blocks; no second development call or endless repair follows.

Worktrees start from `git fetch origin main` and exact `origin/main`, never a root
checkout/reset. Development uses the existing Workers.execute helper, editing
only the task worktree with no publication. Root-local `.tools/dotnet/dotnet.exe`
and `.tools/node/node-v26.13.0-win-x64` supply SDK 10 and Node 26 plus UTF-8.
The Windows PowerShell host also needs the parent-installed .NET 9 Core/Desktop
runtimes. Linux uses explicitly resolved PATH tools when local Windows binaries
are absent. Missing tools/projects/scripts block. Sensitive/unexpected paths,
symlinks, binary files and review diffs over 80,000 characters block manual review;
full review data is never silently truncated. Historic tracked fixture files are
preserved; they cannot be modified through the sensitive-path gate.

Before commit or QA the fixed gate runs local tools restore, locked solution
restore, Release solution build, `dotnet test --solution Poseidon.slnx` without
filters (nonempty per-project TRX, no skips), migration model check, Python generic
discovery, source verification, frontend/browser npm ci, API generation, lint,
typecheck, component tests, frontend build, Chromium installation and real-API
Playwright. Commands use the job directory and root tool paths, bounded by the
remaining deadline. Generated changes are included in the normal commit, then
API generation runs again and a clean exact HEAD is required. Ports are distinct
ephemeral loopback choices via validated `POSEIDON_API_PORT`/`POSEIDON_UI_PORT`.
Defaults remain API 5080, preview 4173 and dev 5173. Port races fail closed;
Playwright never reuses or kills another server.

Independent QA is a separate fresh read-only CLI process with shell/apply-patch
and MCP tools disabled, no resume session and no publication. Its input contains
source/acceptance, full bounded diff and fixed check evidence as data. Any tool
event invalidates the review. Only JSON exactly shaped as
`{"approved":true,"commit":"exact SHA","issues":[]}` is accepted. HEAD and
worktree must stay unchanged. This does not create pilot/SME evidence or increase
canonical accepted SP. Live CLI tool-disable compatibility is not validated by
mocked tests; verify with a controlled parent-owned activation trial.

Publishing uses trusted `.tools/gh/bin/gh.exe` (Linux PATH fallback). It reconciles
PRs by branch/head, polls once per tick, waits for both backend/frontend and all
additional reported checks, and refuses failed/skipped/changed-head checks. Normal
merge uses `--merge --match-head-commit`; required branch protection/reviews still
apply. There is no admin bypass. No GitHub side effects are exercised by tests.

The installer supports `-Preview`, `-Enable`, and separately `-Publish`:

```powershell
pwsh -File scripts/install-nightly-coordinator.ps1 -Preview
```

Without Preview, registration is an explicit operator action. The definition uses
an absolute pythonw path and quoted absolute script, Interactive/Limited,
IgnoreNew, repeat every ten minutes, and a 45-minute execution limit. The process
limits each tick to forty minutes and each command/model to at most fifteen.
It requires the user to remain logged in and the machine awake; it cannot run while
asleep, logged out or powered off. No power-plan or permanent keep-awake changes
are made. CI constructs the actual definition on Windows with Preview only;
`windows_nightly_checks.py` is separate from generic Python discovery and has no
platform skip. Existing backend/frontend CI jobs remain intact.

Local delivery verification (2026-10-31): 25 coordinator lifecycle tests,
9 fixed-gate/queue/CLI tests, 28 existing account-worker tests, 13 watcher tests,
and 2 actual Windows Preview construction checks pass without skips on this host.
Frontend lint/typecheck, standalone Playwright config typecheck and valid/invalid
port import checks pass using existing package caches. Graphify AST update passes
(with expected non-code zero-node warnings). No models, login, GitHub effects,
registration, backend full gate or real browser execution were invoked here.
Current phase: locally validated operations subpackage; parent independent review
and controlled activation remain next. This is not canonical release acceptance.
