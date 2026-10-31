# S0-ADR-01

Source: 18.01 Backlog row5 DEV-004 / T-S0-004; docs/architecture.md

Prepare the engineering ADR set required by DEV-004. Read docs/architecture.md, security-baseline.md and the current source pointer. Use scripts/query_sources.py for small targeted extracts of 14.01, 15.01 and 23.03; do not load the corpus. Write docs/decisions/s0-module-boundaries.md, s0-events-storage.md, s0-offline-ai.md. Include exact source file/section, alternatives, selected engineering direction, consequences, implementation evidence and open approval fields. Distinguish implementation from proposals. Domain -> application -> infrastructure/API dependency directions must be unambiguous; no cross-module DbContext leaking into domain. Transactional outbox is proposed until implemented. Offline replay must not invent active permit authority. AI is advisory, tenant-authorized retrieval and evidence citations required. Leave expert/legal/operational sign-offs open. Add acceptance evidence to this task document without claiming DEV-004 accepted. Do not modify project-status or phase files. Documentation delivery only; all repository checks remain required.

Acceptance:
- Module boundaries, dependency direction, outbox, storage, offline and AI decisions are explicit with source locators
- Unresolved D-STATE-01, IdP, data location and expert decisions stay open; no invented approvals
- Relative document links resolve and canonical accepted SP unchanged

## Documentation delivery evidence — 2026-10-31

Phase: S0, local ADR preparation for architecture review. Status: draft package prepared; verification results below do not approve the ADRs or accept DEV-004. Owner role: Solution Architect; coordinator owns independent review and publication. No runtime implementation is delivered by this task. Existing task instructions above are preserved.

Baseline: [active source pointer](../sources/active.json), `poseidon-v46.3-20261030`, candidate/unverified. Exact backlog locator: `analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`, sheet `Backlog`, row 5: DEV-004 / T-S0-004, DEV-001 dependency, 5 SP. The source requires **approved** ADRs; this draft does not meet that approval gate and adds no accepted SP.

| Acceptance case | Review evidence | Result |
|---|---|---|
| S0-ADR-01-AC1: explicit module boundaries and dependencies | [Module ADR](../decisions/s0-module-boundaries.md): ownership table; inward compile-time references; runtime flow; no domain DbContext or cross-module direct writes; alternatives and implementation gaps | Documented; architecture approval OPEN |
| S0-ADR-01-AC2: events, outbox and storage | [Events/storage ADR](../decisions/s0-events-storage.md): proposed atomic outbox, at-least-once/deduplication, relational metadata/object bytes, deployment reconciliation, consequences and future failure tests | Documented; outbox/object adapters unimplemented |
| S0-ADR-01-AC3: offline and AI authority | [Offline/AI ADR](../decisions/s0-offline-ai.md): authorized idempotent replay, stale/material conflicts, no automatic permit authority, advisory retrieval with tenant authorization and citations | Documented; offline/product AI unimplemented |
| S0-ADR-01-AC4: unresolved decisions and approvals | All ADRs retain D-STATE-01, IdP, data location and expert/legal/operational approvals OPEN with reviewer/date/evidence fields | No approval invented; DEV-004 not accepted |
| S0-ADR-01-AC5: links and canonical accounting | Repeatable check below resolves relative file links and compares status/phase documents to HEAD | Results recorded below; accepted total remains 8/970 SP, 2/121 tasks |

Source retrieval was bounded, local and made no model calls: `py scripts/query_sources.py "DEV-004" --limit 2`, `"modular" --limit 4`, `"outbox" --limit 4`, `"object storage" --limit 3`, `"offline sync" --limit 3`, `"D-STATE-01" --limit 2`, and `"AI" --limit 3` (same script/options). Initial `14.01`, `15.01`, `23.03` lookups located register entries; focused term queries then located the technical sections. Cited DOCX paragraph ranges were read directly using the importer's paragraph locators. The corpus was not loaded into model context. 26.01 embedded runtime and historical UAT were not treated as implementation/test evidence.

The available coordination skill was applied for traceability and approval separation. Per the trusted task, no delegation or external publication was performed. Graph MCP tools were unavailable and this worktree has no `graphify-out/graph.json`; implementation evidence used targeted backend file reads. No code changed, so no graph update was required.

## Repeatable documentation checks

Run this Python block from the worktree root (for example, pipe it to `py -` in PowerShell). It checks document targets and edit boundaries; semantic/approval review remains independent.

