# Persistent operations runtime — OPS-DASH-RUNTIME-01

Source/traceability: explicit user assignment dated 2026-10-31;
[hourly quota policy](hourly-automation.md), [existing coordinator gates](nightly-automation.md),
and the [typed GET `/ops/status` contract](../frontend/src/agents/contract.ts).
This is a local development operations subpackage. Canonical S0 progress remains
8/970 accepted story points (0.82%); the parent owns status and phase reports.

`py scripts/ops_runtime.py --serve` serves metadata only at
`http://127.0.0.1:8765/ops/status`. Add `--develop` to explicitly authorize the
trusted committed queue, and separately `--publish` to authorize the existing
exact-commit QA → all required CI → matching-head merge path. These flags are
operator activation; implementation/tests do not activate development. No planner
creates tasks. An empty queue continues idle monitoring. No API keys are used.

The HTTP server remains responsive while one background coordinator thread works.
Only the exact GET route is served. Host must be exactly `127.0.0.1:8765` (Vite's
proxy must forward that Host); duplicate Host, other paths, query strings and
mutations are rejected. No CORS wildcard, static files, arbitrary paths, credential
home reads, raw state, prompts, identities, source paths or CLI errors are exposed.
The camelCase response contains generatedAt, quota, coordinator with flat jobs,
activities, bounded sanitized events, alarms and accepted project progress.
Quota's timestamp/next_check names match the agreed UI contract. Every retained
lastKnown value carries its own observedAt; busy, unavailable, reconciliation and
stale (>75 minutes) values never authorize dispatch. No absent 5-hour window is invented.

Metadata refresh runs at startup, hourly and the earliest known future blocking
window's actual resetsAt + 60 seconds. All advertised future resets are observed,
including a short window while a weekly window still blocks. Task completion
also prompts a fresh measurement before the next hourly check. It starts no model turn. A reset alarm is
an observation, never proof that weekly quota has recovered. Each profile must
independently have fresh, known metadata with **every** window below 85%.
Esra is primary for both development and QA. Kadir is considered only after
Esra has verified quota at/above the 85% reserve; busy/unknown metadata never
authorizes fallback. Each profile still needs its own eligible quota. The
[model policy](model-policy.md) pins model/effort per task and review risk; the
actual selection is recorded before each model dispatch. Profile locks
cover metadata and CLI use; another account can run only on its own eligible
quota. The account adapter makes at most one proactive account/read refreshToken
attempt after protocol internal error -32603, in the same locked profile/process;
then reads fresh limits. Auth/network/other failures remain noneligible and raw
auth text stays redacted. Existing Poseidon-HourlyQuota remains separate.

The continuous mode explicitly extends the one-shot Coordinator with persisted
8-hour windows, default 4 new jobs and 8 CLI agent session starts per window.
These counters are **CLI starts**, not API request or token limits. Restart does
not reset a window. At its persisted deadline, a renewal requires no active or
unreconciled phase, inflight effect or lease; completed dependency records, job
history and waiting stages survive. Calls and new-job counters renew together;
renewals are recorded. A budget wait exposes its deadline as resumeAt. Partial
model/verification/commit phases and unknown processes require operator
reconciliation; blocked QA/tests never auto-resume. There is no repair loop.
One-shot defaults retain their previous bounded behavior.

The existing S0-ADR-01 `qa_used_tools` block must be reconciled by the parent after
the adapter fix and controlled probe. This runtime never approves or discards it.
QA continues read-only, no delegates/tools, strict JSON exact SHA and clean HEAD.
Only exact controlled startup diagnostic SHA-256 hashes may be allowlisted for
pinned CLI 0.166; unknown errors and actual tool events reject. Persisted diagnostic
records contain only item type, hash and fixed category. Category/keyword matches
alone cannot establish permission. Activation remains blocked until the parent
supplies/verifies the exact benign diagnostics and performs its controlled probe.

`.runtime/ops/events.jsonl` retains at most 500 allowlisted observations: actual
quota snapshots, threshold crossings, assignee, job phase, fixed reasons and
renewals. Activity files are sanitized, bounded records of known call receipts;
unknown historical timestamps remain null. No historical usage or reset times
are fabricated. `.runtime/ops/runtime.lock` is the durable service mutator lease;
the existing coordinator/watch/profile locks also remain. Stale locks are never
automatically removed. `.runtime/ops/service.json` records the requested next wake
and scheduler request outcome; requested hardware wake is never reported as a
confirmed future wake.

On Windows inspect `scripts/install-ops-runtime.ps1 -Preview` first. The explicit
installer constructs **Poseidon-DevelopmentRuntime**, Interactive/Limited,
absolute pythonw/script paths, quoted script argument, IgnoreNew,
AtLogon + periodic restart trigger, StartWhenAvailable and bounded restart policy.
Development/publishing require the separate `-Develop -Publish` flags. It does
not replace Poseidon-HourlyQuota. Installed runtime includes `--manage-wake`, an
explicit scheduler opt-in. Each changed earliest known blocking reset requests
**Poseidon-QuotaReset**, a one-shot `--wake-check` action at reset + 60 with
WakeToRun. The normal runtime lock prevents duplicate metadata/dispatch effects.
`-WakeOnly -WakeAt <epoch>` changes only that reset task; `-Preview` never registers.
No scheduler registration is part of tests. No power plan is changed.

Continuous availability requires Windows running, a usable logged-in interactive
session and the host awake. Hardware/firmware and Windows policy must support wake
timers for WakeToRun; shutdown/logout or unsupported hardware prevents coverage.
The service is persistent, but this is not a 24-hour uptime guarantee.

Acceptance/verification (synthetic files, fake workers/time/processes, local HTTP):

- `py -m unittest discover -s scripts -p test_ops_runtime.py -q`: exact contract,
  redaction, real resets/weekly blocking, unknown/busy/stale eligibility, loopback
  Host/route/mutation denial, bounded event log and durable safe renewal.
- `test_codex_accounts.py`, `test_codex_watch.py`, `test_nightly_gate.py`,
  `test_nightly_coordinator.py`: refresh fallback, quota locks/retention, strict QA,
  unchanged CI/merge gates and one-shot compatibility.
- `py scripts/windows_ops_checks.py -q`: explicit Windows-only scheduler mocks,
  pythonw quoting, principal, triggers, settings, one-shot wake and publish opt-in.
  It is deliberately separate from generic test discovery.

Parent owns independent QA, the controlled live adapter probe, full CI and any
activation. No model/auth/login/GitHub/scheduler side effects occur in these checks.
