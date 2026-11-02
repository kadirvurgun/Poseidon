# ENTERPRISE-CREW-WORKSPACE-01

Status: authored foundation UI continuation; all execution and acceptance DEFERRED.
Baseline: `poseidon-v46.3-20261030`. This package changes existing Crew presentation, not canonical task or screen acceptance.

## Source and scope

Bounded receipts are in [the source register](enterprise-crew-workspace-01.sources.json).
13.04 paragraphs 19, 21, 23–25, 34, 36, 42, 105, 108 and 111–114 govern role-aware actions, exception visibility, progressive disclosure, server pagination, TR/EN, keyboard and tablet layout.
13.02 paragraphs 1960–1967 identify the existing roster/profile screens; their field/action counts are source coverage targets, not implementation acceptance.
14.02 Data Dictionary 2316 rows 1572–1583 and 1615 retain vessel/person authority, masked identity, assignment period, contract/watch declarations and the unresolved source-defined profile status derivation.
The bounded crew-roster/crew-profile records in the 12.01 prototype were inspected for journey identity; prototype runtime and archived tests are not current implementation evidence.

Existing owner tasks and backend contracts remain authoritative. No new catalogs, readiness engine, safety decision, qualification result, privacy grant or offline support is created.

## Authored behavior and acceptance mapping

| Trace | Required behavior | Authored evidence |
|---|---|---|
| ECW-01 / FR46-crew-roster-vessel_id | Actual returned page summaries distinguish listed assignments, unverified declarations, unknown/stale context and incomplete drafts. Never imply vessel totals or minimum manning acceptance. | CrewEnterpriseRoster; component summary test |
| ECW-02 / FR46-crew-roster-assignment_status | Page-only state/department refinements are explicit; no-match results do not imply absence across the vessel. Server paging retains 25-row offsets; filters reset when scope/page/generation changes. | Roster refinement, page/lock tests |
| ECW-03 / FR46-crew-roster-person_id | Withheld identities stay withheld; profile links pass through the existing dirty navigation guard. No photos, fabricated identities, initials or personal search index are added. | Roster guarded-link test; retained CrewWorkspace isolation tests |
| ECW-04 / FR46-crew-roster-sign_on_date / FR46-crew-roster-contract_end_date / FR46-crew-roster-watch_group | Profile particulars display actual nullable assignment values. Qualification and protected contact/health placeholders remain explicitly Unknown/Withheld in a separate disclosure. | CrewEnterpriseProfileFields; profile disclosure test |
| ECW-05 / FR46-crew-profile-status | Preserve unresolved status specification and current/ambiguous/stale assignment explanations. No derived readiness or legal eligibility is invented. | Existing CrewWorkspace conditions and source status notice retained |
| ECW-06 / 13.04 accessibility | Shared enterprise primary action, associated labels, native keyboard disclosures, scoped table caption, responsive core identity/rank/state columns, coarse-pointer 44px controls and TR/EN labels. Full profile remains reachable on narrow screens. | Component keyboard/click/Turkish/empty-state sources; CSS authored |
| ECW-07 / existing draft command contracts | Existing capability/policy gates, actor refresh withdrawal, uncertain retry UUID/key/body, conflict handling, dirty child locks and controlled offline path remain unchanged. New refinements and paging lock during dirty work. | Retained CrewWorkspace tests plus new page/lock test |

## Exact execution status

- New meaningful component tests: [CrewEnterpriseRoster.test.tsx](../../frontend/src/product/CrewEnterpriseRoster.test.tsx), six authored cases.
- Existing privacy, malformed-contract, revoke, uncertain command, conversion, conflict and offline tests: [CrewWorkspace.test.tsx](../../frontend/src/product/CrewWorkspace.test.tsx), retained. One additional integration case explicitly opens the assignment/identity disclosures, consumes the strict historical-policy contract, and confirms identity history stays on demand. The controlled-position option assertion is scoped to its own select so page-filter options cannot affect it.
- Existing registered real-host journeys: [crew-cases.ts](../../tests/Poseidon.Browser.Tests/register-specs/crew-cases.ts). The assignment-history journey now opens its native summary before visibility assertions; roster/profile creation additionally opens the qualification disclosure and verifies Unknown eligibility/Withheld contact against the actual saved profile flow. Existing real API commands and revision assertions remain. No route mocking, permission fallback or registry changes were introduced.
- Test suites, lint, typecheck, build, browser journeys and independent QA: **DEFERRED; not run by this agent**, following the user’s larger-bundle cadence.
- Source and local implementation reads were used for authoring. They are not passing verification results.
- Shared graph/status update and any single requested preview build belong to the coordinating batch; no local execution result is claimed here.
- No push, commit, PR, merge or publication.

## Remaining limits

The current API exposes server paging, not vessel-wide state/department search; refinements intentionally apply to the returned page. Profile status derivation and several qualification owners still require controlled specifications. Full 16/28-field and 8/10-action screen acceptance is not claimed. Responsive rendering, keyboard behavior and existing backend journeys need the integrated gate and independent target-commit QA. Existing production IAM/privacy/retention and safety authority gates remain open.
