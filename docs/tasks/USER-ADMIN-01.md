# USER-ADMIN-01 — Kullanıcılar ve yetkiler (runtime membership administration)

Derived package ID (not a canonical SP). Product-owner request 21.11.2026: users could only be defined server-side in
`Authentication:Oidc:Memberships`. This package makes tenant memberships manageable at runtime from the database while
the operator configuration stays the bootstrap/break-glass source. Builds on PLAN-1511-5202 (lifecycle, delegation,
last-admin) without changing its decisions for configuration-governed identities.

## Source locators

| Topic | Locator |
| --- | --- |
| Least privilege + tenant/vessel/object scope | 13.03 v46.3 Rol Bazlı Kullanıcı El Kitabı paragraph:11-12 |
| Tenant Admin: user/role/tenant/vessel/object scope assignments with least privilege | 13.03 §9 paragraph:102-103 |
| MFA/IdP policy stays in the central IdP | 13.03 §9 paragraph:104 |
| Delegation/proxy start-end and scope audited | 13.03 §9 paragraph:105 (existing 5202 delegation; 52.02 paragraph:102-103) |
| Controlled configuration publish, no direct production data fixes | 13.03 §9 paragraph:106 |
| "Yetki yok → Tenant Admin" escalation | 13.03 §10 paragraph:114-116 |
| Role sections used for bundles | 13.03 §2 paragraph:25-48 (Master), §3 paragraph:50-56 (Tug Master), §4 paragraph:57-78 (DPA/HSEQ), §5 paragraph:79-85 (Chief Engineer), §6 paragraph:86-91 (Crew/OOW), §7 paragraph:92-96 (Shore Ops/Tug Coordinator), §8 paragraph:97-101 (Regulatory/Assurance) |
| Existing membership design | `docs/tasks/plan-1511-5202-01.md` (P5202-D01..D08) |

Locators are 1-based paragraph indices as returned by `py scripts/query_sources.py "Tenant Admin" --limit 4`.

## Decisions

- **UA-D01 — grant source precedence.** Per identity (issuer+subject) exactly one grant source governs:
  1. an entry in `Authentication:Oidc:Memberships` ⇒ **configuration governs** exactly as in P5202-D01 (config grant ∩
     persisted lifecycle overlay). This is the bootstrap/break-glass source; the UI shows it `ManagedBy = Configuration`
     and read-only (409 `configuration_managed` on grant edit). Lifecycle (suspend/resume/revoke) still applies.
  2. no config entry and a persisted account/membership with `Source = AdminProvisioned` and a grant revision ⇒ the
     **database grant governs** (`ManagedBy = Database`).
  3. any other persisted row (`ConfigurationImport` without config entry, `SyntheticUnverified`) ⇒ grants nothing
     (unchanged P5202-D01 rule, `ManagedBy = None`).
  Creating a DB membership for an identity present in the configuration is refused (409 `identity_unavailable`). If an
  operator later adds a config entry for a provisioned identity, configuration wins and the 5202 narrowing intersects
  it with the (empty) imported vessel snapshot — fail-closed, never wider.
- **UA-D02 — grant storage.** Each grant change appends an immutable `identity.MembershipGrantRevisions` row (bundles,
  individual permissions, effective permission snapshot, tenant-wide read/manage, active company, granting actor, server
  time) plus immutable `identity.MembershipGrantVessels`. The latest revision is current; earlier ones are the reviewable
  grant history. The effective snapshot is bounded at grant time, so a later code change to a bundle never widens an
  existing grant; at resolution it still passes `ActorResolver.TrustedPermissions`.
- **UA-D03 — role bundles.** `MembershipRoleBundles` (Domain) defines 9 fixed bundles of existing trusted permissions:
  `master`, `tug-master`, `dpa-hseq`, `chief-engineer`, `crew-oow`, `shore-ops`, `regulatory-assurance`, `tenant-admin`,
  `read-only`, each with its 13.03 locator. Bundle role ids are deterministic GUIDs; the grant's role context is the
  canonical set of bundle role ids (at least one bundle is required, so every DB membership has write attribution).
  Bundles are a technical starting set, **not an approved production role catalog**.
