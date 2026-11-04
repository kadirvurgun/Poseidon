# S4 offline and service-observation UI

Status: SOURCE AUTHORING CHECKPOINT / NOT VALIDATED. Accepted story points: **0**.
This is a next-phase package in a separate author worktree. It does not accept S4,
complete its chaos gate, or change the frozen S2/S3 integration/runtime evidence.

## Controlled requirements and scope

Baseline: `poseidon-v46.3-20261030`, read through
[active sources](../sources/active.json). Original source:
`analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx`,
SHA-256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Backlog rows 34–39 were read directly after bounded local retrieval; original
Not Started statuses remain unchanged. The full source scope is six tasks/58 SP.

| Task / trace | Locator | Original acceptance criterion |
|---|---|---|
| DEV-033 / T-S4-001 | Backlog row 34 | Client UUID/base version/timestamp ile queue restart sonrası korunur; encrypted local storage; pending state görünür. |
| DEV-034 / T-S4-002 | Backlog row 35 | Idempotency key + expected version; retry duplicate critical state yaratmaz; server authoritative result döner. |
| DEV-035 / T-S4-003 | Backlog row 36 | High-risk conflict auto overwrite olmaz; before/local/server context gösterilir; human resolution audit edilir. |
| DEV-036 / T-S4-004 | Backlog row 37 | Severity/due/state trigger; dedupe; acknowledgement/escalation audit; mandatory critical route user preference ile susturulamaz. |
| DEV-037 / T-S4-005 | Backlog row 38 | API errors/latency, sync backlog/conflict, audit writer, file service ve data freshness metrikleri görünür. |
| DEV-038 / T-S4-006 | Backlog row 39 | Network drop/reconnect, duplicate request, stale base version, attachment retry, conflict paths otomasyonda PASS. |

DEV-033 depends on DEV-021/029; DEV-035 on DEV-034/016; DEV-037 on
DEV-011/034; DEV-038 on DEV-033 through DEV-035. Source-ready dependencies are
not independently accepted by this UI package. Backend sync/notification work,
store/controller pagination and full chaos evidence are coordinated separately.

Design sources read: 13.02 Screen Catalog paragraphs 135–137 (offline conflict)
and 171–173 (system health); 13.04 Design System paragraphs 80–89 (explicit
unknown/review states), 110–114 and 124–131 (language, focus, contrast, visible
offline/conflict and responsive journeys). The graph was used for bounded code
discovery; it is not requirement authority.

## Authored product behavior

- [Crew offline panel](../../frontend/src/product/OfflineApplicationPanel.tsx) and
  [tug offline panel](../../frontend/src/product/TugOfflinePanel.tsx) use shared
  enterprise actions, form/context controls and bounded history pagination.
  [Queue presentation](../../frontend/src/product/OfflineQueueSummary.tsx) shows
  the original command UUID, base version, capture timestamp, local revision and
  explicit resolution parent. Display metadata never rewrites or supplies replay
  payloads. Domain fields, device/key identifiers and plaintext envelopes are not
  dumped into the UI. Invalid metadata stays unavailable.
- The unlocked current local context has distinct pending/unknown, material
  conflict and server-receipt counts. Locked/revoked/restarted contexts do not
  render that summary or imply a zero queue. Server receipts are explicitly
  separate from operational approval. Existing encrypted namespaces, lease
  expiry/withdrawal, explicit reauthorization, exact replay and discard gates
  remain in their existing controllers.
- [Conflict review](../../frontend/src/product/OfflineConflictReview.tsx) adds
  an optional authorized before value beside local/server/resolved values. The
  crew adapter supplies it only for its supported `watch_group` field. Tug window
  conflicts have labeled before/local/server columns. No choice is preselected;
  resolution still submits the exact conflict revision, decisions and required
  human reason. Before values are presentation data, never command fields.
- [System health workspace](../../frontend/src/product/SystemHealthWorkspace.tsx)
  adds `/app/system-health` with an explicitly selected vessel and a strict
  [coordinated health contract](../../frontend/src/product/system-health-api.ts).
  The backend requires current tenant-wide directory rights, trusted
  `system.health.read`, audit access and selected-vessel scope. API/audit/freshness
  values are tenant-attributed observations in this process since start; missing
  telemetry stays null/unavailable. Sync counts are selected-vessel persisted
  server outcomes; device backlog remains unavailable. File/scanner configuration
  is explicitly unprobed, never healthy by inference.
