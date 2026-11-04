# S2 — vessel context and enterprise workspace

Status: **AUTHORING; verification DEFERRED; 0/42 SP accepted**.

The user requested continuation after S1 and deferred test execution until the
larger delivery package is ready. S1 engineering acceptance is recorded separately;
its publication is [PR #15](https://github.com/kadirvurgun/Poseidon/pull/15), whose
required checks must close before merge. S2 authoring does not imply S1 publication
or acceptance of any S2 task.

## Controlled source

Baseline: `poseidon-v46.3-20261030`, selected by
[active source configuration](../sources/active.json). Original workbook:
`analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`.
SHA256: `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Rows below were read from the hash-checked original workbook. Its immutable
`Not Started` source status is distinct from implementation progress.

| Task / trace | Locator | SP | Literal acceptance criteria | Dependencies |
|---|---|---:|---|---|
| DEV-020 / T-S2-001 | sheet:Backlog/row:21 | 8 | IMO no, flag, class, ship type, GT, build/trading/cargo context versioned kaydedilir; provenance/freshness görünür. | DEV-005, DEV-014 |
| DEV-021 / T-S2-002 | sheet:Backlog/row:22 | 5 | Context topbar görünür; vessel switch authorization check yapar; unsaved critical form context change'i uyarır. | DEV-015, DEV-020 |
| DEV-022 / T-S2-003 | sheet:Backlog/row:23 | 8 | Role lens ile sade menü gelir; Full product search authorized power users'a açıktır; 172 ekran düz menü olarak gösterilmez. | DEV-015, DEV-021 |
| DEV-023 / T-S2-004 | sheet:Backlog/row:24 | 8 | Exception cards priority/source/Why/owner/next action taşır; loading/empty/error/offline states deterministiktir. | DEV-021, DEV-022 |
| DEV-024 / T-S2-005 | sheet:Backlog/row:25 | 8 | Critical state için source/reason/input freshness/blocker listesi döner; unauthorized source detail sızmaz. | DEV-016, DEV-023 |
| DEV-025 / T-S2-006 | sheet:Backlog/row:26 | 5 | Keyboard focus, contrast, status text/icon, responsive breakpoints ve form error semantics temel shell'de PASS. | DEV-022 |

## Work package and dependency order

Authoring branches start from S1 publication candidate
`8a18a436c0bff4caaf4d1117360d0fc2fe1842bc`. Separate worktrees own backend and
frontend changes; the integration branch owns this report and browser scenarios.
Existing product code is assessed against source criteria before extending it.
DEV-020 starts from accepted DEV-005/DEV-014 behavior. Dependent UI/Why work begins
with contract preparation and follows the upstream contract; neither a contract
agreement nor a local commit is independent acceptance.

The first observed UI gaps are the absence of a vessel switch control in the global
context bar and the absence of a role-lens filter in the primary enterprise menu.
The existing Today feed already carries source, priority, owner, action and Why
disclosures. Additional changes must preserve its permission withdrawal and offline
invalidation behavior. Source coverage and unverified drafts must remain explicit;
an empty feed never means operational approval.

## Required verification at the bundle gate

All checks in this section are **DEFERRED**, with no new PASS evidence:

- Backend domain and real PostgreSQL/API integration tests for profile revision,
  provenance, incomplete/unknown fields, concurrency, audit atomicity and tenant/
  vessel/role denial; Why details must respect their owning source's permissions.
- React component tests for authorized context switching, cancellation of dirty
  forms, role-filtered navigation, denied search, context refresh/offline, accessible
  errors and focus, and Today/Why state transitions.
- Desktop/mobile Playwright journeys for keyboard operation, context switching and
  permission withdrawal. Mocked transport verifies browser behavior only; signed
  real-backend journeys remain separate evidence.
- Locked restore/build, lint/typecheck, actual host/OpenAPI consumer parity,
  migration/model parity if changed, secret scanning, and the complete relevant
  regressions at a single reviewable package gate.
- Independent QA on the final target commit, then required push/PR CI before any
  normal merge. No source acceptance totals or screen counts change during authoring.

No new dependencies, production provider choice, safety decision, verified evidence,
human appointment or pilot sign-off is authorized by this work package. The next
delivery report will identify actual changed behavior and exact executed results,
including failures and unresolved gates.
