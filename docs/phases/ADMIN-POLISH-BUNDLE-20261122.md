# ADMIN-POLISH-BUNDLE-20261122 — integration of mobile UI, help center, invitation e-mail and rate limiting

Status: **INTEGRATED, LOCALLY VERIFIED (non-PG); PostgreSQL/API integration tests and required CI pending.**
This is an integration package, not a canonical sprint or release gate. Accepted canonical count is unchanged:
114/121 tasks, 932/970 SP (pilot-week tasks remain human gates).

- Branch: `integration/admin-polish-20261122`, base `origin/main` 36d5d183.
- Merge order (normal merge commits, no rebase/force):

| # | Package | Head | Independent QA | Resolution |
|---|---|---|---|---|
| 1 | `fix/mobile-ui-defects-20261122` | 7f196f8c | ACCEPT-WITH-NOTES | notes fixed in 7f196f8c (mobile-only checkbox sizing, 44 px touch targets) |
| 2 | `feature/help-center-20261122` (HELP-CENTER-01) | a41fe952 | REJECT (deterministic test regression) | fixed in a41fe952 (drawer focus test, a11y, honest states) |
| 3 | `feature/invite-email-20261122` (USER-ADMIN-03) | 4ac9a7d6 | security QA REJECT (B1; M1 poison row, M2 retention when transport disabled) | fixed in a7418de1 and 4ac9a7d6 |
| 4 | `feature/rate-limit-20261122` (RATE-LIMIT-01) | 34205ea9 | ACCEPT-WITH-NOTES | M1, M2, L1, L4, N1 fixed in 34205ea9 |

QA reports: `C:\Users\avurgun\.claude-poseidon\scheduled\workers\{mobile-ui,help-center,invite-email,rate-limit}-qa.result.md`
(local operator files, not in the repo).

## Conflicts resolved

- Merges 1-3: no conflicts.
- Merge 4: `docs/project-status.md` and `docs/project-status.json`. Both packages' entries kept (USER-ADMIN-03 and RATE-LIMIT-01).
  `PoseidonDbContext`, `Startup.cs`, `MembershipInvitationEndpoints.cs`, the model snapshot, `security-baseline.md` and
  `S1ScopeCoverageTests.cs` auto-merged.
- i18n: no catalog conflicts. All 17 catalogs have identical key sets (`i18n_untranslated.py`: missingKeys 0 and extraKeys 0 in every locale).

## Migration renumbering

Both packages added migration id `20261124110001`. The rate-limit migration is now `20261124110002_RateLimitCounters`:
1. Removed `20261124110001_RateLimitCounters{,.Designer}.cs`.
2. Reset the model snapshot to the post-invite-email version (merge parent 1).
3. Rebuilt and ran `dotnet ef migrations add RateLimitCounters` with dotnet-ef 10.0.14 from the local tool manifest. The generated id was already `20261124110002`.
4. Generated `Up`/`Down` are identical to the original: schema `ratelimit`, PK (Policy, PartitionHash), the CHECKs
   `CK_RateLimitCounters_Count`/`_Hash`/`_Policy` and `IX_RateLimitCounters_ExpiresAt`. The RATE-LIMIT-01 header comment was restored.
   The new Designer contains both `InvitationDelivery*` and `RateLimitCounter`. The regenerated snapshot matches the auto-merged
   snapshot apart from line endings.
5. `migrations list`: `...110000_DailyReviewIndependence`, `...110001_UserAdministrationInvitationDelivery`, `...110002_RateLimitCounters`.
   `has-pending-model-changes`: none. References in `RATE-LIMIT-01.md` and the project status were updated. No test referenced the id.

## Local verification (this integration head; no Docker/PostgreSQL)

| Check | Result |
|---|---|
| `dotnet build Poseidon.slnx -c Release` | 0 warnings, 0 errors |
| `dotnet ef migrations has-pending-model-changes` | No changes |
| Domain tests | 1673/1673 |
| Persistence tests (non-Integration) | 198/198 |
| API tests (non-Integration) | 522/522 |
| frontend `npm ci`, `typecheck`, `lint` (Node 26.13.0) | exit 0 |
| frontend `npx vitest run` (Node 26.13.0) | 233/233 files, 3100/3100 tests |
| `py scripts/i18n_untranslated.py` | exit 0; 0 missing/extra keys in every catalog. The only new untranslated value against main is `copies.UserAdmin_tsx_copy.delivery` = "E-mail" in cs/fr/hu/it/pt-BR/sk, a correct cognate |

DEFERRED: PostgreSQL persistence and API integration tests (CI), OpenAPI regeneration (needs the full host), real SMTP journey,
browser/Playwright journeys and human review of machine-drafted locale strings.

## Next step

Push the integration branch once invite-email CI is green, then run CI, independent QA on the integration head and one integration PR.
