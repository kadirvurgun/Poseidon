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

## Open items
See task contract: P5202-OPEN-01 eligibility lists, no display name, bundle catalog approval, 14 locales English
fallback (`i18n_untranslated.py` will report them), IdP-side invitation/MFA.

## Next step
CI on push, independent QA, then integration into the next bundle PR.
