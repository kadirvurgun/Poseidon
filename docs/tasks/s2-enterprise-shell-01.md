# S2 enterprise shell — authored implementation package

Status: AUTHORING COMPLETE / VERIFICATION DEFERRED. No S2 story points or canonical
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

All frontend tests, lint, typecheck, builds, browser journeys and OpenAPI/type
generation are **DEFERRED** until the combined S2 bundle gate, following the
user's cadence. No live preview or provider fixture was rebuilt or changed.
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
These checks, builds, lint and typechecks remain **DEFERRED**. No updated screen
has been visually accepted from a rebuilt browser runtime in this authoring
checkpoint; the live user preview and its configuration remain untouched.
