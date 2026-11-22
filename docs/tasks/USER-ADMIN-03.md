# USER-ADMIN-03 — Davet e-postası (outbox) ve kiracı davet süresi

Derived package ID (not a canonical SP). Closes two USER-ADMIN-02 open items: (a) invitations were not delivered by
e-mail (the administrator had to copy the link), (b) the invitation lifetime was global (24 h) instead of the 54.01
tenant setting. Builds on USER-ADMIN-02 (UA2-D01..UA2-D10) without changing the token, acceptance or session design.

## Source locators

| Topic | Locator |
| --- | --- |
| Token hash stored, delivery tracked separately | 52.02 Rev3 paragraph:342 |
| Invitation expiry uses `account.invite_hours` | 52.02 Rev3 paragraph:277, paragraph:389 |
| `account.invite_hours`: Tenant, integer, default 24, range 1–168 | 54.01 Rev4 paragraph:10025 (registration paragraph:3231-3237) |
| No secrets/PII in logs, attributed system audit | `docs/security-baseline.md` §Log/audit, AGENTS.md |

Locators as cited in `docs/tasks/USER-ADMIN-02.md` (`py scripts/query_sources.py "account.invite_hours" --limit 6`).
The 52.02/54.01 rows are `candidate-unverified` source text, not an approved catalog.

## Decisions

- **UA3-D01 — transactional outbox.** `identity.InvitationDeliveries` (tenant-scoped, query filter, FK to the
  invitation, one row per invitation) is written in the SAME audited SaveChanges transaction as the invitation on
  create (`deliveryEmail` on `POST /api/v1/memberships`) and reissue (`deliveryEmail` on
  `POST /api/v1/memberships/{id}/invitation`, new `InvitationReissueCommand`). No `deliveryEmail` = no row (copy link
  only, unchanged USER-ADMIN-02 behaviour). Migration `20261124110001_UserAdministrationInvitationDelivery`.
- **UA3-D02 — token and address at rest.** The existing design never stores the token (only `SHA-256(secret)`), so the
  link cannot be rebuilt from the database at send time. The outbox therefore keeps recipient + token in
  `ProtectedPayload`: ASP.NET Core Data Protection ciphertext with purpose
  `Poseidon.InvitationDelivery.Payload.v1` + tenant id + delivery id (a ciphertext copied to another row/tenant does not
  decrypt). The payload is erased when the row becomes terminal (DB CHECK + EF guard: QUEUED ⇔ payload present). Before
  sending the dispatcher re-verifies `SHA-256(token secret)` against the invitation's stored hash. If the Data
  Protection key ring is lost (non-persisted keys) the row fails closed as `FAILED/payload_unavailable`; the
  administrator reissues. Without a transport the row is recorded `NOT_CONFIGURED` at creation and keeps nothing.
- **UA3-D03 — link origin.** `InvitationDelivery:PublicBaseUrl` (https, no query/fragment/userinfo). Never the request
  `Host` header (caller-controlled → phishing link). The token stays in the URL fragment (`#token=`, USER-ADMIN-02 QA N1).
- **UA3-D04 — state machine.** `QUEUED → SENT | FAILED | NOT_CONFIGURED | CANCELLED`, terminal states never change
  (`InvitationDeliveryPolicy`, EF guard, CHECK). `SENT` = the SMTP server accepted the message, not that the person
  read it or accepted the invitation (UI says so). Retry: 5 attempts, backoff 1/2/4/8 min (cap 1 h); 5xx → `FAILED`
  `smtp_rejected`; 4xx/connection/timeout → retry, after the 5th `smtp_retries_exhausted`; a retry that would fall
  after the invitation's expiry stops as exhausted. A closed (superseded/revoked/accepted) or expired invitation is
  `CANCELLED` (`invitation_closed`/`invitation_expired`) and never sent; reissue and revoke cancel queued rows in the
  closing transaction. Closed error codes only — never an SMTP reply, address or exception text.
