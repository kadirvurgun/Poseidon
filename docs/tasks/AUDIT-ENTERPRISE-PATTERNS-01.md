# AUDIT-ENTERPRISE-PATTERNS-01 — shared audit presentation

Status: **AUTHORED / new execution DEFERRED / acceptance false**. User requested
professional shared controls and correction of existing inconsistent pages.

Active `poseidon-v46.3-20261030`: 18.01 Backlog17 DEV-016 / T-S1-004,
[AUDIT-TRACE-01](audit-trace-01.md) AT-07, 13.04 paragraphs19–25/30–40/94–95/105.
No additional accepted story points or independent screen acceptance.

The audit workspace now composes the existing shared Button, EnterpriseFormGrid,
EnterpriseActions, EnterpriseSectionHeader and EnterprisePagination. Filters sit
in a responsive panel, clear/apply actions have explicit form semantics, record
actions wrap long identifiers, and read-only metadata uses the shared summary
grid. Exact revision navigation remains a real authorized anchor. Existing
localized labels are reused across all catalogs; decorative icons are hidden
from assistive technology. Scoped styles do not change the global sidebar.

Acceptance requires keyboard Enter to submit actual filter values, Clear to
navigate independently without submitting, retained query/cursor/back-forward
and immutable revision behavior, and unchanged unavailable/denied/private-data
guards. One meaningful keyboard regression is authored beside the preserved
[AuditWorkspace tests](../../frontend/src/product/AuditWorkspace.test.tsx).

Tests/build/browser and visual/contrast/breakpoint checks remain DEFERRED to the
combined gate. Source use of shared patterns is not proof of measured focus
contrast, pixel layout or final S2 acceptance. Backend permissions, DTO parsing,
data fetching, audit persistence and exact safe metadata are unchanged.
