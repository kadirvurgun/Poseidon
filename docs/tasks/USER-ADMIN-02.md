# USER-ADMIN-02 — Davet ve kabul (invitation + acceptance by the identity holder)

Derived package ID (not a canonical SP). Closes the USER-ADMIN-01 security QA note N1 (cross-tenant identity squatting):
all tenants share one IdP issuer and a database-provisioned identity (issuer+subject, global unique index) bound to the
first tenant that provisioned it, so a tenant-2 administrator could provision a tenant-1 user's subject first (that user
then landed in tenant 2 and tenant 1 got `409 identity_unavailable`). Builds on USER-ADMIN-01 (UA-D01..UA-D10) and
PLAN-1511-5202 without changing configuration (break-glass) identities.

## Source locators

| Topic | Locator |
| --- | --- |
| Account creation and invitation (ACC01), invite state DRAFT→INVITED, terminal re-invite | 52.02 Rev3 paragraph:57, paragraph:340 |
| Invitation fields: token hash, expiry/used, state, requested scopes | 52.02 Rev3 paragraph:272, paragraph:275, paragraph:278, paragraph:281 |
| Expired or used token refused; uses 54.01 `account.invite_hours` | 52.02 Rev3 paragraph:277, paragraph:389 ("Davet süresi dolmuş.") |
| Authorised re-invite cancels the old token | 52.02 Rev3 paragraph:390 |
| Token hash stored, delivery tracked separately (no e-mail delivery in this package) | 52.02 Rev3 paragraph:342 |
| `account.invite_hours`: Tenant, integer, default 24, range 1–168 | 54.01 Rev4 paragraph:10025 (registration paragraph:3231-3237) |
| Tenant Admin assigns user/role/scope with least privilege; MFA/IdP policy stays in the IdP | 13.03 §9 paragraph:102-104 |
| USER-ADMIN-01 decisions and QA note N1 | `docs/tasks/USER-ADMIN-01.md` (UA-D01..UA-D10), QA result `useradmin-qa.result.md` N1 |

Locators are 1-based paragraph indices from `py scripts/query_sources.py "davet" --limit 12` and
`py scripts/query_sources.py "account.invite_hours" --limit 6`. The 52.02/54.01 rows are `candidate-unverified`
source text, not an approved catalog.

## Common practice (research note)

Multi-tenant SaaS products that federate one IdP across many customer organisations bind a user to an organisation only
through an invitation the user redeems while signed in (for example Auth0 Organizations "invite members", Microsoft Entra
B2B invitation redemption, GitHub organisation invitations, Keycloak organization invitations): the inviter never gets to
claim an identity, the invitation is single-use, expiring and revocable, and only a hash/handle is stored server-side.
Products that allow one identity in several organisations add an organisation picker at login that lists only the
caller's own accepted memberships. These references are background from general knowledge, not re-fetched in this
session and not POSEIDON requirements; the binding requirements are the 52.02/54.01 locators above.

## Decisions

- **UA2-D01 — invitation, not provisioning.** `POST /api/v1/memberships` now creates an `Invited` membership
  (`StatusReason = AdminInvitation`, access revision 0) whose `AdminProvisioned` account has **no subject**, its grant
  revision 1 and a `Pending` `identity.MembershipInvitations` row. An Invited membership grants nothing: the session gate
  admits only Active memberships, the login locator only finds bound accounts, ActiveAdministrators counts only Active.
  The administrator may still edit the grant (new revision) or revoke before acceptance.
- **UA2-D02 — token.** `pi1.<tenant:N>.<secret>`; the secret is 32 random bytes (256 bits, base64url). Only
  `SHA-256(secret)` is stored (unique per tenant); an unsalted fast hash is sufficient for a 256-bit random secret. The
  token is returned once in the create/reissue response (`MembershipInvitationIssueDto.invitationToken`), never stored,
  logged or audited; an idempotent replay returns `invitationToken: null`. The tenant part only selects the tenant-scoped
  database context; it grants nothing and is not secret to the link holder. Lifetime: **24 hours** — the 54.01
  `account.invite_hours` default (the tenant override 1–168 h is registered in the source but not wired; open item).
  Expiry is derived from server time (`Pending` past `ExpiresAt` = `Expired`, never acceptable); no background job.
  Reissue (`POST /api/v1/memberships/{id}/invitation`, `expectedVersion` = membership version) supersedes the pending
  invitation and issues a new token (52.02 paragraph:390); revoking an Invited membership (existing `/revoke`) closes its
  pending invitation (`InvitationRevoked`). At most one pending invitation per membership (partial unique index).