- **UA3-D05 — dispatcher identity and tenant isolation.** `InvitationDeliveryDispatcher` (hosted service) writes only as
  the explicit system actor `system:invitation-delivery` (ActorType `System`, Source `InvitationDelivery`) through a
  tenant-scoped context per tenant; every change is an EF audit row with `ReasonStatus = SystemOperation` and closed
  fields (state before/after, attempts, error code; recipient `ValuesWithheld`). The only cross-tenant read is the due
  scan returning tenant ids (`IgnoreQueryFilters` on a context that never writes, keyed by no request input) — same
  pattern as the login lookup. Concurrency: claim = attempt count + lease (`LeaseUntil`) saved with the xmin token
  before sending; a lost claim skips the row. At-least-once: a crash after an accepted send can resend once after the
  lease (documented, not exactly-once).
- **UA3-D06 — transport.** `IInvitationMailer`; default `SmtpInvitationMailer` (System.Net.Mail, no new dependency).
  Options `InvitationDelivery:{Enabled, PublicBaseUrl, SmtpHost, SmtpPort=587, Security=StartTls|None, From, UserName,
  PasswordReference, PollSeconds, BatchSize, LeaseSeconds, SendTimeoutSeconds}`. `Security=None` only for a loopback host
  (development mail catcher). The password is read only through `PasswordReference` (environment/secret-store variable
  name via `OperatorSecretReferences.Resolve`); a literal password key does not exist (unknown keys fail startup).
  Implicit TLS (465) is not supported by this client (use submission + STARTTLS). Logs carry the closed outcome and
  exception type only, never the message (SMTP exceptions can contain the recipient), address, link, token or secret.
  Invalid/absent configuration = not configured: the dispatcher does no DB access, new e-mail rows are `NOT_CONFIGURED`.
- **UA3-D07 — recipient.** Narrow address check (no display names, quoted/IP-literal forms, CR/LF/comma/angle brackets:
  no header injection), ≤254 chars. The address is personal data: it is never returned, audited or logged; the
  idempotency fingerprint records only whether an address was given (no hash of it). Privacy-register entry: purpose =
  deliver this one invitation; retention = until the row is terminal (minutes in normal operation, at most the
  invitation lifetime); audience = none (ciphertext only).
- **UA3-D08 — tenant lifetime.** `identity.TenantInvitationSettings` (PK tenant, `LifetimeHours` CHECK 1–168). No row = 24 h
  source default (p10025 kept). `GET /api/v1/memberships/invitation-settings` (membership read, tenant-wide) and
  `POST` (S1 `InvitationLifetimeUpdate`, critical, `PREPARATION_COMMAND`, `membership.manage` tenant-wide, CSRF +
  Idempotency-Key, `expectedVersion` = settings version, 0 = default) — audited EF write + receipt. Applies to
  invitations issued afterwards; existing expiry stays immutable (USER-ADMIN-02).
- **UA3-D09 — seed.** No seed rows: a delivery row carries a live token ciphertext or claims a delivery outcome; a tenant
  lifetime is an attributed administrator setting. `docs/seed-data.md`.

## API

| Method | Route | Operation |
| --- | --- | --- |
| POST | `/api/v1/memberships` | `CreateMembership` + optional `deliveryEmail` |
| POST | `/api/v1/memberships/{id}/invitation` | `ReissueMembershipInvitation` body `InvitationReissueCommand {expectedVersion, deliveryEmail?}` |
| GET | `/api/v1/memberships/invitation-settings` | `GetInvitationSettings` (`NATIVE-READ-1`) |
| POST | `/api/v1/memberships/invitation-settings` | `UpdateInvitationSettings` (S1 `InvitationLifetimeUpdate`, `NATIVE-COMMAND-1`) |

