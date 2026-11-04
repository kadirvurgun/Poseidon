# S2 enterprise shell — authored implementation package

Status: COMBINED GATE OPEN / FAILURES DIAGNOSED / REVALIDATION PENDING. No S2 story points or canonical
screens are accepted by this document. A local checkpoint is not a phase gate.

## Controlled sources and dependencies

Baseline: `poseidon-v46.3-20261030`, selected through
[active source register](../sources/active.json).

Source: `analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`,
SHA-256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.

| Task / trace | Original locator | Acceptance criterion |
|---|---|---|
| DEV-021 / T-S2-002 / 5 SP | Backlog row 22 | Context topbar görünür; vessel switch authorization check yapar; unsaved critical form context change'i uyarır. |
| DEV-022 / T-S2-003 / 8 SP | Backlog row 23 | Role lens ile sade menü gelir; Full product search authorized power users'a açıktır; 172 ekran düz menü olarak gösterilmez. |
| DEV-023 / T-S2-004 / 8 SP | Backlog row 24 | Exception cards priority/source/Why/owner/next action taşır; loading/empty/error/offline states deterministiktir. |
| DEV-025 / T-S2-006 / 5 SP | Backlog row 26 | Keyboard focus, contrast, status text/icon, responsive breakpoints ve form error semantics temel shell'de PASS. |

Dependency order remains DEV-015 + DEV-020 → DEV-021 → DEV-022 → DEV-023;
DEV-025 depends on DEV-022. Existing S1 evidence supports the identity foundation;
DEV-020's current profile/context contracts were inspected and coordinated with
the backend author. S2 dependencies are authored, not independently accepted.

Design authority: `13.04_POSEIDON_v46.3_Design_System_ve_UX_Standartlari_TR.docx`,
SHA-256 `83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c`,
paragraphs 19–25 (role/mode, exception handling, Why, human authority), 29–42
(global context, grouped navigation, responsive layout), 110–125 (language,
text/icon status, focus, contrast, role clarity, Why). The relevant screen
registry of `12.01_POSEIDON_v46.3_TR_EN_Tam_Calisir_Tiklanabilir_Prototip.html`
was read as UI intent, never as implemented or passing runtime evidence.

## Delivered behavior

- The global context topbar opens an accessible vessel chooser with server search
  and bounded pagination. A returned list row never becomes a switch grant: the
  selected vessel receives a fresh, strictly tenant/actor/vessel-bound context
  read before the existing workspace navigation/unsaved-draft guard receives
  the destination. Compatible workspace families are preserved; old record and
  job IDs are discarded. Denied, malformed, offline, hidden, expired and late
  reads cannot commit a context switch. The chooser remains available after
  cancelling the native discard confirmation and closes only after navigation
  commits.
- The first genuine role lens is visible on entry. Selecting another assigned
  lens replaces the primary sidebar's bounded daily/review destinations, including
  crew, equipment, maintenance and import. Empty lenses remain empty; actor,
  vessel and revalidation scope changes withdraw the old projection. This is a
  presentation choice and never grants full product search: the separate current
  workspace capability still controls its trigger and shortcut.
- Initial offline/hidden context now has an explicit state instead of indefinite
  loading. Mobile styling keeps role context visible. Sidebar focus has a visible
  high-contrast indicator; priority and connection status include icons and text;
  data-state validation errors describe both the value and its enabled state
  selector.
- Existing Today priority/source/owner/action, retry, empty, offline and scoped Why
  behavior is retained. An unavailable owning regulatory profile has explicit
  unavailable coverage and no revealing draft card. A saved incomplete draft with
  `Missing` provenance remains `DraftUnverified`, not absent or ready.
- The coordinated additive `Why.source` contract binds source kind, ID and
  revision to the actual selected vessel draft, maintenance plan or tug request.
  Disclosure shows the exact authorized source lineage with existing freshness
  and blockers. Older payloads without this additive field remain readable.
- The regulatory profile shows its saved observation timestamp and explicitly
  unverified freshness; an unsaved timestamp cannot replace saved source context.

## Authored verification and remaining gate

Meaningful component/contract regressions cover fresh authorization before guarded
navigation, 403/404/invalid projection, late actor grants, offline withdrawal,
bounded search, unsafe target IDs, compatible workspace destinations, primary
role replacement, search denial, empty lenses, source-kind/ID/revision mismatch,
owning-source redaction, incomplete-draft provenance, saved-source freshness,
keyboard focus and validation descriptions. Existing mobile focus and Today
maintenance/tug lineage tests were extended. The coordinator separately owns
real-browser shell scenarios and updates the existing signed role-lens journey.

At the initial authoring checkpoint, frontend tests, lint, typecheck, builds,
browser journeys and OpenAPI/type generation were **DEFERRED** until the combined
S2 bundle gate, following the user's cadence. No live preview or provider fixture
was rebuilt or changed by this UI author.
`git diff --check` is an authoring whitespace check only; it is not test evidence.
Graphify refresh is delegated to the coordinator after integration.

Remaining before acceptance: integrate the matching backend source contract,
regenerate the actual API contract/types at the bundle gate, execute the required
frontend/backend/browser/security checks, obtain independent target-commit QA,
and record exact native results. Responsive contrast and keyboard behavior are
authored here and must be measured/observed at that gate; no PASS is claimed.

## User-directed correction of existing screens

The user's subsequent instruction explicitly calls for correcting existing page
and button layouts using consistent UI patterns. This work therefore extends the
S2 accessibility/shell package into the already implemented core workspaces; it
does not add new accepted tasks or story points.