- **UA2-D03 — uniqueness only over accepted bindings.** `IX_Accounts_IssuerSubject` is now unique `WHERE "Subject" IS NOT
  NULL`: an unaccepted invitation account reserves nothing, so no tenant can squat an identity. Create checks only
  tenant-local facts for an *expected* subject (operator configuration entry, an account already bound in this tenant, a
  pending unexpired invitation for the same expected subject here) and returns `409 identity_unavailable`; it never
  queries or reveals another tenant.
- **UA2-D04 — one accepted membership per identity (strictest option).** An identity may hold at most one accepted
  membership across all tenants; accepting a second tenant's invitation is refused with the neutral code. Rationale: it is
  the existing 5202/USER-ADMIN-01 model (one federated identity ↔ one account ↔ one tenant stamped into the session, no
  tenant switch anywhere in the product), so no login tenant picker, no cross-tenant session and no new enumeration
  surface is introduced. The multi-membership alternative (server-validated tenant choice at login) is recorded as a
  future product decision, not implemented. Consequence: a bound account stays bound after revocation (stable audit
  attribution, account id = actor id); moving a person to another organisation needs an operator procedure (open item).
  Enforcement: `ProvisionedIdentityLocator` (the existing login lookup, keyed only by the caller's IdP-validated
  issuer+subject) pre-checks; the filtered unique index is the authority under concurrency.
- **UA2-D05 — acceptance by the identity holder.** The holder opens `/app/accept-invitation#token=…`. The token is in the
  URL **fragment**, which browsers never send to a server, so it is not in any request line, proxy/host access log or
  Referer (QA N1 on 9db83443: the earlier `?token=` form reached the server on the first GET). On its first render,
  before any request, the page moves the token into this tab's `sessionStorage` and clears the fragment
  (`history.replaceState`), so it stays out of history and bookmarks; a legacy `?token=` query is stripped and never used.
  Every API response carries `Referrer-Policy: no-referrer` and `index.html` sets the same meta policy: no URL leaves in
  a Referer at all. The OIDC authorization-code flow does not depend on Referer (state, nonce and PKCE travel in
  parameters and cookies), so the strict policy does not affect login. The page then starts the normal OIDC login with
  `returnUrl=/app/accept-invitation`. `OnTokenValidated` issues an **invitation session** only for an
  IdP-validated identity with no operator entry and no bound account, and only when the login returns to the accept page:
  claims `sub`, `iss`, `poseidon:src=invitation`, lifetime ≤ 10 min (and ≤ token expiry). The decision is
  `FoundationIdentity.InvitationSession` (unit-tested: 10-minute cap, ID-token cap, return-path validation, configured /
  ambiguous / incomplete identities refused); `OnTokenValidated` only applies its result (QA N6). It resolves to no actor, so every
  existing policy denies it; `GET /api/v1/auth/session` returns `invitationSession: true` and a CSRF token.
  `POST /api/v1/invitations/preview` shows what the invitation grants in its tenant (tenant name, bundle codes,
  permission count, tenant-wide flags, vessel count, company scope, expiry); `POST /api/v1/invitations/accept`
  (CSRF + Idempotency-Key) binds: account subject ← validated subject (once, EF guard), membership Invited → Active
  (`InvitationAccepted`, access revision 1), invitation → Accepted, receipt `MembershipInvitationAccept` keyed by the bound
  account. Conditions: invitation Pending, unexpired, membership Invited, account unbound, issuer equal to the
  invitation's, expected subject (if any) equal (ordinal), identity not bound anywhere. Afterwards the invitation session is
  signed out; the grant applies at the next login (UA-D07). Member sessions (configured or provisioned) get 403 on
  preview/accept.
- **UA2-D06 — neutral refusals and rate limit.** Every refusal after the transport checks — unknown/malformed token,
  expired, used, superseded, revoked, wrong subject or issuer, identity already bound or configured, concurrent loser —
  is the same `409 invitation_unavailable`; the internal reason goes only to the SecurityEvent (`invitation_token_invalid`,
  `invitation_expired`, `invitation_closed`, `invitation_subject_mismatch`, `identity_unavailable`, `conflict_changed`).
  Preview and accept share a per-identity limiter (hash of issuer+subject, 10 attempts / 15 min, `429 request_limit`;
  process-local technical guard, not an approved quota). QA N4: the limiter runs before any SecurityEvent is written and
  only the first refusal of a window is audited; the table is bounded (10 000 identities) with time-bucketed eviction of
  past windows only, so a flood never resets other identities' counters (a new identity is refused while the table is full
  of current-window entries).
- **UA2-D07 — transaction and races.** Acceptance re-reads invitation, membership and account in a SERIALIZABLE
  transaction of the token's tenant; concurrent accepts of one invitation produce exactly one binding (the losers get
  40001/40P01, an xmin concurrency failure or a unique violation, classified through `PostgresFailure` → neutral 409).
  One identity accepting two tenants' invitations concurrently ends bound to exactly one (filtered unique index).