`MembershipAccessSummaryDto.invitationDeliveryState` (QUEUED/SENT/FAILED/NOT_CONFIGURED/CANCELLED or null).
`InvitationSettingsDto {lifetimeHours, minHours, maxHours, defaultHours, tenantValue, version, deliveryConfigured, canManage}`.

## UI

`/app/user-admin`: delivery badge on the list and detail (SENT shown as "accepted by the mail server", FAILED/NOT_CONFIGURED
as warnings with the copy-link fallback), optional e-mail field on create and reissue (hint depends on whether delivery is
configured), invitation lifetime section (read-only for readers; number field 1–168 + save for managers). 23 new/changed
strings in all 17 catalogs (machine-drafted translations, not human-reviewed).

## Acceptance criteria

| ID | Criterion | Tests |
| --- | --- | --- |
| UA3-AC-01 | Closed delivery states, only QUEUED moves, terminal never changes; backoff 1/2/4/8 min, 5 attempts, FAILED terminal | domain `InvitationDeliveryPolicyTests`; persistence unit `InvitationDeliveryStorageGuardTests` |
| UA3-AC-02 | Only server acceptance is SENT; unconfigured transport/credential = NOT_CONFIGURED, never delivered | domain `Only_mail_server_acceptance_…`; Api unit `A_missing_credential_…` |
| UA3-AC-03 | Lifetime 1–168 h, default 24 (p10025) | domain `Lifetime_is_bounded_…`; persistence unit `The_tenant_lifetime_stays_within_…` |
| UA3-AC-04 | Payload present only while QUEUED; payload/invitation/attempts immutable; closed error codes | persistence unit `InvitationDeliveryStorageGuardTests` |
| UA3-AC-05 | No recipient/token/link/password in logs; SMTP outcomes classified | Api unit `An_unreachable_server_…`, `Smtp_replies_are_classified_…` |
| UA3-AC-06 | Payload protected and bound to tenant + row; lost key fails closed; https origin only | Api unit `The_outbox_payload_is_protected_…`, `The_transport_is_configured_only_…`, `The_link_uses_…` |
| UA3-AC-07 | Outbox row in the invitation transaction; dispatcher sends once as attributed system actor; no address/token in audit | API PG `An_invitation_email_is_queued_…` |
| UA3-AC-08 | Transient → retry with backoff → terminal FAILED | API PG `Transient_failures_retry_…` |
| UA3-AC-09 | Without transport NOT_CONFIGURED and copy link still accepted; invalid recipient 400 | API PG `Without_a_transport_…` |
| UA3-AC-10 | Reissue/revoke cancel queued e-mail, erase payload, nothing sent | API PG `Reissue_and_revoke_cancel_…` |
| UA3-AC-11 | Lifetime endpoint bounded, permission-gated, version-checked, audited, tenant-isolated, used on create | API PG `The_tenant_invitation_lifetime_…` |
| UA3-AC-12 | Contracts: S1 +1 (304/294/319), endpoint profiles +2, OpenAPI/api.d.ts, audit vocabulary | `S1ScopeCoverageTests`, `EndpointProfileRegistryTests`, scripts `test_s1_audit_contract` |
| UA3-AC-13 | UI: delivery badges, e-mail field on create/reissue, lifetime 1–168 with version, read-only for readers, strict decoding | `frontend/src/product/UserAdminWorkspace.test.tsx` |

## Open items

- No real SMTP server journey (only a recording mailer and an unreachable loopback port); implicit TLS (465) unsupported.
- Data Protection key persistence is operator configuration; with ephemeral keys a restart turns queued rows into
  `FAILED/payload_unavailable` (fail closed, reissue needed).
- At-least-once delivery (possible single duplicate after a crash mid-send).
- E-mail body is fixed bilingual EN/TR plain text; not localized per recipient, not human-reviewed copy.
- No bounce/DSN processing: `SENT` is server acceptance only.
- Machine-drafted strings in 15 locales need human review (UG-6).
