# S7 sprint gate — independent QA review (a7b2730)

- Target: PR #45 head `a7b27308905a8a5e6662d6300ba4c88120b2f44c`, merged into main as `021e8edcc136b7565047ae72577bc4438520e8c2`.
- Reviewer: independent QA subagent (did not author S7 code), own detached worktree, 20.11.2026.
- CI evidence for a7b2730 (both `completed`, re-verified by the coordinator with `gh run view` before this record):
  - push run `44644334404` — success
  - pull_request run `44644347908` — success
  - critical-write coverage gate: `PASS_ACTUAL_CURRENT_CRITICAL_SCOPE` 191/191
- Canonical criteria: 18.01 Development Backlog rows 48–52, 84–87, 105–107, 113.

## Rounds
1. **2750d2c — NOT ACCEPTED.** CI red: default-seed audit counts picked up the tow fixtures. Escalated: a vessel could show `MANNING_ACCEPTED` while a crew member was `NOT_ELIGIBLE`.
   Fixes: `12dff7c` (tow fixtures optional; tug GO only on READY per 60.02 p201), `56d205e` (DEV-050 Administration-dispensation rule per SOLAS V/14, IMO A.1047(27), STCW Art. VIII; roster no longer echoes the search term), `570ecaf` (server tug destination labels match the TR/EN catalogs), `9527989` (product fix: vessel Safe Manning state folds a member `NOT_ELIGIBLE`), `d87b99f` (success-path HTTP tests for the 7 unobserved critical writes).
2. **d87b99f — NOT ACCEPTED.** A DEV-050 dispensation could clear non-credential negatives; the risk template edit/suggestion test used an invalid work type.
   Fixes: `e52bfc5` (closed DEV-106 work type in the test), `a7b2730` (a flag dispensation covers certificate gaps only, STCW Art. VIII).
3. **a7b2730 — ACCEPT-WITH-NOTES ×13**, no blocking findings.

## Verdicts (a7b2730)
| Task | SP | Verdict | Notes |
|---|---|---|---|
| DEV-047 Crew roster + profile | 8 | ACCEPT-WITH-NOTES | roster search term not echoed; crew translations machine-authored |
| DEV-048 Crew certificates & endorsements | 8 | ACCEPT-WITH-NOTES | approved STCW/competence catalog not supplied |
| DEV-049 Training-position/competence gate | 8 | ACCEPT-WITH-NOTES | competence catalog pending |
| DEV-050 Crew eligibility + Safe Manning evaluation | 13 | ACCEPT-WITH-NOTES | dispensation covers certificate gaps only; acknowledgements are per reason code, not per member; master/CE post check is English-text based; MSMD records not supplied |
| DEV-051 Crew privacy access regression | 5 | ACCEPT-WITH-NOTES | — |
| DEV-083 Tug assignment + conflict check | 13 | ACCEPT-WITH-NOTES | — |
| DEV-084 Tug operation readiness gate | 13 | ACCEPT-WITH-NOTES | GO only on READY (60.02 p201) → needs comms/weather sources to ever produce GO |
| DEV-085 Tow equipment readiness | 8 | ACCEPT-WITH-NOTES | tow fixtures optional in the default seed |
| DEV-086 Tug crew/towage competence readiness | 8 | ACCEPT-WITH-NOTES | — |
| DEV-104 Pre-Survey Checklist + voyage-fit preparation | 8 | ACCEPT-WITH-NOTES | predecessor DEV-060/061 numbering question open; templates `STARTER_UNAPPROVED` |
| DEV-105 General Risk Assessment workspace | 13 | ACCEPT-WITH-NOTES | no default matrix (50.01 p37/N06); APPROVE is review only |
| DEV-106 Risk Template Library + revisions | 8 | ACCEPT-WITH-NOTES | — |
| DEV-112 Survey Planner fleet calendar + voyage-aware fit | 8 | ACCEPT-WITH-NOTES | fit is never `FIT` without a voyage module |

Overall: **S7 ACCEPT-WITH-NOTES — 13 tasks / 121 SP. Blocking findings: none.**

## Open human/expert gates (not claimed, not blockers)
CrewAuthority and TugOperationsAuthorities appointments; work/rest source (58.04); comms/weather sources; flag-dispensation process owner; approved STCW/competence catalog; MSMD records; published RiskMethod; HSE-G01..G08; approved class/flag pre-survey templates; voyage module.