```python
from pathlib import Path
import re
import subprocess

allowed = {
    "docs/decisions/s0-module-boundaries.md",
    "docs/decisions/s0-events-storage.md",
    "docs/decisions/s0-offline-ai.md",
    "docs/tasks/s0-adr-01.md",
}
links = 0
for name in sorted(allowed):
    path = Path(name)
    text = path.read_text(encoding="utf-8")
    assert all(line == line.rstrip() for line in text.splitlines()), name
    for target in re.findall(r"\]\(([^)]+)\)", text):
        if "://" in target or target.startswith("#"):
            continue
        assert (path.parent / target.split("#")[0]).is_file(), (name, target)
        links += 1
changed = set(subprocess.check_output(
    ["git", "diff", "--name-only", "HEAD"], text=True).splitlines())
untracked = set(subprocess.check_output(
    ["git", "ls-files", "--others", "--exclude-standard"], text=True).splitlines())
assert changed | untracked <= allowed, changed | untracked
task = "docs/tasks/s0-adr-01.md"
assert Path(task).read_bytes().startswith(subprocess.check_output(
    ["git", "show", "HEAD:" + task])), "Original task content must be preserved"
for name in ("docs/project-status.md", "docs/project-status.json"):
    assert Path(name).read_bytes() == subprocess.check_output(
        ["git", "show", "HEAD:" + name]), name
assert not subprocess.check_output(["git", "diff", "HEAD", "--", "docs/phases/"])
subprocess.run(["git", "diff", "--check"], check=True)
print(f"PASS: {len(allowed)} documents; {links} relative links; scope and canonical accounting unchanged")
```

## Verification and remaining gate

Verification results are local to this uncommitted documentation package. Required repository checks in [the workflow](../../.github/workflows/backend-foundation.yml) and [architecture test contract](../architecture.md) remain required; local success is not independent QA or remote CI acceptance. No missing or skipped suite can be counted as passing.

| Local check | Exact result |
|---|---|
| Documentation block above; `git diff --check` | PASS: four authorized files, 38 relative links, original task content preserved, no whitespace errors; status/phase files unchanged |
| `py scripts/verify_sources.py` | PASS: 269 files, 34,958 chunks, 0 mismatches; verifies lineage without loading corpus into model context |
| `py -m unittest discover -s scripts -p 'test_*.py' -v` | PASS: 94 tests; no failures/errors/skips |
| `py -m unittest discover -s scripts -p windows_scheduler_checks.py -v` | PASS: 1 test; scheduler preview only |
| `py scripts/windows_nightly_checks.py -v` | PASS: 6 tests; construction/preview only, no registration |
| `dotnet tool restore`; `dotnet restore Poseidon.slnx --locked-mode`; `dotnet build Poseidon.slnx --configuration Release --no-restore` | PASS: tool/locked restore; Release build, 0 warnings, 0 errors |
| `dotnet test --project tests/Poseidon.Persistence.Tests/Poseidon.Persistence.Tests.csproj --configuration Release --no-build --filter-not-trait "Category=Integration"` | PASS: 12 tests, 0 skipped |
| `dotnet test --project tests/Poseidon.Domain.Tests/Poseidon.Domain.Tests.csproj --configuration Release --no-build` | PASS: 16 tests, 0 skipped |
| `dotnet test --project tests/Poseidon.Persistence.Tests/Poseidon.Persistence.Tests.csproj --configuration Release --no-build --filter-trait "Category=Integration"` | PASS: 10 real PostgreSQL tests, 0 skipped |
| `dotnet test --project tests/Poseidon.Api.Tests/Poseidon.Api.Tests.csproj --configuration Release --no-build --filter-trait "Category=Integration"` | PASS: 5 API integration tests, 0 skipped |
| `dotnet ef migrations has-pending-model-changes --project backend/Poseidon.Persistence --startup-project backend/Poseidon.DbMigrator --configuration Release --no-build` | PASS: no pending model changes |
| `dotnet publish backend/Poseidon.Api --configuration Release --no-build --no-restore --output .runtime/artifacts/api` | PASS: local build artifact only; no external publication/deployment |
| `npm ci --prefix frontend`; `npm ci --prefix tests/Poseidon.Browser.Tests` | PASS: locked installs; frontend audit reports 13 moderate advisories, browser audit reports 0. Dependency remediation remains outside this documentation scope |
| `npm run generate:api --prefix frontend`; `git diff --exit-code -- frontend/src/generated/api.d.ts` | PASS: generated contract unchanged |
| `npm run lint --prefix frontend`; `npm run typecheck --prefix frontend`; `npm test --prefix frontend`; `npm run build --prefix frontend` | PASS: lint/typecheck/build; 12 component tests, no skips |
| `npx playwright install chromium` in browser test directory; `npm test --prefix tests/Poseidon.Browser.Tests` with API/UI ports 59321/59322 | PASS: 4 desktop/mobile real-API browser smoke tests, 0 failed/skipped |

Observed tooling warning: npm reports an existing unknown user config `always-auth`; it did not fail the checks. No dependency/configuration files were changed. Existing frontend audit advisories remain unresolved; this package does not claim security/release acceptance.

Approval blockers remain D-STATE-01, IdP, data location and expert/legal/operational decisions. Next step: coordinator obtains independent review and the required approvals/check evidence before considering DEV-004 acceptance or publication. Canonical project-status and phase files remain untouched by this task.
