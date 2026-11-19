# S5 sprint gate — independent QA review (08834cd)

- Target: PR #43 head `08834cdf48b439ad9b095e09a6010040b693f2bc`, merged into main as `fcbf36e4f14fdcf0eb602152bb537b8eb7d75f15`.
- Reviewer: independent QA subagent (did not author S5 code), own detached worktree, 19–20.11.2026.
- CI evidence for 08834cd (both `completed`, re-verified by the coordinator with `gh run list` before merge):
  - push run `44577141890` — success
  - pull_request run `44577156313` — success
- Canonical criteria: 18.01 Development Backlog rows 40–43, 81, 98–100; override authority 60.03 p154–166, 564, 575–580, 584 and REG-033..035.

## Rounds
1. **6453c32 — NOT ACCEPTED.** CI push `44563013824` / PR `44563111227` failed: two DEV-040 PG tests (`ApplicabilityOverrideApiTests.cs:46,31`). Separate blocking UI defect: stray PowerShell escape "`n" rendered at `CompliancePassport.tsx:49`.
2. **776f5cd — fix.** Root cause was a test defect: `AdmittedByCommands` replaces `Memberships[0]` via `RegulatoryHuman` (`RegulatoryAdmissionCurrentTests.cs:22`), so the override author permission/actor kind was set on a discarded membership; moved after admission, no assertion changed. Passport escape removed with a no-backtick assertion. DEV-098 `VerifyChanged` re-checks the officer pin when `log_date` changes. CI push `44570973368` / PR `44570986832` still failed one test (`ApplicabilityOverrideApiTests.cs:123`).
3. **08834cd — fix by the coordinator, reviewed independently.** Cross-tenant proposal expectation 403 → 404 `resource_not_found`: a tenant-wide author passes `Author`/`Human` (`CanEditVessel` under `TenantWideManage`, `FoundationIdentity.cs:59`) and tenant `Scope()` excludes the foreign vessel (`ApplicabilityOverrideEndpoints.cs:141`), identical to the GET at :122 and the repo cross-tenant rule (no existence oracle, cf. `MaintenanceObservationRecoveryTests.cs:266-270`); the no-row assertion is kept. Reviewer: legitimate correction, not a weakening.

## Verdicts (08834cd)
| Task | SP | Verdict | Notes |
|---|---|---|---|
| DEV-039 Pilot vessel onboarding wizard | 8 | ACCEPT-WITH-NOTES | class number critical (`ComplianceOnboardingPolicy.cs:29`); complete stays DECLARED_COMPLETE_UNVERIFIED |
| DEV-040 Applicability reason review | 8 | ACCEPT-WITH-NOTES | override maker-checker, human-only, append-only, REQUIRES_REVIEW after re-evaluation; approval always 422 `override_authority_unavailable` because no published OVERRIDE_AUTHORITY policy/grant exists — exactly what 60.03 p580 requires |
| DEV-041 Applicable controls | 8 | ACCEPT-WITH-NOTES | DRAFT candidates only, cannot submit until a human fills owner/frequency/evidence/verification; "orphan critical = 0" is a target and is reported honestly as `CRITICAL_REQUIREMENT_COVERAGE_UNAVAILABLE` |
| DEV-042 Digital Compliance Passport pilot view | 8 | ACCEPT-WITH-NOTES | source freshness UNKNOWN, `/app/sources/{id}` drill-down, certificates AUTHORIZED_PAGE_ONLY, stays REVIEW_REQUIRED |
| DEV-080 Tug mode context and profile root | 8 | ACCEPT-WITH-NOTES | Ship daily screens filtered without `mode.cross` server + client |
| DEV-097 Noon Report daily operations center | 13 | ACCEPT-WITH-NOTES | reviewed-summary exposes only Master-reviewed hash-bound revision; `voyage_id` UNKNOWN (no voyage module); no emissions formulas |
| DEV-098 Deck log book | 8 | ACCEPT-WITH-NOTES | officer of watch validated against `log_date` incl. re-check on date change |
| DEV-099 Engine log book | 8 | ACCEPT-WITH-NOTES | exact-revision WorkOrder/Defect links, C/E appointment-only; links added after a review are not covered by it (shown REVIEW_REQUIRED); DEV-052 not claimed |

Overall: **S5 ACCEPT-WITH-NOTES — 8 tasks / 69 SP. Blocking findings: none.**

## Open human/expert gates (not claimed, not blockers)
OVERRIDE_AUTHORITY policy and grant holders (60.03 p584); source-freshness policy (54.01); qualified criticality source; voyage master module; production role catalog; Master/Chief Engineer appointments; 65.02 pilot UAT signature; operator activation of offline settings.
