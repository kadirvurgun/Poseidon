# OPS-COORDINATOR-01 QA follow-up — 2026-10-31

Source: independent QA findings on branch feature/nightly-coordinator at
6772cac1d35c3bc6a7738fcbe390d82ffa58bf47. Requirement/acceptance traceability:
[OPS-N04/N05/N06](../tasks/OPS-COORDINATOR-01.md). Scope is this coordinator
worktree only; locally validated operations subpackage, no sprint/release acceptance.

- OPS-N05: all four expected CI checks must be present and pass, including
  windows-automation and nightly-installer-construction. Each missing-check
  regression stays pending; existing additional-check/head/failure rules remain.
- OPS-N04: full unittest discovery writes numeric JSON including skips and
  expected/unexpected outcomes. Vitest JSON and Playwright JSON summaries are
  required alongside existing reporters. Exit zero cannot bypass missing,
  malformed, empty or skipped results; fake-runner and actual synthetic unittest
  outcomes cover this. Existing full .NET/source/frontend/browser gate is retained.
- OPS-N06: validated installer limits (jobs 1–20/default 4, calls 1–40/default 8,
  hours 0–8/default 8, timeout 1–900/default 900) are explicit CLI arguments.
  Zero hours expires immediately; fractional hours use invariant formatting.

Verification:

- `py scripts/nightly_python_tests.py --report .runtime/ops-coordinator-followup/python.json`:
  90 passed, no failures/skips (28 account, 13 watcher, 8 import, 26 coordinator,
  12 gate/summary, 2 query and 1 source-verification tests). Structured JSON reports
  total=90, passed=90, all other counters zero.
- Final `py -m unittest discover -s scripts -p test_nightly_gate.py -v`:
  12 passed, no failures/skips, including the two additional summary tests.
- `py scripts/windows_nightly_checks.py -v`: 6 passed, no failures/skips;
  three actual Preview construction checks and three mock checks.
- Graphify AST-only update and `git diff --check`: passed.

No auth, model calls, network, push or registration performed. The full functional
runtime gate was not executed in this constrained follow-up. Current phase: local
operations subpackage ready for parent integration and independent review. Remaining
acceptance gates: parent integrates latest main and runs separate QA against the
delivery commit before activation; no local code blocker identified.
