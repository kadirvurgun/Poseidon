# Guide bundle 22.11.2026 — USER-ADMIN-02 + UG-2 + UG-3 + guide product fixes

Branch `integration/guide-bundle-20261122` (base main dde4bb50). Canonical count unchanged: 114/121 tasks, 932/970 SP (96.08%). Software-quality and documentation delivery; no pilot gate closed.

## Delivered
- **USER-ADMIN-02** (1d093986): invitation/accept flow closes the cross-tenant identity squatting finding of USER-ADMIN-01. Token `pi1.<tenant>.<secret>` (SHA-256 at rest, 24 h, 54.01 p10025), fragment link, actor-less ≤10 min invitation session, neutral 409 `invitation_unavailable`, one accepted membership per identity. Detail: `USER-ADMIN-02.md`.
- **UG-2** screenshot pipeline (cb096dd8): committed generator, provenance v2 (generator blob + dirty flag), 64 desktop PNGs TR/EN for 6 guides (today, tug-job-board, companies, user-admin, fleet, tug-command). Detail: `USER-GUIDE-01-UG2.md`.
- **UG-3**: 50 remaining page guides TR/EN (tug a511db01, ship 10542c6e, HSE 6c1cdae3); `help-pending` empty, 54/54 navigation keys documented. All `reviewed: false` until human SME review (UG-REV-01).
- **Guide QA**: independent content QA rejected 6 guides (wrong rules / invented features); fixed 4208983d, re-check ACCEPT-WITH-NOTES.
- **Product fixes found while writing guides** (`fix/guide-findings-20261122` 2b2ee89c):
  1. Work order summary copy matches the real execution lifecycle (evidence accepted only by independent VERIFY, ISM 10.2).
  2. Port SOF summary shows server `sof_state` (Draft / Final-internal); unknown values never shown as Final.
  3. Departure readiness explains why Release is unavailable (Emergency source gap 61.02, certificate inventory gap); gate not lowered.
  4. Noon/deck/engine log review must be independent of every revision author (13.03 p35-37/p86-89, 60.01 Rev3 p544-546): blocker `REVIEWER_IS_RECORD_AUTHOR`, 403 `separation_of_duties`, migration `20261124110000_DailyReviewIndependence` trigger.
  5. Tug readiness GO enabled only on READY (60.02 p201).
  6. Renewal duty-separation refusal has its own message.
  Independent QA ACCEPT-WITH-NOTES. N1 (map only the trigger's 23514, not other CHECK constraints) and N2 (stale GO comment) fixed in this bundle.
- Affected guides updated to the new behavior (c71de36c).

## Verification (local)
- Frontend full vitest 231 files / 2316 tests passed; typecheck and lint exit 0.
- Product fixes: build 0/0; Domain 1633/1633; Persistence non-integration 155/155; API non-integration 478; daily review/reference/offline PG integration 74/74 (agent) and 45/45 (QA); after N1 fix DailyReviewTests on PG 21/21.
- CI: fix branch 44982644381 green; UG-2 push 44981338310 green; bundle gate CI pending.

## Open / deferred
- QA notes N3 (renewal refusal text also used when COMPLETE lacks a verifier) and N4 (unknown-SOF test should assert the Unknown label).
- Mobile screenshots; screenshots for remaining 48 guides; offline Playwright help check DEFERRED.
- UG-4 manual generator (HTML/PDF), UG-5 other 15 locales, UG-6 human SME review.
- Invitation: no e-mail delivery, process-local rate limiter, per-tenant `account.invite_hours`, re-homing a bound identity.
- Departure Release stays unavailable until drill records (61.02) and a required-certificate baseline exist.
