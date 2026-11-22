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
See final report / next status update (run for this push).

## Open items
See task contract: no real SMTP journey, implicit TLS unsupported, Data Protection key persistence is operator
configuration (lost keys → `FAILED/payload_unavailable`), at-least-once delivery, fixed EN/TR mail body, no bounce
processing, machine-drafted locale strings need human review.

## Next step
CI on push, independent security QA, then integration into the next bundle PR.
