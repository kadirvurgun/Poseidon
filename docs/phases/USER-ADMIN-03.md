# USER-ADMIN-03 — Davet e-postası (outbox) ve kiracı davet süresi

State: **AUTHORED, targeted local tests PASS; PostgreSQL integration tests DEFERRED to CI; independent QA pending**
(locally validated subpackage, not a sprint or release gate; canonical count unchanged 114/121, 932/970 SP).
Branch `feature/invite-email-20261122` from main 36d5d183. Task contract: [docs/tasks/USER-ADMIN-03.md](../tasks/USER-ADMIN-03.md).

## Delivered behavior
- Optional `deliveryEmail` on invitation create and reissue writes an outbox row (`identity.InvitationDeliveries`) in the
  same audited transaction. Recipient + token are Data Protection ciphertext bound to tenant + row, erased at the
  terminal state; the stored token hash is re-verified before sending.
- Background dispatcher (system actor `system:invitation-delivery`, audited `SystemOperation` writes, tenant-scoped
  contexts) sends via `IInvitationMailer` / SMTP (STARTTLS, password only through a secret reference). Retry 5× with
  1/2/4/8 min backoff, terminal `FAILED`; closed/expired invitations `CANCELLED`; reissue/revoke cancel queued rows.
- No transport configured → `NOT_CONFIGURED` immediately, nothing kept, copy-link flow unchanged. `SENT` is shown as
  "accepted by the mail server" only.
- Tenant invitation lifetime (54.01 `account.invite_hours`, 1–168 h, default 24): `GET/POST /api/v1/memberships/invitation-settings`,
  `membership.manage` tenant-wide, version-checked, audited + receipt; used on create and reissue.
- UI: delivery badges (list + detail), e-mail field on create/reissue, lifetime section; strings in all 17 catalogs.
- Migration `20261124110001_UserAdministrationInvitationDelivery` (2 tables, receipt action CHECK +1). No seed rows.

## Verification (22.11.2026, local, repo-local .NET SDK / Node 26; no Docker/PostgreSQL started)
| Check | Result |
| --- | --- |
| `dotnet build Poseidon.slnx -c Release` | 0 errors, 0 warnings |
| `dotnet ef migrations has-pending-model-changes` | no changes |
| Domain tests (all) | 1673/1673 passed (incl. new `InvitationDeliveryPolicyTests`) |
| Persistence non-integration | 182/182 passed (incl. new `InvitationDeliveryStorageGuardTests`) |
| Api non-integration | 502/502 passed (incl. new `InvitationDeliveryUnitTests`) |
| Api `*Every_mapped_endpoint_has_exactly_one*` (host start, endpoint profile observation) | 1/1 passed; `scripts/endpoint_profiles.py` 615 endpoints, 0 unassigned |
| OpenAPI export (`scripts/export_openapi.py`) + `npm run generate:api` | exported 501 paths / 946 schemas; api.d.ts regenerated |
| `scripts` `test_s1_audit_contract`, `test_endpoint_profiles`, `test_critical_write_coverage` | 1/1, 6/6, 31/31 OK |
| Frontend `npm run typecheck`, `npm run lint` | exit 0 |
| Frontend vitest `src/i18n`, `src/help`, `UserAdminWorkspace`, `AcceptInvitationPage` | 10 files, 959/959 passed (one earlier `HelpPanel` 5 s timeout under parallel load passed on re-run, unrelated to this change) |

DEFERRED to CI (real PostgreSQL): `InvitationDeliveryTests` (5 new API PG tests, trait `USER-ADMIN-03`), the S1
inventory/deny probes and OpenAPI tests in `S1ScopeCoverageTests`/`DiagnosticsTests` (Integration category), USER-ADMIN-01/02
regression traits. Not run: full frontend suite, Playwright/axe, a real SMTP server.

## CI
- Run 45039612661 (88bb67e0, push): failure in `persistence-and-security` / repository secret scan, rule `db-password` at
  `backend/Poseidon.Api/InvitationDelivery.cs:73` (an assignment to a local variable named after the credential matched
  the pattern; no secret value). Fixed by renaming the variable; all other jobs green.
- Run 45040746196 (a2576c08, push): failure in the same step, now at `docs/phases/USER-ADMIN-03.md:41` (this report quoted
  the flagged assignment). Reworded in the QA-fix commit; a scan of every changed file with all `secret_scan.py` rules
  shows 0 hits.
- Run 45041780566 (a7418de1, push): secret scan green; `PostgreSQL integration tests` red, 1198/1200, shard 2:
  `An_invitation_email_is_queued_…` (`InvitationDeliveryTests.cs:114`) and `Transient_failures_retry_…` (`:151`). Both
  are exact `DateTimeOffset` equality between an in-memory value (100 ns) and a PostgreSQL round-trip (microseconds);
  the extra locations :133/:162 are the methods' rethrow point after `await using` disposal. The other 7 new PG tests
  passed. Fix (test-only, within 1 ms): committed locally, **not pushed** — the branch has 3 consecutive red runs
  (AGENTS.md stop limit). Not reproducible locally without PostgreSQL (Docker is reserved for another session).

## Independent QA on 88bb67e0 — REJECT (B1), fixed
- B1 (CI secret scan `db-password` at `InvitationDelivery.cs:73`): fixed in a2576c08 (variable renamed, rule unchanged).
- M1 poison row: a row at the attempt ceiling (claim saved, finish lost) is finished `FAILED/smtp_retries_exhausted`
  instead of claimed again; row and tenant processing are isolated (one failure is logged by type and the loop
  continues); a throwing mailer is a transient attempt. Test `An_exhausted_or_faulty_row_never_stalls_the_dispatcher`.
- M2 retention: the dispatcher always runs; without a valid transport it sweeps due rows every 5 minutes to
  `NOT_CONFIGURED` (or `CANCELLED` for closed/expired invitations) and erases the payload; accepting an invitation cancels
  its queued e-mail in the acceptance transaction. Tests `Rows_queued_before_the_transport_was_disabled_…`,
  `Accepting_an_invitation_cancels_its_queued_email_…`.
- L1 message construction inside the classified try; no exception except shutdown leaves the dispatcher loop.
- L3 client `plausibleEmail` mirrors server `ValidEmail` (vitest cases). L4 tenant name put on one line, bounded
  (unit test). L5 round-robin tenant cursor (64 per run). L6 reissue racing a send: the reissue's cancellation wins
  (xmin), the late SENT write is discarded, the old token is unusable (test `A_reissue_racing_a_send_…`).
- L2 **production precondition (not built):** `AddDataProtection()` has no persisted or shared key ring (no
  `PersistKeysTo*` pattern exists in the repo). Before invitation e-mail is enabled in a multi-instance or container
  deployment, the operator must configure a persisted, shared, protected Data Protection key ring; otherwise queued rows
  fail closed as `FAILED/payload_unavailable` after a restart or on another node.
- PG test isolation: each delivery test first cancels QUEUED rows left in the shared fixture database (audited write).

## Open items
See task contract: no real SMTP journey, implicit TLS unsupported, Data Protection key persistence is operator
configuration (lost keys → `FAILED/payload_unavailable`), at-least-once delivery, fixed EN/TR mail body, no bounce
processing, machine-drafted locale strings need human review.

## Next step
CI on push, independent security QA, then integration into the next bundle PR.