- **UA-D04 — decision authority is never bundled.** Permissions that record a human decision/verification/approval/
  attestation/sign-off, security suspension or purge approval (`IsDecisionAuthority`) are excluded from every bundle and
  can only be assigned individually by an administrator who holds them. Appointment-gated actions (TugOperations,
  Departure, HumanAuthorities, CrewAuthorities, …) additionally require operator-provisioned appointments, which this
  package never creates; a DB membership has none.
- **UA-D05 — least privilege and separation.** A grant (bundle permissions, individual permissions, tenant-wide read,
  tenant-wide manage) can never exceed the granting administrator's current authority (400 `validation_failed` with
  `exceeds_grantor_authority`). An administrator cannot change their own grant (403 `self_modification`: no
  self-escalation or self-lockout). The last active administrator (P5202-D04 predicate; DB admins counted with the same
  predicate on their current grant) cannot be demoted by a grant change (409 `last_admin`) or suspended/revoked
  voluntarily (existing guard). Vessel and company references must exist in the caller's tenant.
- **UA-D06 — privacy.** Only issuer+subject are stored (P5202 privacy decision); no password, hash, token, e-mail or
  display name. Subjects are never returned; the list can be searched by exact subject (server-side match only) or id
  prefix. Audit summaries carry closed bundle codes, counts and flags; permission lists are withheld.
- **UA-D07 — when changes take effect.** Every request is revalidated by `MembershipSessionGate`, which re-reads the
  current grant revision: a grant change applies from the member's **next request**; a suspension/revocation denies the
  next request (5202). A new membership applies at the member's **next login**. Login (`OnTokenValidated`) resolves an
  unconfigured identity through `ProvisionedIdentityLocator` — the single cross-tenant read, keyed only by the
  IdP-validated issuer+subject of the token being accepted, returning only `AdminProvisioned` bindings — and stamps
  tenant + actor into the protected session cookie (`poseidon:tid`, `poseidon:src=provisioned`). A provisioned session
  without a current Active grant is signed out (401).
- **UA-D08 — commands.** `POST /api/v1/memberships` and `POST /api/v1/memberships/{id}/grant` reuse the 5202 command
  prelude (CSRF, Idempotency-Key, attempt/denial SecurityAudit `MembershipCommand`), SERIALIZABLE transaction with the
  acting-admin fence, audited SaveChanges and `MembershipCommandReceipt` (actions `MembershipCreate`,
  `MembershipGrantUpdate`). Grant optimistic concurrency: `expectedVersion` = current grant revision (stale ⇒ 409
  `version_conflict`; concurrent append ⇒ PK ⇒ 409 `lineage_conflict`). Reason code `PREPARATION_COMMAND` as in 5202.
  No new permission: reads need `membership.read|manage` + tenant-wide scope, writes `membership.manage` + tenant-wide
  manage + role context (existing `CanReadMemberships`/`CanManageMemberships`).
- **UA-D09 — seed.** No seed rows (see `docs/seed-data.md` USER-ADMIN-01).