- **UA2-D08 — attribution and audit.** The S1 critical-write gate admits an actor-less request for exactly one operation,
  `MembershipInvitationAccept`, and only for an invitation session. Its EF writes are attributed to the account being
  bound (server-derived from the matched invitation, never a request value) with the invited grant's role context and the
  S1 command context; SecurityEvents (`MembershipInvitation`) carry the invitation's tenant and, on success, the account.
  QA N3: before binding, events carry a pseudonymous identity reference in the new `audit.SecurityEvents.IdentityReference`
  column, `idref:v1:<32 hex>` = truncated HMAC-SHA256(issuer+subject) keyed by an HKDF of the operator-resolved OIDC
  client secret (stable while that secret is unchanged; a public client without a secret uses a random per-process key,
  `idref:p1:`); the raw subject is never written. `ActorId` stays reserved for trusted actors (the writer still requires a
  tenant for it). When the token's tenant segment parses but the invitation is not identified, that claimed tenant goes
  to the new `UnverifiedTenantId` column (no foreign key, `TenantId` null): recording it never depends on, or reveals,
  whether that tenant exists. Both columns are accepted by `SecurityEventWriter` only for events without a trusted actor
  (reference) or tenant (unverified tenant). Added in migration `20261124100002_UserAdministrationInvitations`. An audit-store outage on preview or accept is `503 service_unavailable` (QA N5).
  Audit summaries carry closed codes only: invitation status, expiry, `SubjectExpected` flag, account `BindingState`
  Pending→Bound; token hash and subjects are `ValuesWithheld`. New S1 operations: `MembershipInvitationReissue`,
  `MembershipInvitationAccept` (critical, `PREPARATION_COMMAND`), `MembershipInvitationPreview` (session observation).
  New SecurityEvent action `MembershipInvitation`, results `invitation_unavailable`, `invitation_closed`,
  `invitation_expired`, `invitation_subject_mismatch`, `invitation_token_invalid`.
- **UA2-D09 — migration path.** Migration `20261124100002_UserAdministrationInvitations`: `Accounts.Subject` nullable
  (CHECK `COALESCE(length("Subject"),0) > 0 OR ("Subject" IS NULL AND "Source" = 'AdminProvisioned')`; QA N2: the
  earlier form evaluated to NULL for a NULL subject and therefore passed for every source), filtered unique index, `Invited` status and three reasons in the closed
  CHECK, `MembershipInvitations` table, two receipt actions. Existing `AdminProvisioned` rows created before this change
  keep their subject and Active state and are treated as **already accepted** (no data rewrite). Down fails closed while
  an unaccepted invitation account exists. No seed rows (`docs/seed-data.md` USER-ADMIN-02).
- **UA2-D10 — privacy.** The optional expected subject is a pseudonymous IdP identifier stored only on the invitation
  (privacy-register entry like UA-D10 N6: purpose = restrict who may accept; audience = tenant membership administrators
  via exact-subject search; never returned or logged). No e-mail address or name is collected; the link is handed out of
  band (no e-mail sending in scope).

## API

| Method | Route | Operation |
| --- | --- | --- |
| POST | `/api/v1/memberships` | `CreateMembership` — now an invitation; returns `MembershipInvitationIssueDto` |
| POST | `/api/v1/memberships/{id}/invitation` | `ReissueMembershipInvitation` (S1 `MembershipInvitationReissue`) |
| POST | `/api/v1/memberships/{id}/revoke` | existing; on an Invited membership also closes the pending invitation |
| POST | `/api/v1/invitations/preview` | `PreviewMembershipInvitation` (S1 `MembershipInvitationPreview`, invitation session only) |
| POST | `/api/v1/invitations/accept` | `AcceptMembershipInvitation` (S1 `MembershipInvitationAccept`, invitation session only) |

`MembershipAccessSummaryDto` adds `invitationState`, `invitationExpiresAt`, `invitationSubjectExpected`; `SessionDto` adds
`invitationSession`. Endpoint profiles: reissue/accept `NATIVE-COMMAND-1`, preview `NATIVE-OBSERVATION-COMMAND-1`.

## UI

`/app/user-admin`: Invited status and invitation state badges (Pending/Expired/Accepted/Revoked/Superseded), optional
expected subject on create, the one-time invitation link with **Copy invitation link** right after create/reissue,
**Create a new invitation link**, **Revoke invitation**. `/app/accept-invitation` (route only, not a navigation
destination): sign-in prompt, member-session refusal, preview of what the invitation grants, accept, neutral refusal,
rate-limit, offline and error states. TR/EN copy; the other 15 locales carry the English text (repo pattern). Help guides
`frontend/src/help/content/{tr,en}/user-admin.md` updated (8 headings, `reviewed: false`).

## Acceptance criteria