Concrete source findings: the old directory search and vessel-type filter occupied
unrelated rows; detail actions and three workspace links were scattered across
the page; directory labels, values and error text were separate grid children;
legacy primary-button margins offset Save from Cancel; technical and crew forms
placed native Save/Discard controls directly after fieldsets without a shared
action layout. Broad crew navigation and technical header/fieldset selectors also
reached the enterprise chrome or overrode its intended composition.

[EnterprisePatterns](../../frontend/src/product/EnterprisePatterns.tsx) now provides
explicit page/section headers, search/filter toolbars, semantic action groups,
field grids and pagination. Patterns use the existing shadcn Button, product
tokens, consistent control sizing and responsive wrapping without `!important`
or new dependencies. Labels and event handlers are retained. Cancel/Discard is
an explicit non-submit action; the save action follows it in keyboard order.

Applied to existing [fleet/company pages](../../frontend/src/product/ProductApp.tsx),
[directory editor](../../frontend/src/product/DirectoryEditor.tsx),
[regulatory profile](../../frontend/src/product/RegulatoryProfile.tsx),
[Today](../../frontend/src/product/TodayWorkspace.tsx),
[technical workspace](../../frontend/src/product/TechnicalWorkspace.tsx),
[crew workspace](../../frontend/src/product/CrewWorkspace.tsx) and the shared
[discard confirmation](../../frontend/src/product/DraftConfirmation.tsx). Fleet
search/filter/actions share a toolbar; detail actions and workspace links are
grouped; technical mode navigation is a compact segmented group rather than two
controls spread across the page. Legacy CSS selectors are narrowed at their
source so they do not style shared chrome or replace the explicit form grid.

Additional component regressions assert TR/EN keyboard order, cancellation without
HTTP submission, validation descriptions and removal of resolved error references.
The coordinator owns actual desktop/mobile layout and keyboard browser checks.
These checks, builds, lint and typechecks were **DEFERRED** at that authoring
checkpoint. Subsequent actual gate results are recorded below; the live user
preview and its configuration remain untouched by this UI author.

## First combined-gate diagnosis

The coordinator subsequently opened the combined S2 gate on exact commit
`5e40f18e4c5496f2012e8487de99c6d52d1c58ce`, tree
`31765948ebcfc8a17b94df7a6ae1ccca3bdcbd31`. Its actual first frontend production
build failed with `TS2322` at the bilingual role-lens test helper and five
`TS18048` diagnostics in the saved profile observation display. Lint failed with
`react-hooks/refs` for assigning the chooser's active request binding during
render. The issued native results remain attached to that original target.

The authored repair binds the test helper to the real component props type,
checks the optional saved observation before displaying it, and moves request
binding into a layout effect with synchronous cleanup/abort on scope changes.
The actor/epoch guard and fresh authorization before guarded navigation remain
in place. No type assertion, lint suppression or acceptance threshold is used
to hide the failures. Retesting belongs to the coordinator on the subsequent
frozen integration commit; this repair is not a passing gate result.

The first whole component invocation exited 1 after 169.676 seconds without its
structured reporter result: `FAILED_OR_UNPROVEN / structured_result_missing`.
No total, failed-test name or full-suite acceptance is inferred from that missing
result. The coordinator's subsequent closed lifecycle diagnostics on the same
old target passed 14/14 chooser tests and 79/79 tests across the role-lens/profile
files, with zero failures, pending cases, unhandled errors or module errors.
Those are narrowly scoped diagnostic results, not acceptance of the full frontend
or of this later repair commit.

## Closed component diagnosis and focused-form correction

The complete native component run on commit
`506d8269f4b864d862a72a89f11984048d41f87c`, tree
`1736bdd2d9dbba6d282593f57b44e2c8c27b2c16`, subsequently closed with **1,010
tests / 1,003 passed / 7 failed**, zero skipped, pending, todo, unhandled errors or
module errors. Receipt SHA-256:
`1add121aa128f4872670da308aed9c28ed96f9886711b906ee4afc9b450f2054`.
These failures are retained as real results on their original target.

The closed diagnosis identifies five obsolete secondary-navigation selectors in
the ProductApp, TodayWorkspace and Workspace journeys, one obsolete secondary
tug label in ProductTugNavigation, and one collapsed-role-disclosure/last-focus
assumption in Workspace. Tests now target the actual authorized primary POSEIDON
sidebar, its unchanged destination actions and the fixture's real last tabbable
item. Dirty values, Back/Forward, cancellation/discard, unknown-save freezing,
CSRF, retry and focus-wrap assertions remain; visible role-first controls and
no-navigation assertions are added. No assertion is skipped or relaxed to accept
an unrelated result.

The coordinator's closed Chromium desktop/mobile shell run passed 8/8 scenarios
on that older source. This author inspected both issued form PNGs: button and
field alignment was improved, but browse KPI/chart content still pushed the edit
form far down the page. The fixed chrome appearing mid-page was a scrolled
full-page capture artifact and was not treated as a CSS defect.

In the new authored form mode, editing or opening a regulatory profile suppresses
the browse overview, search/filter toolbar, record table, unrelated details and
workspace links. The form follows the page header and compact necessary context.
The original guarded Switch vessel action and sign-out access remain available;
profile mode also provides a guarded Back to vessel action. Normal browse mode
keeps its overview and list. Existing save/cancel, selected-record and URL/history
handlers are retained. Profile-access loading/denial stays explicit instead of
being hidden with the browse detail panel.

Additional component coverage checks that dirty-edit cancellation preserves the
form, explicit discard restores the same selected browse context/URL, profile
mode excludes browse content, and sign-out remains available. No test, build,
browser journey or runtime mutation was run by this UI author for this update.
The subsequent integration commit requires actual complete regression and fresh
desktop/mobile viewport screenshots before acceptance.
