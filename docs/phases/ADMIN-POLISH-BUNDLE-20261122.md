# ADMIN-POLISH-BUNDLE-20261122 — integration of mobile UI, help center, invitation e-mail and rate limiting

Status: **INTEGRATED, LOCALLY VERIFIED (non-PG); PostgreSQL/API integration tests and required CI pending.**
This is an integration package, not a canonical sprint or release gate. Accepted canonical count is unchanged:
114/121 tasks, 932/970 SP (pilot-week tasks remain human gates).

- Branch: `integration/admin-polish-20261122`, base `origin/main` 36d5d183.
- Merge order (normal merge commits, no rebase/force):

| # | Package | Head | Independent QA | Resolution |
|---|---|---|---|---|
| 1 | `fix/mobile-ui-defects-20261122` | 7f196f8c | ACCEPT-WITH-NOTES | CSS notes fixed in 7f196f8c (mobile-only checkbox sizing, 44 px touch targets); branch CI was red on UG-2 screenshot provenance because `capture.ts` changed without regenerating the 196 screenshots, so the `capture.ts` badge-placement change is removed from this bundle (DEFERRED to a screenshot regeneration package) |
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
| `py scripts/export_openapi.py` (Release host, no DB) | exported 501 paths / 946 schemas; `contracts/openapi.v1.json` unchanged |
| `py scripts/test_project_status_consistency.py`, `test_ops_runtime.py` | 7/7, 19/19 |
| `py scripts/i18n_untranslated.py` | exit 0; 0 missing/extra keys in every catalog. The only new untranslated value against main is `copies.UserAdmin_tsx_copy.delivery` = "E-mail" in cs/fr/hu/it/pt-BR/sk, a correct cognate |

DEFERRED: PostgreSQL persistence and API integration tests (CI), real SMTP journey,
browser/Playwright journeys and human review of machine-drafted locale strings.

## Next step

Push the integration branch once invite-email CI is green, then run CI, independent QA on the integration head and one integration PR.

## Integration QA (634b3bd5)

Independent integration QA: REJECT on CI run 45063250285 (`frontend-and-browser` step "Help screenshot mapping and provenance tests", `tests/Poseidon.Browser.Tests/screenshots/provenance.test.ts:29`, `generator_blob` drift from the mobile branch's `capture.ts` change). All other jobs green (Persistence Integration 139/139, API PG gate 4 shards, migration model, secret scan). Code review of all post-QA fix commits and the migration renumber: ACCEPT-WITH-NOTES. Fix: `capture.ts` restored to main (screenshots and provenance unchanged; badge-placement improvement deferred until screenshots are regenerated in the lab); `product.spec.ts` Help center focus assertion made unconditional (N2). Open notes: N1 mobile `.tug-workspace label` visual check; N3 all API instances need the same `InvitationDelivery` transport config; N4 reissue idempotency fingerprint records only address presence; L2 persisted Data Protection key ring is a production precondition.

## HELP-MANUAL-DOWNLOAD-01 (merged 9ed1a5fb)

UG-2 screenshots (196) regenerated in the non-required `help-screenshots.yml` workflow (run 45075963418, compare 196/196, provenance commit 5e7e2e0e), `capture.ts` badge placement re-applied, TR/EN PDF manuals downloadable from `/app/help` (draft, `reviewed:false`). Branch CI 45078869198 green. Independent QA ACCEPT-WITH-NOTES (`manual-download-qa.result.md`). Open follow-ups: N1 run `scripts/test_lab_profile_acl.py` POSIX tests in a Linux CI step; N2 `lab_profile_ca.py:21-32` fail closed when a pinned-bundle dependency is missing instead of possible system NSS fallback (lab-only, synthetic data, throwaway runner); N3 PDFs are public static assets (UI-only gating, synthetic content); N5 +18.9 MB; N6 pre-existing UI defects visible in screenshots (language selector globe overlap, mobile overflow of long codes, desktop safety checkbox box, `SeverityPolicyUnconfigured` vs "no open high-severity events" honesty review).