| ID | Criterion | Tests |
| --- | --- | --- |
| UA2-AC-01 | Token: 256-bit secret, strict format, tenant round-trip, hash ≠ secret; malformed tokens rejected | domain `Tokens_are_high_entropy_…`, `Malformed_tokens_are_rejected` |
| UA2-AC-02 | Acceptance rule (pending, unexpired, invited, issuer, expected subject); Invited lifecycle (revoke only, never admits a session); 24 h source default | domain `Only_a_pending_unexpired_invitation_…`, `An_invited_membership_can_only_be_revoked_…`, `Expiry_is_derived_from_server_time_…` |
| UA2-AC-03 | Account binds once (null→subject, AdminProvisioned only); accepted-binding index filtered | persistence unit `MembershipInvitationStorageGuardTests` |
| UA2-AC-04 | Invitation starts Pending, closes once, token/subject/expiry immutable; Invited never reopened | persistence unit `MembershipInvitationStorageGuardTests` |
| UA2-AC-05 | Squatting impossible: a tenant-2 invite for a tenant-1 subject reserves nothing; tenant 1 can still invite; holder accepts tenant 1; tenant-2 link then neutral | API `A_foreign_tenant_invitation_cannot_squat_…` |
| UA2-AC-06 | Invited grants nothing (forged binding 401, locator null) until acceptance; afterwards login lookup finds the tenant-1 binding | API `A_foreign_tenant_invitation_cannot_squat_…`, `Admin_provisions_…` |
| UA2-AC-07 | Expired, used, superseded (reissue), revoked, wrong-subject, malformed and unknown tokens → same 409; reissue/version conflict; reissue after acceptance 409 | API `Expired_reissued_revoked_used_and_wrong_subject_…` |
| UA2-AC-08 | Concurrent accept → exactly one binding; one identity × two tenants → bound to one | API `Concurrent_acceptance_binds_exactly_one_…` |
| UA2-AC-09 | Rate limit 429 per identity; CSRF and Idempotency-Key required; member sessions 403; tenant isolation 404 | API `Invitation_attempts_are_rate_limited_…`, `A_foreign_tenant_invitation_cannot_squat_…` |
| UA2-AC-10 | No token, token hash or subject in EF audit or SecurityEvents; SecurityEvent attribution (tenant/account) and closed reason codes | API `A_foreign_tenant_invitation_cannot_squat_…`, `Expired_reissued_…`; `MembershipAuditBeforeAfterTests.Invitation_rows_are_proven_without_token_or_subject` |
| UA2-AC-11 | Contracts: S1 (+3), endpoint profiles (+3), OpenAPI/api.d.ts, audit vocabulary, S1 deny/anonymous/forged probes | `S1ScopeCoverageTests`, `EndpointProfileRegistryTests`, `DiagnosticsTests.OpenApi_*` |
| UA2-AC-12 | USER-ADMIN-01 behaviour preserved through the invitation flow | API trait `USER-ADMIN-01` |
| UA2-AC-13 | UI: invitation states, one-time link + copy, reissue body, subject optional, strict decoding | `frontend/src/product/UserAdminWorkspace.test.tsx` |
| UA2-AC-15 | QA fixes: fragment link + cleared before any request, legacy query ignored; DB CHECK refuses NULL/empty subject for other sources (raw SQL); limiter bounded/eviction/audited-once; identity reference; invitation-session decision | `AcceptInvitationPage.test.tsx`, `UserAdminWorkspace.test.tsx`, persistence PG `MembershipInvitationPostgresTests`, Api unit `InvitationSessionPolicyTests`, API `Expired_reissued_…` |
| UA2-AC-14 | Accept page: token leaves the URL, sign-in return without token, preview + accept with CSRF/key, neutral refusal, member session, rate limit, invalid link | `frontend/src/product/AcceptInvitationPage.test.tsx` |

## Open items

- `account.invite_hours` tenant override (54.01, 1–168 h) is not wired: every tenant uses the 24 h source default.
- A revoked accepted binding keeps the identity bound to its tenant (strict UA2-D04); re-homing a person to another
  organisation needs an operator procedure (not built). Multi-organisation membership with a login picker is a future
  product decision.
- No e-mail/outbox delivery (52.02 paragraph:342); the administrator hands the link out of band.
- The attempt limiter is process-local; a multi-instance deployment needs a shared limiter.
- No real-IdP (Keycloak) journey or Playwright run for the invitation login. The invitation-session decision is unit-tested
  through `FoundationIdentity.InvitationSession`; the OIDC event wiring itself (`OnTokenValidated` assigning the result)
  needs a signed ID token from a real or emulated IdP, which the synthetic test auth handler bypasses by design.
- Identity references change when the OIDC client secret rotates (and per process for a public client).
- 15 locales use English fallback text for the new strings.
