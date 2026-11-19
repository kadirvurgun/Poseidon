# S2 sprint gate — independent QA review (55d5e1f)

- Target: origin/main `55d5e1fc9627f18ce2209e4fa732efe4c72ac15c` (merge of PR #39)
- Reviewer: independent QA subagent (did not author S2 code), own detached worktree, 19.11.2026
- CI evidence: GitHub Actions run `44510977746` (push, 55d5e1f) — **success**; jobs persistence-and-security, frontend-and-browser, windows-automation, nightly-installer-construction all green (Domain/API/Persistence unit, API real-PG integration, vitest, full Playwright specs desktop+mobile via browser_gate.py, lint/typecheck).
- Local tests run by reviewer: none (CI is the pass/fail evidence).
- Canonical criteria: 18.01 backlog rows 21–26 (`sheet:Backlog/row:21..26`, source sha256 09890162…4674).
- i18n: 17 catalogs, identical 3319 keys; enforced by `frontend/src/i18n/messages.test.ts`.

## Verdicts

| Task | SP | Verdict | AC test evidence (main 55d5e1f) |
|---|---|---|---|
| DEV-020 Vessel master & regulatory profile | 8 | ACCEPT-WITH-NOTES | `tests/Poseidon.Api.Tests/VesselProfileFieldTests.cs:34,57,91,119,142,178,195,223`; `VesselProfileFieldPolicyTests.cs:18,76,83,98,110,119`; PG `VesselProfileFieldSourcePersistenceTests.cs:59,84,113,132,152,172`; UI `frontend/src/product/ProfileFieldProvenance.test.tsx:39–235` |
| DEV-021 Global vessel/role context | 5 | ACCEPT-WITH-NOTES | `WorkspaceContextTests.cs:141,152,165,175,189,203`; `WorkspaceContextBar.test.tsx:129,135`; `WorkspaceVesselSwitcher.test.tsx:83,95`; `ContextSwitchGuard.test.tsx:48`; browser `s2-shell.spec.ts:106,133` |
| DEV-022 Role-first navigation shell | 8 | ACCEPT-WITH-NOTES | `WorkspaceMenuAuthorizationTests.cs:60,86,105,133,172,189,216,232,252`; `EnterpriseSidebarNav.test.tsx:56–137`; `EnterpriseRoleLens.test.tsx:27,49`; `WorkspaceSearchBoundary.test.tsx`; browser `s2-shell.spec.ts:193,246,298` |
| DEV-023 POSEIDON Today shell | 8 | ACCEPT-WITH-NOTES | `TodayItemContractTests.cs:18,48,78,96,114,128`; `TodayViewStatePolicyTests.cs:17,24,28`; `TodayItemStates.test.tsx:27–167`; browser `today.spec.ts:154` |
| DEV-024 Why Engine base contract | 8 | ACCEPT-WITH-NOTES | `CriticalStateTests.cs:29,63,80,99,143,185,225,262`; `CriticalStatePolicyTests.cs:21–130`; `CriticalStatePanel.test.tsx:30–136`; `critical-state-reasons.test.ts:28` (41 reason codes × 17 locales) |
| DEV-025 UI accessibility baseline | 5 | ACCEPT-WITH-NOTES | browser `s2-accessibility.spec.ts:107,179,194,248,261`; `shell-contrast.test.ts`; `a11y-patterns.test.tsx`; `DirectoryWorkspace.test.tsx`; `ThemeBuilder.a11y.test.tsx`; `WorkspaceContextSummary.test.tsx`; `s2-axe.spec.ts:91–124` |

Overall: **S2 ACCEPT-WITH-NOTES — 6 tasks / 42 SP. Blocking findings: none.**

## Non-blocking notes (open)
- DEV-020: no 54.01 freshness policy registry → production freshness is UNKNOWN (never FRESH/STALE); exact `fleet.*` actions, `methodCode`/evidence link and publication state absent; audit-history UI does not list the new entity type.
- DEV-021: no role-switch command (source: roles come from membership).
- DEV-022: direct URLs to separately routed workspaces still show their title (server denies); 54.02 `effective-menu` not implemented; server-side Turkish İ folding depends on PG collation.
- DEV-023: viewState always Partial until safety/permit/evidence owners are connected (UI states this honestly).
- DEV-024: Today Why panels do not yet consume `CriticalStateExplanationView` (AC says "can"); no WhyNode tree (60.03); 15 added locales lack linguist review.
- DEV-025: axe scans one route (`/app/today/{id}`, 5 variants) against a `page.route` mock; no dedicated status text/icon AC (covered by DEV-023/024); virtual-cursor double reading of the collapsed context summary pending a real assistive-technology test.

## Gates not claimed
No pilot, SME, linguist or assistive-technology sign-off exists or is claimed. This is a software acceptance of the canonical S2 backlog criteria, not a WCAG certification or operational safety approval.