- Session, vessel, network, focus and visibility changes withdraw prior health
  projections before current reads. Invalid context URLs, denials, missing
  telemetry and failed transport have explicit states. The health destination is
  shown only when the current server navigation grants it; a presentation role
  lens does not confer that authority. Search and vessel-switch handling retain
  the new exact destination. No untrusted tenant/actor construction is accepted.
- Three coordinated notification command reason mappings were added to the
  frontend audit vocabulary. The subsequent supporting inbox slice is described
  below; no public audit-type expansion, scheduling or external message delivery
  is introduced.

## Authored regression sources

`S4-UI-QUEUE-01`: queue metadata privacy, malformed metadata/unknown state and
distinct counts in
[queue tests](../../frontend/src/product/OfflineQueueSummary.test.tsx).
`S4-UI-CHAOS-01`: restart hides retained metadata until explicit recovery, without
creating/replaying a command, in
[offline panel tests](../../frontend/src/product/OfflineApplicationPanel.test.tsx).
`S4-UI-CONFLICT-01`: before/local/server values with only reasoned choices sent,
in [conflict tests](../../frontend/src/product/OfflineConflictReview.test.tsx).
`S4-UI-HEALTH-01`: strict null/scoped contracts and rejection of fabricated health,
foreign scope, negative counts and malformed values in
[contract tests](../../frontend/src/product/system-health-api.test.ts).
`S4-UI-HEALTH-02`: unavailable telemetry, unauthorized context, network withdrawal
and invalid URL handling in
[workspace tests](../../frontend/src/product/SystemHealthWorkspace.test.tsx).
Server-granted destination behavior is authored in
[navigation tests](../../frontend/src/product/enterprise-navigation.test.ts).

All tests, builds, lint, typecheck, browser/lab journeys and contract generation
are **DEFERRED until actual PR preparation**, per the latest user instruction.
No screenshot, simulated service or unexecuted test is recorded as PASS. The
coordinator owns AST graph refresh after integration. Actual encryption/restart,
clock drift, duplicate delivery, stale base versions, attachment retry, attributed
conflict resolution and health-source isolation still need integrated native
evidence at that later gate. No source-only result accepts those criteria.

## Supporting in-app notification inbox slice

The user directed continued S4 authoring while the backend package was prepared.
This is supporting DEV-036 UI, with **no new task/story-point acceptance**.
Bounded retrieval was followed by direct reads of original 11.01 Traceability
Matrix `196 Ekran v46.3` row 17 (`notification-escalation`, one action, ready for
BA/dev review) and 13.02 Screen Catalog paragraphs 117–121. The actual authored
backend NotificationEndpoints and NotificationPolicy were read, including the
closed request bodies, permissions, visibility, expected-version/idempotency
receipts and mandatory-routing rules.

The backend author added actual required Inbox capability booleans
`canManage`, `canAcknowledge` and `canWritePreference`. The
[strict consumer](../../frontend/src/product/notification-inbox-api.ts) requires
those values rather than deriving write authority from a role label. The
[inbox workspace](../../frontend/src/product/NotificationWorkspace.tsx) provides
bounded current-vessel pages, severity/state/source revision/due context, source
plan navigation, explicit acknowledgement and an advisory-only preference.
Only Open/Escalated rows may be acknowledged; terminal rows remain read-only.
Mandatory/Critical routes visibly cannot be muted. Configured policy remains
unverified; unavailable policy or a failed read is not interpreted as all clear.
Delivery is explicitly in-app only. No management-refresh mutation UI is added.

Ambiguous commands retain their exact original UUID/key and body in memory;
there is no automatic mutation retry. Explicit retry uses the frozen bytes with
current authorization and CSRF. New commands use the exact server version.
Dirty preferences use the existing native discard guard before vessel/source
navigation; uncertain commands lock context changes until outcome recovery.
Network/focus/session revalidation withdraws private projections while retaining
same-account unsaved state and frozen attempts. A different actor/tenant is a
separate component scope and receives none of the former private projection.
Server-granted utility navigation/search and the existing guarded vessel chooser
include the exact new route.

Authored sources:
[contract tests](../../frontend/src/product/notification-inbox-api.test.ts) cover
foreign context, forged capability, critical-route muting, fabricated delivery or
approval, duplicate notices and exact CSRF/key/body transport.
[interaction tests](../../frontend/src/product/NotificationWorkspace.test.tsx)
cover advisory muting with mandatory routes still present, denied write
capabilities, terminal states, frozen acknowledgement retry across translation,
authorization withdrawal with an unsaved preference, and unavailable policy.
All remain **NOT RUN / DEFERRED to actual PR preparation**. No native capability,
idempotency, visual or notification-delivery acceptance is claimed by authoring.
