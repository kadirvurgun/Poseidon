# USER-ADMIN-01 — Kullanıcılar ve yetkiler

State: **AUTHORED, targeted tests PASS locally; CI and independent QA pending** (locally validated subpackage, not a
sprint or release gate). Branch `feature/user-admin-20261121` from `origin/main` 4d3b75c4. Task contract:
[docs/tasks/USER-ADMIN-01.md](../tasks/USER-ADMIN-01.md).

## Delivered behavior
- Tenant administrators provision database memberships for IdP identities (issuer+subject; accounts, passwords and
  MFA stay in the IdP) with role bundles, individual permissions, tenant-wide/vessel scope and active company.
- Operator configuration keeps precedence for its identities (bootstrap/break-glass); DB grants govern only
  `AdminProvisioned` identities without a config entry.
- Grant changes append immutable revisions (history), effective on the member's next request; new memberships at next login.
- Least privilege (grant ≤ grantor), no self-modification, last-admin protection including provisioned admins, tenant
  isolation, optimistic concurrency, idempotent receipts, audited SaveChanges + SecurityAudit codes.
- Delegations from provisioned sources bounded and narrowed by the source's current grant.
- `/app/user-admin` workspace (list/filter/search, detail, create/edit, lifecycle, delegations), TR/EN.
- Migration `20261124100001_UserAdministration`; no seed rows by design (docs/seed-data.md).

## Verification (21.11.2026, local, repo-local .NET SDK / Node 26, Docker PostgreSQL)
| Check | Result |
| --- | --- |
| `dotnet build Poseidon.slnx -c Release` | 0 errors, 0 warnings |
| Domain tests (all) | 1632/1632 passed |
| Persistence non-integration | 155/155 passed |
| Api non-integration | 477/477 passed |
| `dotnet ef migrations has-pending-model-changes` | no changes |
| Api integration (real PG), 24 filtered methods: 3 new USER-ADMIN tests, all 5202 membership/delegation/session tests, S1 coverage (3), endpoint profile registry (3), OpenAPI real contract, membership audit before/after (4), DEV-022 label sync | 27/27 passed |
| Frontend `npm run typecheck`, `npm run lint` | exit 0 |
| Frontend vitest full suite | 227 files, 2200 tests passed (UserAdminWorkspace 7 tests) |
| OpenAPI export + `npm run generate:api` | regenerated |

Not run: full Api integration suite (bundle gate/CI), Playwright/real-IdP login journey (the provisioned login path is
covered by `ProvisionedIdentityLocator` integration and the synthetic session binding, not by a Keycloak run),
`scripts` python tests (no script changed).

Test fixes outside the package: `EndpointProfileRegistryTests.Profile_probe_grants_every_server_accepted_permission`
reads the new `ActorResolver.KnownPermissions` array instead of regex-parsing the old filter;
`PilotProgramStorageGuardTests.Pilot_kit_migration_target_models_match_the_snapshot_…` compares only the `pilot` schema
(it compared every historical target model with the latest snapshot and broke on any later migration).

## CI
- Run 44939813106 (840a2410, push): all 1175 API integration tests and 130 persistence integration tests passed, but the
  critical-write coverage gate failed with `AUDIT_BEFORE_AFTER_UNPROVEN` for `UserAdministrationTests.cs:54` and `:208`
  (both perform a grant update). Cause: the `MembershipGrantRevision` audit projection set `BeforeRevision = N-1` on an
  `Added` row, which `CriticalWriteCoverageCapture.BeforeAfter` (CriticalWriteCoverageCapture.cs:199) rejects. Fix: no
  before revision on the Added row (prior revision stays in the `Revision` field). Reproduced and proven by
  `MembershipAuditBeforeAfterTests.Membership_create_and_grant_revision_rows_are_proven_without_permission_lists`
  (failed before the fix at MembershipAuditBeforeAfterTests.cs:166, 7/7 after); new integration tests 3/3 and
  persistence unit 155/155 re-run green.

## QA notes on 2e61f7a3 (ACCEPT-WITH-NOTES) — fixed in one commit
N2 grantor re-read inside the SERIALIZABLE transaction, N4 configuration shadowing (read-only `configuration_shadowed`,
409 `configuration_managed`), N5 database-side filters/total/paging, N9 navigation needs `membership.read`; N3/N6/N8
documented, N1 recorded as open follow-up USER-ADMIN-02 (UA-D10 in the task contract). Local results (22.11.2026):
build 0 errors; Api non-integration 478/478; Api integration real PG, 28 filtered methods (incl. 3 new: >2000-row
paging, shadowing, navigation) 31/31; frontend typecheck/lint exit 0, vitest user-admin + i18n + hardcoded-colors
7 files / 91 tests passed. No deterministic N2 race test (see UA-D10).

## Open items
See task contract: P5202-OPEN-01 eligibility lists, no display name, bundle catalog approval, 14 locales English
fallback (`i18n_untranslated.py` will report them), IdP-side invitation/MFA.

## Next step
CI on push, independent QA, then integration into the next bundle PR.
