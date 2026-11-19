# S4 sprint gate — independent QA review (afc0798)

- Target: PR #42 head `afc07983d7808bae4e0660df6ff8a1868044ab5a`, merged into main as `d88527f76698013432e306b9fbc0eea51cdf8422`.
- Reviewer: independent QA subagent (did not author S4 code), own detached worktree, 19.11.2026.
- CI evidence for afc0798 (both observed with `status: completed`; re-verified by the coordinator with `gh run list` before merge):
  - push run `44546185031` — success
  - pull_request run `44546308435` — success
- Canonical criteria: 18.01 Development Backlog rows 34–39; notifications also 58.01 PL-035/036.

## Round 1 — 85a31a9 (NOT ACCEPTED)
CI push run `44540561426` failed: 4 of 1002 PostgreSQL integration tests (`SyncConflictCenterTests.cs:72,97,107,118`, `WorkspaceMenuAuthorizationTests.cs:278`, `NotificationTests.cs:275,301`). Root cause: audited codes `SyncConflictList` (action) and `quiet_hours_incomplete|invalid|zone_unknown` (results) were not registered in `SecurityEventCodes`, so `SecurityEvent.cs:47` rejected them. DEV-035 and DEV-036 rejected; DEV-038 blocked by DEV-035.

## Round 2 — afc0798
Fix 5686f53 registers exactly those four codes; a scripted scan of every audited literal in the S4 endpoints/policies found no other unregistered code. The main merge (908a771) kept S4 endpoint profiles, S1 counts (153/144/154) and navigation intact.

| Task | SP | Verdict | Evidence / notes |
|---|---|---|---|
| DEV-033 Durable offline queue | 13 | ACCEPT-WITH-NOTES | encrypted queue, client UUID/base version/timestamp, saved only with decoded receipt (`sync-operations.ts:70-77`, `sync-operations.test.ts:66,75,81`). Notes: per-resource order enforced in UI not in `replay()`; reconciliation at unlock; data stays locked on device after logout. |
| DEV-034 Idempotent sync API | 13 | ACCEPT-WITH-NOTES | `/api/v1/sync/operations` reads only the actor's receipts and checks rights first (`SyncOperationEndpoints.cs:98-130`, `SyncOperationReconciliation.cs:60-71`); PG tests green. Note: no second-actor key-reuse test. |
| DEV-035 Material conflict center | 8 | ACCEPT-WITH-NOTES | no preselection, reason required, explicit discard confirmation, menu gated by `CanOpen`. Notes: own conflicts only; tug/daily rows point to their panels without a link; no foreign-actor or daily-only-menu test. |
| DEV-036 Notification/escalation v1 | 8 | ACCEPT-WITH-NOTES | mandatory/critical never muted (`NotificationPolicy.cs:23-25,91`); escalation recorded (`NotificationEngine.cs:143`); single-level in-app escalation satisfies row 37. |
| DEV-037 Health dashboard v1 | 8 | ACCEPT-WITH-NOTES | real computations (`SystemHealthEndpoints.cs:51-90`), Unavailable/ConfiguredUnprobed instead of invented values. Note: file service not actively probed. |
| DEV-038 Offline chaos regression pack | 8 | ACCEPT-WITH-NOTES | CHAOS-01..10 assert no data loss, no duplicate effect, no false saved, run in CI `npm test`. Notes: mocked storage/scripted server; queue reconnect outside the pack; attachment retry only online-tested. |

Overall: **S4 ACCEPT-WITH-NOTES — 6 tasks / 58 SP. Blocking findings: none.**

## Not claimed
No pilot, SME, linguist or assistive-technology sign-off. Operator activation of offline classification/retention/key settings remains an operator step.