- **UA-D10 — QA notes on 2e61f7a3 (ACCEPT-WITH-NOTES).**
  - *N2 grantor authority.* Create and grant-update re-read the grantor's current authority inside the same
    SERIALIZABLE transaction as the write (`CurrentGrantor`): a provisioned grantor's latest grant revision; a
    configured grantor's static operator grant plus the existing persisted-lifecycle fence of `Run`. A concurrent
    demotion either serializes before the read (⇒ 409 `conflict_changed` or a narrower grantor) or aborts with
    40001/40P01, classified by `Run` through `PostgresFailure` (`SerializableTransactionGuardTests`). No deterministic
    race test was added (a two-writer interleaving is not cheaply forceable); suspend/resume/revoke and delegation
    commands by a provisioned admin still use the request-start grant (the gate re-reads it on every request).
  - *N3 narrowing by a less-privileged admin.* An administrator may narrow (or replace) the grant of a member who
    holds more than the administrator does; only the NEW grant is bounded by the grantor. Accepted under 13.03 §9
    paragraph:103 (the tenant admin assigns scopes with least privilege); the last-admin guard still applies.
  - *N4 shadowing.* Any operator entry for a provisioned identity (any tenant, any actor id) shadows the database grant:
    configuration governs the identity entirely. The screen shows `ManagedBy = Configuration` with
    `editBlockedReason = configuration_shadowed`, no grant values (the entry may belong to another tenant), the bundle
    filter excludes it, and grant edits return 409 `configuration_managed`.
  - *N5 paging.* Status, search and bundle filters, the total and paging are evaluated in the database (tenant query
    filters on every set; bundle = latest revision of a governing, unshadowed `AdminProvisioned` grant).
  - *N6 privacy register.* Exact-subject search lets a `membership.read` holder confirm that a given IdP subject has a
    membership in their own tenant (never another tenant's). Recorded as a privacy-register entry: purpose =
    administrator lookup; data = IdP subject (pseudonymous identifier); audience = tenant membership readers; the subject
    is never returned or logged (query strings are not audited).
  - *N8 immutability.* Grant revisions and their vessel rows are append-only through the EF SaveChanges guard only;
    there is no database trigger (as for the 5202 grant snapshots). A database administrator can still alter rows.
  - *N9 navigation.* The `user-admin` destination is projected only with explicit `membership.read` and tenant-wide
    scope (`ShowsUserAdministration`); manage-only callers keep the API routes. The frontend contract is unchanged.
- **USER-ADMIN-02 (open follow-up, N1) — cross-tenant identity squatting.** `IX_Accounts_IssuerSubject` is global, so the
  first tenant administrator to provision an issuer+subject claims it: a tenant-2 admin who knows a tenant-1 user's
  subject could bind that identity to tenant 2 first (that user would then log into tenant 2, and tenant 1 receives
  `identity_unavailable`). Fix options: invitation + acceptance by the identity holder at login, or a per-tenant
  trusted-issuer binding (each tenant provisions only subjects of its own issuer/realm). Interim mitigation: operators
  deploy membership provisioning single-tenant per issuer, or tenant administrators are trusted parties; the operator
  configuration remains the authoritative source for any contested identity (it shadows the database grant).

## API

| Method | Route | Operation |
| --- | --- | --- |
| GET | `/api/v1/memberships/catalog` | `GetMembershipCatalog` — bundles (+ grantable), permission allow-list grouped by module (decision-authority / delegable / grantable flags), tenant vessels/companies, delegation reason codes, trusted issuer |
| GET | `/api/v1/memberships/access` | `ListMembershipAccess` — status/bundle/q filters, paging |
| GET | `/api/v1/memberships/{id}/access` | `GetMembershipAccess` — effective grant, scope, edit block reason, revision history |
| POST | `/api/v1/memberships` | `CreateMembership` (S1 `MembershipCreate`) |
| POST | `/api/v1/memberships/{id}/grant` | `UpdateMembershipGrant` (S1 `MembershipGrantUpdate`) |

Lifecycle and delegation use the existing 5202 routes. Migration `20261124100001_UserAdministration` (two identity tables,
widened closed CHECKs for `AdminProvisioned`, `AdminProvisioning`, the two receipt actions). New audit result codes:
`identity_unavailable`, `self_modification`, `configuration_managed`.

## UI

`/app/user-admin` ("Kullanıcılar ve yetkiler" / "Users and permissions", destination key `user-admin`, workspace group)
shown when the server projects it (`CanReadMemberships`): list with search and status/bundle filters, detail (bundles,
permissions by module, vessel scope, delegations, grant history, lifecycle actions), create and grant-edit forms with
loading/empty/error/forbidden/offline/conflict states. TR/EN copy; the other 15 locales carry the English text as fallback.

## Acceptance criteria

| ID | Criterion | Tests |
| --- | --- | --- |
| UA-AC-01 | Every bundle ⊆ trusted allow-list; allow-list public and order-preserving | Api unit `MembershipRoleBundleTests` |
| UA-AC-02 | Closed bundles, distinct role ids, directory.read in each, no decision authority in any bundle | domain `Bundles_are_closed_distinct_and_carry_no_decision_authority` |
| UA-AC-03 | Grant ≤ grantor (bundles, individual permissions, tenant-wide read/manage) | domain `A_grant_never_exceeds_the_grantor_authority`; API `Admin_provisions_…` (400 exceeds_grantor_authority) |
| UA-AC-04 | Closed/canonical input, scope required, distinct vessels | domain `Bundles_and_individual_permissions_…`, `Scope_is_required_…` |
| UA-AC-05 | Administrator predicate; self-modification forbidden | domain `Administrator_predicate_…`, `Own_membership_…`; API `Self_modification_…` (403) |
| UA-AC-06 | DB membership governs the member's next request; no binding ⇒ none; forged tenant binding ⇒ 401 | API `Admin_provisions_…` |
| UA-AC-07 | Grant revisions immutable; closed bundle codes; provisioned membership starts Active rev 0 | persistence unit `MembershipGrantStorageGuardTests` |
| UA-AC-08 | Grant change appends a revision, narrows next request; stale expectedVersion ⇒ 409 | API `Admin_provisions_…` |
| UA-AC-09 | Idempotent create replay; duplicate / configured identity ⇒ 409 identity_unavailable; unknown body fields ⇒ 400; untrusted issuer, foreign vessel ⇒ 400 | API `Admin_provisions_…` |
| UA-AC-10 | Configuration-governed identity is read-only (409 configuration_managed) | API `Self_modification_…` |
| UA-AC-11 | Provisioned admins count for last-admin (409 last_admin) | API `Self_modification_…` |
| UA-AC-12 | Tenant isolation (404, not listed) and non-admin 403 with no write | API `Self_modification_…` |
| UA-AC-13 | Delegation from a provisioned source bounded by and narrowed with its current grant | API `Delegation_from_a_provisioned_source_…` |
| UA-AC-14 | Login lookup returns only AdminProvisioned bindings | API `Admin_provisions_…` (`ProvisionedIdentityLocator`) |
| UA-AC-15 | Contracts: S1 (+2), endpoint profiles (+5), OpenAPI/api.d.ts, audit vocabulary, navigation label sync | `S1ScopeCoverageTests`, `EndpointProfileRegistryTests`, `DiagnosticsTests.OpenApi_*`, `WorkspaceMenuAuthorizationTests.Dev022_search_label_map_*` |
| UA-AC-17 | Filters/total/paging in the database beyond 2000 rows | API `Access_list_filters_counts_and_pages_in_the_database_beyond_two_thousand_rows` |
| UA-AC-18 | Config entry shadows a DB grant: read-only, 409, governed by config | API `A_configuration_entry_shadows_a_provisioned_grant_which_becomes_read_only` |
| UA-AC-19 | Navigation destination needs membership.read | API `User_admin_destination_needs_membership_read` |
| UA-AC-16 | UI: list/filter, create body, non-grantable disabled, field errors, self block, conflict, forbidden hides permission names | `frontend/src/product/UserAdminWorkspace.test.tsx` |

## Open items

- Owner/verifier/qualifier eligibility lists and appointment readers still read the operator configuration only
  (P5202-OPEN-01): DB members never appear as eligible owners/verifiers and hold no appointments.
- No person link/display name (privacy decision); members are identified by account id + bundles + exact-subject search.
- Bundles and decision-authority classification are technical and need an approved role catalog review.
- 15 locales use English fallback text for the new screen.
- USER-ADMIN-02: cross-tenant identity squatting (see UA-D10 / N1).
- Invitation/activation flows of 52.02 G, IdP account creation and MFA enforcement remain in the IdP (13.03 paragraph:104).
