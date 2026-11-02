# ENTERPRISE-WORKSPACE-01

Status: **AUTHORED_UNVERIFIED / NOT ACCEPTED**. Same larger foundation branch,
`foundation/current-tug-today-20261102`, baseline `610074e`.

User direction: replace the simplistic presentation with an enterprise product
design; React, shadcn/ui and ECharts are explicitly allowed. Write meaningful
tests without running suites. A separate request authorized the builds and local
startup necessary to inspect the application; this preview exception does not
replace the later integrated gate or permit phase acceptance.

## Controlled sources and traces

Active baseline: `poseidon-v46.3-20261030`, [active source](../sources/active.json).
Bounded [original receipts](enterprise-workspace-01.sources.json) quote13.04
paragraphs19–24,29–42,104–114. The12.01 prototype supplies layout intent; its
embedded runtime is historical reference, not current implementation/tests. The
light canvas/navy navigation implement the user's design iteration. These local
trace IDs add no canonical tasks, accepted screens or SP.

| Local trace | Source locator | Acceptance criterion / authored behavior | Authored test source |
|---|---|---|---|
| EW-NAV-01 | 13.04¶19–20,31–32 | Group actual server-authorized workspace destinations; exact known route pairs only; current page visible; scope changes withdraw old menu/search | `enterprise-navigation.test.ts`, `EnterpriseChrome.test.tsx` |
| EW-CHROME-01 | 13.04¶29–36 | Shared tenant/vessel/role/language/network context across directory, Crew, Technical, Tug and Import; retain authority and action state | `EnterpriseChrome.test.tsx`; existing workspace suites deferred |
| EW-GUARD-01 | 13.04¶23,39–40 | Retain dirty/uncertain-save guards; search closes after actual navigation commit | `EnterpriseChrome.test.tsx`; existing dirty/offline suites deferred |
| EW-SUMMARY-01 | 13.04¶21–24,35–38,105 | Count only authorized returned pages; no invented global total, trend, verified readiness or safety score | `DirectoryOverview.test.tsx`, `ScopedDistribution.test.tsx` |
| EW-A11Y-01 | 13.04¶41–42,110–114 | TR/EN, focus, skip link, mobile focus/inert/Escape restoration, coarse targets, chart text fallback, reduced motion | `EnterpriseChrome.test.tsx`, `DirectoryOverview.test.tsx`; real-browser verification deferred |
| EW-RENDER-01 | 13.04¶24,36,112 | Lazy SVG ECharts with visible count legend, resize/late visibility support and old-scope disposal | `ScopedDistribution.test.tsx` |

## Implementation

Manual shadcn/ui foundations live in [owned components](../../frontend/src/components/ui/)
with Radix Slot, CVA, Tailwind4 tokens and alias/component configuration. Icons
use Lucide. Shared [chrome](../../frontend/src/product/EnterpriseChrome.tsx) and
[styles](../../frontend/src/product/enterprise.css) unify all five workspace
families. Navigation reads the existing server capability projection. Existing
role-specific tools remain under Work view.

[Directory overview](../../frontend/src/product/DirectoryOverview.tsx) counts the
loaded page and its edit permissions/pagination. [Today overview](../../frontend/src/product/TodayOverview.tsx)
counts returned alerts and source availability, preserving availability versus
verification. Existing source/Why and Crew/Tug readers retain business authority.
[ECharts renderer](../../frontend/src/product/scope-chart-renderer.ts) loads
separately, uses only returned types/counts and retains text fallback.

Primary references: [shadcn manual setup](https://ui.shadcn.com/docs/installation/manual),
[button](https://ui.shadcn.com/docs/components/button),
[ECharts modular imports](https://echarts.apache.org/handbook/en/basics/import/).
Exact pins and dependency gate: [dependency policy](../dependency-policy.md).

## Verification disposition

Preview at `https://localhost:5443/app/fleet` serves current frontend distribution
and Release API. API/DbMigrator locked restore/build, frontend TypeScript/build,
local migration/idempotent synthetic seed and strict-TLS health startup succeeded
solely for the requested preview. The API build exposed a nullable MSMD role-array
dereference; an explicit empty-role guard repaired it before the successful build.
No test suite ran.

The preview reuses the owned synthetic IdP/database and a private ignored config
copy with ordinary module read permissions for the existing demo account. Private
source/medical/role authorities remain separately enforced. This is not an
operational sign-off, production configuration or independent QA. Credential
values must not enter this report. An attempted local browser sign-in automation
did not establish success; the user reported viewing the preview manually. No
automated visual/a11y acceptance is claimed.

The main frontend chunk remains above500kB; install reported13moderate advisories,
with full dependency/security review deferred. All backend/domain/persistence/API/
React/Playwright suites, lint, generated API/EF parity, scans, independent QA and
CI remain **DEFERRED / NOT RUN**. TypeScript/build success is not test success.
The broader foundation package remains incomplete.

Continue source-supported owning workflows, fixtures/offline adapters and full
integration; execute the integrated gate before any substantial PR, then obtain
independent exact-target QA and required CI before merge. Canonical
**8/970SP,2/121tasks,0/196accepted screens**, selected **40tasks/272SP** and
conditional194target remain unchanged.
