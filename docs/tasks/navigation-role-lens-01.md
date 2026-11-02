# NAVIGATION-ROLE-LENS-01

Status: AUTHORED_UNVERIFIED / NOT ACCEPTED. Current phase: FOUNDATION authoring,
after the Crew legacy, offline application and Tug planning checkpoints, before
S0 closure and the single combined gate. Publishing/merge belongs to the
coordinator. No suite, build, typecheck, contract/model generation, migration,
browser, provider, independent QA or CI has run for this package.

## Reconciled baseline and source authority

Receiving HEAD `dbf86e3` (Tug draft planning/assignments) had a clean worktree.
There was no uncommitted prior-worker work to discard. Existing Crew privacy,
photo, offline, search, planning, migrations, normal support-origin integration
receipts and private configuration are retained. No additional agent was used.
Read AGENTS.md and backend/UI/coordinate skills, architecture, automation,
security, seed/dependency rules, active source baseline and the current Domain,
S0, Foundation, Crew legacy, offline, Tug, NAV, context and search task/reports.
MCP code discovery was unavailable; Graphify query preceded targeted source reads.

Active baseline: `poseidon-v46.3-20261030`, root
`analiz/POSEIDON GUNCEL 30.10.2026/`. Bounded `query_sources.py` retrieval used
DEV-021, DEV-025, Role lens and Role-first; exact indexed locators were then read.
The corpus was not dumped into context. Source authority remains candidate,
without invented production catalogs, appointments or legal/safety approvals.

| Source (relative to active root) | Exact locator / trace | Applied behavior |
|---|---|---|
| `06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`, SHA `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674` | `sheet:Backlog/row:22`: DEV-021 / T-S2-002; row23: DEV-022 / T-S2-003; row26: DEV-025 / T-S2-006 | Visible current context, guarded vessel navigation, actual role lens reduction, separate capability-gated power search, keyboard/responsive shell. Dependencies DEV-015/020/021 remain canonical gates. Historical172 wording does not authorize a196-screen menu. |
| `04_UX_PRODUCT_DESIGN/13.04_POSEIDON_v46.3_Design_System_ve_UX_Standartlari_TR.docx`, SHA `83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c` | paragraphs19/20/25/29–34/105/110–114/118–131 | Role/mode-aware daily work, no silent loss, topbar context and grouped navigation, bounded server search, shared semantic TR/EN keys, visible focus/status and responsive behavior. |
| Same Backlog | `sheet:Backlog/row:81`, DEV-080 / T-TUG-001 | Preserve Ship/Tug visibility and genuine cross-mode roles via existing server mode capabilities and actual vessel kind. Shared Crew/Equipment core is not misclassified as Ship-only. |

Selected sources provide DEV/T identifiers rather than a new canonical FR code.
FR-NRL identifiers below are explicit **task-local functional trace IDs**, not
fabricated canonical FR assignments or source acceptance. Existing NAV-D01..04,
WORKSPACE-CONTEXT-01 and WORKSPACE-SEARCH-BOUNDARY-01 decisions remain applicable;
the former capability-only shell limitation now has concrete configured software.

## Delivered software and acceptance cases to verify later

| Local FR / TC | Behavior / acceptance case | Authored coverage |
|---|---|---|
| FR-NRL-01 / TC-NRL-01 / DEV-022 | `GET /api/v1/workspace/lenses[/{vessel}]` returns only current trusted tenant-local role references resolved through the existing label catalog and explicit versioned `WorkspaceNavigation` mappings. No request-supplied role/tenant/mode/capability/lens selector is accepted. Choosing a lens sends no authorization mutation or role claim. | `WorkspaceLensPolicyTests.Configured_lens_simplifies_menu_without_granting_any_endpoint_or_search`; endpoint intersection and query-selector theories; React explicit selection/switch cases. |
| FR-NRL-02 / TC-NRL-02 / DEV-021/022 | Server projection intersects the mapping with implemented endpoint capabilities, current mode/kind and previously authorized vessel scope. Maintenance requires both maintenance and equipment reads; Tug intake requires attributable write scope. Ship context omits Tug operations; shared cross-mode role references may have both modes. Tenant/vessel filtering precedes projection; no foreign labels or totals. | Pure cross-mode/maintenance/intake tests; actual API cross-tenant/ungranted denial and real endpoint denial. Same-session API case replaces current server membership and rechecks capability, role, Ship visibility and vessel-grant withdrawal using the same authenticated client. |
| FR-NRL-03 / TC-NRL-03 / DEV-022 | Missing role assignment is Missing. Incomplete/invalid/duplicate/partial tenant-local catalogs/mappings are Unavailable. Valid mapping without current mode is configured/no lens; valid lens with no available items has explicit empty work. No implicit admin/Master/engineer/default selection. | Pure missing/invalid/duplicate/partial/mode cases, actual anonymous/configuration failure, React Missing/Unavailable/mode/empty states. |
| FR-NRL-04 / TC-NRL-04 / DEV-021/022 | No persistent navigation/search cache. Session object/actor/tenant/vessel changes hide previous projections synchronously; aborted old responses cannot restore them. Focus, visibility, network and60-second refresh withdraw projections before fresh reads. A surviving in-memory selection uses only the new server intersection; removed references clear selection. Offline labels/menu are withdrawn. Search keeps its own factual offline state and independently reauthorizes results on reconnect/focus. | React session refresh, logout/relogin, scope race, focus and search withdrawal tests; wire identity/mode/href/duplicate/state tests; new `WorkspaceSearchBoundary` focus/revocation case and retained offline/reconnect/search page decoder cases. |
| FR-NRL-05 / TC-NRL-05 / DEV-021/025 | One shared grouped TR/EN role navigation appears in ProductApp (Today/directory/profile/audit/register), Crew, Technical, Tug and Import. Native select requires explicit choice. Keyboard/focus styling,44px controls, responsive wrapping and current destination indication are authored. Product mobile drawer includes select/input in focus trap. Power-search dialog uses a portal outside the collapsed drawer and retains cancel/offline behavior. |11 new React logical cases,8 wire cases, adapted existing ProductApp/DirectoryWorkspace/Workspace form/history tests; registered five-family signed-host case including menu keyboard focus and both Ship/Tug contexts. |
| FR-NRL-06 / TC-NRL-06 / DEV-021/025 | Menu, context, brand/back and power-search navigation use the owning dirty/uncertain guards. Product search cancellation retains the dialog and draft. Crew own uncertain save stays frozen; Import cross-family navigation really leaves the importer only through its guard; Technical header guard includes observation state. No lens selection discards a form. | Existing adapted dirty Back/Forward/vessel switch/uncertain directory save tests; actual signed-host dirty cancel/discard case; retained technical/Crew/import/Tug form cases. |

