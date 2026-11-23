# INVITE-RATELIMIT-FOLLOWUPS — QA follow-ups of USER-ADMIN-03 and RATE-LIMIT-01 (23.11.2026)

State: AUTHORED, locally unit-verified; PostgreSQL integration tests DEFERRED to CI; independent QA PENDING. Not a
sprint/release gate. Branch `fix/invite-ratelimit-followups-20261123` (base main 494045ee). Canonical count unchanged
(114/121 tasks, 932/970 SP). No migration (model unchanged; last migration stays 20261124110002).

## Source / trigger
- USER-ADMIN-03 independent QA (invite-email) N3, N4, L2; RATE-LIMIT-01 open item "invitation create/reissue and /login
  limits need a public contract change". Security baseline: brute-force/rate limit, no secrets/PII in logs.

## Delivered behavior
- **N4 idempotency binds the recipient.** `InvitationAddressFingerprint` (`MembershipInvitationEndpoints.cs`):
  `a1:` + HMAC-SHA256(HKDF(OIDC client secret, `poseidon.invitation.address-fingerprint.v1`), normalized address), or a
  random per-process key (`p1:`) for a public client (after a restart a retry is a 409, never a second invitation).
  `InvitationDeliveryPolicy.NormalizedEmail` folds case of a valid ASCII address. Create and reissue fingerprints use it:
  same key + same mailbox (any case) replays; other address or no address = `409 idempotency_key_reused`. The address is
  never stored in receipts, audit or logs.
- **N3 sweep is an explicit decision.** `InvitationDelivery:Mode` (unset | `Disabled`). Only `Mode=Disabled` (without
  `Enabled`) finalizes due QUEUED rows as `NOT_CONFIGURED`. An instance with a missing transport leaves those rows
  untouched (payload stays encrypted) for a configured peer and logs warning 4303 at startup; any instance still cancels
  rows of closed/expired invitations. `Enabled=true` with an incomplete/unsafe configuration or unresolvable credential
  reference, an unknown `Mode`, or `Mode=Disabled` + `Enabled=true` stops startup (reason codes only). Startup logs the
  transport state and a 16-hex non-reversible configuration digest (4301) so operators can compare instances.
- **Rate limits** (RATE-LIMIT-01 `SharedRateLimiter`, keyed-HMAC partitions, nothing raw stored/logged):
  - `invitation.issue`: create + reissue share 60 / 15 min per (tenant, administrator), taken before the attempt audit,
    body parsing or any write.
  - `auth.login`: `GET /api/v1/auth/login` 60 / 5 min per client network (IPv4 address, IPv6 /64), configurable
    `RateLimiting:LoginPermitLimit` (10-100000, out of range stops startup). Behind a reverse proxy forwarded-header
    processing must be configured. The IdP still owns credential brute-force protection.
  - Refusal: `429 request_limit` + `Retry-After`; only the first refusal of a window is a SecurityEvent
    (`MembershipCommand`/`Login`, result `request_limit`). Browser navigations (`Accept: text/html`) to the login get the
    429 with a fixed page that returns to `/app?auth=request_limit`.
  - Contract: `contracts/openapi.v1.json` (429 on CreateMembership, ReissueMembershipInvitation, Login) re-exported from
    the Release host; `frontend/src/generated/api.d.ts` regenerated.
  - UI: `UserAdminWorkspace` treats 429 as a definite refusal (`code_request_limit`, never "uncertain"); `ProductApp`
    shows `signinLimited` for `?auth=request_limit`. Both keys in all 17 catalogs.
- **L2 Data Protection key ring.** `DataProtection:KeyDirectory` (absolute path; relative/blank/control characters or an
  unknown key stop startup) → `PersistKeysToFileSystem` + application name `Poseidon` for all instances
  (`DataProtectionRuntime.cs`). Invitation e-mail `Enabled` outside Development/Test without it stops startup. Directory
  access control, sharing, backup, volume encryption and key rotation operations remain operator responsibilities.

## Tests (authored)
- Unit: `InviteRateLimitFollowupUnitTests` (27: fingerprint keyed/normalized/fail-closed; N3 reason codes, credential
  reference, Mode decision, startup notices without values; digest; limiter registration, ranges, per-admin/tenant
  budget, IPv4/IPv6 partitions; OpenAPI 429; Data Protection persistence across two providers, invalid paths, fail-fast
  matrix), `InvitationDeliveryPolicyTests.Normalized_addresses_…`.
- PostgreSQL integration (CI): `DirectoryWorkspaceTests` create/reissue replay vs 409 per recipient, shared issue limit
  with one audited refusal and other-tenant budget; `InvitationDeliveryTests.Rows_queued_before_the_transport_was_disabled_end_not_configured_only_by_the_explicit_disabled_mode`;
  `FoundationApiTests.Sign_in_starts_are_rate_limited_…` (JSON + browser page, one audited refusal).
- Frontend: `UserAdminWorkspace.test.tsx` 429 reissue refused; `ProductApp.test.tsx` sign-in limit notice.

## Verification (local, 23.11.2026; no Docker/PostgreSQL started)
| Check | Result |
| --- | --- |
| `dotnet build Poseidon.slnx -c Release` | 0 warnings, 0 errors |
| `dotnet ef migrations has-pending-model-changes` | no changes |
| Domain / Persistence / API unit (`--filter-not-trait Category=Integration`) | 1674/1674, 198/198, 549/549 |
| `InviteRateLimitFollowupUnitTests` | 27/27 |
| `py scripts/export_openapi.py` + `npm run generate:api` | exported (501 paths); contract + api.d.ts regenerated |
| scripts `test_s1_audit_contract`, `test_endpoint_profiles`, `test_critical_write_coverage`, `test_i18n_untranslated` | 1, 6, 31, 4 OK |
| `npm run typecheck`, `npm run lint` | exit 0 |
| `vitest run` (full) | 3109/3109 (an earlier run under parallel host load showed 1 unidentified failure; the rerun was fully green) |
| `i18n_untranslated.py` | 0 missing / 0 extra keys in 17 catalogs |
| secret scan | reviewed scan needs the CI trust pin (`POSEIDON_SOURCE_REVIEW_SHA256`); raw scan: no finding in any changed file; CI authoritative |
| PostgreSQL integration tests | DEFERRED to CI (no Docker locally) |

## Open items
- Operator rollout: `RateLimiting:Store=Postgres` key material, `DataProtection:KeyDirectory` shared volume,
  `InvitationDelivery:Mode=Disabled` decision per fleet, forwarded headers behind a proxy.
- Login limit values are technical defaults, not an approved quota. The login 429 page loses the original returnUrl
  (returns to `/app`).
- Independent QA of this package.

## Next step
CI on push, independent security QA, then integration into the next bundle PR.
