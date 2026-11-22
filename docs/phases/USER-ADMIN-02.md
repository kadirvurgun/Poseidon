# USER-ADMIN-02 — Davet ve kabul (invitation + acceptance)

State: **AUTHORED, targeted tests PASS locally; CI and independent QA pending** (locally validated subpackage, not a
sprint or release gate). Branch `feature/user-admin-02-20261122` from `origin/integration/admin-help-bundle-20261122`
(PR #54) 23738e7a. Task contract: [docs/tasks/USER-ADMIN-02.md](../tasks/USER-ADMIN-02.md). Closes USER-ADMIN-01 QA note
N1 (cross-tenant identity squatting).

## Delivered behavior
- An admin-created membership is an invitation: `Invited`, grants nothing, its account has no subject and reserves no
  identity in any tenant. Optional expected subject restricts who may accept.
- Single-use 256-bit invitation token, shown once (create/reissue), only its SHA-256 stored; 24 h lifetime (54.01
  `account.invite_hours` default); reissue supersedes; revoke closes; expiry derived from server time.
- The identity holder signs in through the trusted IdP into a ≤10-minute invitation-only session (no actor), previews
  what the invitation grants and accepts; binding is SERIALIZABLE in the token's tenant only, exactly once.
- Uniqueness over accepted bindings only (filtered unique index); one accepted membership per identity (strict).
- Neutral `409 invitation_unavailable` for every refusal, per-identity attempt limit (`429 request_limit`), closed
  SecurityEvent codes, token/subject never in logs or audit summaries.
- UI: invitation states, one-time link with copy, reissue, revoke; `/app/accept-invitation` page (not a navigation
  destination); TR/EN copy and help guides updated.
- Migration `20261124100002_UserAdministrationInvitations`; existing provisioned rows treated as already accepted; no seed.

## Verification (22.11.2026, local, repo-local .NET SDK / Node 26, Docker PostgreSQL)
| Check | Result |
| --- | --- |
| `dotnet build Poseidon.slnx -c Release` | 0 errors, 0 warnings |
| Domain tests (all) | 1649/1649 passed |
| Persistence non-integration | 163/163 passed |
| Persistence integration (real PG): `AccountMembershipPersistenceTests` | 6/6 passed |
| Persistence integration (real PG): methods `*igration*` | 15/15 passed |
| `dotnet ef migrations has-pending-model-changes` | no changes |
| Api non-integration | 479/479 passed |
| Api, trait `USER-ADMIN-02` + `USER-ADMIN-01` (4 new PG tests + updated USER-ADMIN-01 tests + audit projection tests) | 23/23 passed |
| Api, traits `PLAN-1511-5202` + `PLAN-1511-5803` (membership lifecycle/delegation/session, endpoint profiles) | 33/33 passed |
| Api, S1 inventory/anonymous/forged/role-less probes (4), OpenAPI (2), DEV-022 label sync | 8/8 passed; OpenAPI re-run after the final description change 3/3 passed |
| `scripts` `test_s1_audit_contract`, `test_endpoint_profiles` / `test_critical*` | 7/7 OK / 31/31 OK |
| Frontend `npm run typecheck`, `npm run lint` | exit 0 |
| Frontend vitest full suite | 231 files, 2292 tests passed (run before the 24 h copy change) |
| Frontend vitest after the 24 h copy change: user-admin, accept page, `src/help`, `src/i18n` | 10 files, 179 tests passed |
| OpenAPI export + `npm run generate:api`; endpoint profile registry regenerated (`scripts/endpoint_profiles.py`) | regenerated, 613 endpoints, 0 unassigned |

Not run: full Api/Persistence integration suites (CI), Playwright/real-IdP (Keycloak) invitation login journey, axe.

## Test fixes outside the package
- `MembershipLifecycleTests.MembershipTestHandler`: `X-Membership-Invitee` stamps the invitation-session source claim.
- `UserAdministrationTests`: `Created` now invites and accepts through the API; `Provisioned` stamps access revision 1
  (an accepted invitation is at access revision 1, as the real login stamps it).
- `S1ScopeCoverageTests`: counts +3 (303 routes, 293 critical, 318 critical ids, 10 non-critical).
- Frontend `Session` type: `invitationSession` optional on the client (existing fixtures omit it).

## CI
- Run 44959072560 (9db83443, push): success.

## Independent QA on 9db83443 — ACCEPT-WITH-NOTES, fixed in one commit
- N1: invitation link uses the URL fragment (`#token=`), read and cleared before the first request; legacy `?token=`
  stripped and ignored; `Referrer-Policy: no-referrer` on every API response and as `index.html` meta (OIDC code flow
  does not use Referer).
- N2: `CK_Accounts_Closed` rewritten with `COALESCE` in the (unmerged) migration `20261124100002`; raw-SQL PG test.
- N3: pre-binding SecurityEvents carry `IdentityReference` (HMAC of issuer+subject, HKDF of the OIDC client secret) and
  `UnverifiedTenantId` (new columns in the same migration); `ActorId` stays trusted-actor only.
- N4: limiter before any SecurityEvent write, first refusal per window audited once, bounded time-bucketed eviction.
- N5: audit-store outage on preview/accept → 503.
- N6: invitation-session decision extracted to `FoundationIdentity.InvitationSession` and unit-tested (10-minute cap,
  ID-token cap, return-path validation, identity checks); the OIDC event wiring itself needs a signed ID token (open item).

Results after the fix (local): build 0/0; `has-pending-model-changes` none; Api non-integration 495/495; Api traits
USER-ADMIN-02 + USER-ADMIN-01 39/39 (incl. 16 new unit tests); PLAN-1511-5202 + 5803 33/33; S1/OpenAPI/DEV-022/status
15/15; Persistence non-integration 163/163; Persistence PG invitation + membership classes 9/9 (incl. 3 new raw-SQL CHECK
tests); Persistence `*igration*`/`*SecurityEvent*` 22/22; frontend typecheck/lint exit 0; vitest user-admin, accept page,
help, i18n, scripts 11 files / 198 tests.

## Open items
See task contract: `account.invite_hours` tenant override not wired, revoked binding stays bound (operator re-homing
procedure), no e-mail delivery, process-local limiter, no real-IdP journey test for the invitation session branch, 15
locales English fallback.

## Next step
CI on push, independent security QA, then integration into the next bundle PR.
