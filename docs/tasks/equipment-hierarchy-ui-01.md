# EQUIPMENT-HIERARCHY-UI-01

AUTHORED / UNVERIFIED supporting UI for DEV-052 / T-S8-001, equipment-hierarchy.
Source: active18.01 Backlog row53 and14.02 parent_equipment_id logical Asset reference,
Lookup Kaynak Çözümü row286. This component requires the complete authorized vessel
forest; it cannot turn an incomplete page or unconfirmed lookup into an approved selector.

Actual React renderer with TR/EN loading, denial, unavailable/retry, genuine empty,
invalid-structure and incomplete-name states. It validates structure before displaying
rows, uses iterative traversal and resets expansion when the caller's actor/vessel scope
key changes. It stores no persistent cache or authentication data. UI scope reset is
presentation isolation, not proof of backend authorization or device-cache security.

Single selection stays separate from focus. Keyboard arrows, Home/End, Enter/Space and
Turkish/English type-ahead are authored. Parent/group ownership is explicitly expressed
with aria-owns; flat rendering avoids recursive DOM construction. Declared levels and
sibling positions preserve the accessible hierarchy while indentation is constrained
on small viewports. Behavior was authored with the
[WAI-ARIA treeview pattern](https://www.w3.org/WAI/ARIA/apg/patterns/treeview/).
Actual browser/assistive-technology validation is still deferred; no WCAG conformance claim.

Fifteen component cases are authored, including keyboard/pointer behavior, malformed
and disconnected graphs, selection/ancestry changes, scope reset, escaped labels and
honest loading/error/empty states. No suite, build, lint, typecheck or browser was run.

The following Equipment package must wire this real component to its real authorized
API, generated contract adapter, URL/Back/Forward state and complete CRUD/version/lookup
workflow. These presentation props are not a fabricated generated API schema. No stale
rows may be supplied while authorization is unresolved. No standalone component or
mocked component case completes DEV-052 or establishes backend tenant/role isolation.