Implementation owners: `backend/Poseidon.Api/WorkspaceLensEndpoints.cs` (pure
policy and actual endpoints), `WorkspaceEndpoints.cs` registration;
`frontend/src/product/workspace-lens-api.ts` (bounded validated projection),
`WorkspaceRoleNavigation.tsx`, shared revalidation hook/styles and the five real
workspaces. Existing parent search Audit/Tug routes and exact page decoder remain.
Read responses use no-store and mandatory attributed WorkspaceRead audit before
delivery. No raw role/query/body/person metadata is added to audit or errors.

This is read-only presentation over existing entities. There is no navigation
operational entity, business write, new dependency, EF model/migration or seed
change. Existing EF migrations/idempotent fixtures and audited SaveChanges remain
the business mutation path. No actor/seed/import bypass is exposed.

## Explicit configuration contract

`WorkspaceContext:RoleCatalog` remains the tenant-local trusted-reference label
resolver. `WorkspaceNavigation:Version` is a nonempty bounded presentation version.
Each `Mappings` entry has TenantId, Reference, explicit Modes (`Fleet`, `Ship`,
`Tug`) and Keys from the13 implemented destination keys. Duplicates, unknown keys,
invalid modes/version and incomplete assigned-role mapping fail closed. Mappings
contain no URL, permission grant, person assignment or safety authority. Stable
server groups/order and shared client semantic labels supply TR/EN presentation.

[Synthetic example](../examples/workspace-navigation.synthetic.json) is **not
loaded as runtime default**. At the later disposable signed-host gate the operator
must provision two genuine fixture membership RoleReferences and bind those exact
tenant-local references to labels/mappings; the example does not create membership.
The writer fixture needs its existing ordinary module grants, one accessible Ship
and Tug, and a crew-containing daily lens. Tests fail for absent configuration.
Private configuration and credentials were neither changed nor printed. Unknown
production role catalogs/appointments remain external decisions; configured safe
presentation software is implemented independently of those decisions.

## Verification and remaining gates

Authored inventory, **not execution results**:10 pure API-project policy cases,
8 actual-host/PostgreSQL cases,11 role-menu React cases,8 wire-boundary cases,
1 additional parent search focus/revocation case and3 registered real signed-host
journeys. Existing shell/form/search/context regressions are preserved and adapted,
not marked passing. Browser manifest retains personFileCases/searchCases once each
and includes navigationRoleLensCases once. No mocks are used by these new browser
journeys; the existing private strict runner suppresses sensitive artifacts.

ALL suites/build/typecheck/restore/OpenAPI-contract/EF-model generation/migration/
browser/provider/security/independent QA/CI: **DEFERRED / NOT RUN**. New DTO client
projections are handwritten and validated; actual OpenAPI/client regeneration and
consumer/model reconciliation are required at the later combined gate. Source
whitespace inspection and Graphify AST maintenance are not product verification.

Actual remaining software outside this task: full original S0 environment/ADR/
schema/trace/DoR-DoD/observability/ERP/golden criteria and the current Domain/Tug
report's broader operational authority/readiness/offline/lifecycle scope. This
package does not close those criteria or certify every UX screen. The new lens
software itself still needs actual generation, compilation, execution and
independent exact-target review; no runtime success is asserted. Immediate
disconnected server revocation knowledge is impossible; current endpoints enforce
every request and the client withdraws/revalidates on the stated lifecycle events.

Canonical8/970SP,2/121tasks,0/196accepted and the40-task/272SP candidate ledger remain
unchanged. Next dependent author continues foundation/S0 software, then coordinator
owns one combined generation/model/security/test gate, independent target-commit
QA, required CI and separate source acceptance. No publication or self-approval.
