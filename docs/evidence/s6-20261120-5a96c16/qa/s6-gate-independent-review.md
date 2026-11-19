# S6 sprint gate — independent QA review (5a96c16)

- Target: PR #44 head `5a96c1602d5fd130e475a5120d93069ffb5d4321`, merged into main as `7b0504e4e33fe0b606f75c73dcbe6178edaf33e4`.
- Reviewer: independent QA subagent (did not author S6 code), own detached worktree, 19–20.11.2026.
- CI evidence for 5a96c16 (both `completed`, re-verified by the coordinator with `gh run view` before this record):
  - push run `44606147317` — success
  - pull_request run `44606162275` — success
- Canonical criteria: 18.01 Development Backlog rows 44–47, 82–83, 101–103.

## Rounds
1. **e6a8cdd — NOT ACCEPTED.** DEV-081 CI red: tug job submitted-body timestamp precision (PostgreSQL stores µs, the in-memory copy carried 100 ns ticks). DEV-101/DEV-102 acceptance-criteria gaps (SOF finalization, downtime review lifecycle).
2. **1d0cf43 — WP-D finalize/downtime lifecycle merged.** DEV-101/DEV-102 PG tests red.
3. **b95626a — permission allowlist + LoH test alignment.** Still red: `ShipOpsLifecycleTests.cs:47` (SOF finalize ≠ 200) and `ShipOpsLifecycleTests.cs:81` (downtime `SUBMIT_FOR_REVIEW` ≠ 200) on CI push `44601911669` / PR `44601931922`. Stop limit (2 fix attempts on the same failing step) reached.
4. **5a96c16 — revert of the WP-D lifecycle merge and its fix.** Tree identical to `9242ffa` = `f98bff2` (green) + WP-B vessel/person reference matching. Push `44606147317` and PR `44606162275` green. DEV-101/DEV-102 lifecycle work continues on `feature/s6-wpd-ship-ops-20261119` pending PostgreSQL reproduction.

## Verdicts (5a96c16)
| Task | SP | Verdict | Notes |
|---|---|---|---|
| DEV-043 Certificate Intelligence Center | 13 | ACCEPT-WITH-NOTES | VALID is a date label only; issuer/authority verification is not modelled |
| DEV-044 Renewal workflow v1 | 8 | ACCEPT-WITH-NOTES | separation of duties requires ≥3 distinct people; supersession is immutable (DB triggers) |
| DEV-045 Certificate OCR assist | 13 | ACCEPT-WITH-NOTES | digital PDF text layer only; photo/scan OCR returns an explicit unavailable state (story F46 scope needs a PO decision); vessel/person reference matched on real identifiers |
| DEV-046 OCR golden-set evaluation | 8 | ACCEPT-WITH-NOTES | synthetic 23-document set, in-sample; P=1.000 R=0.853; thresholds re-pinned |
| DEV-081 Tug job request record | 8 | ACCEPT-WITH-NOTES | submitted body copied verbatim, timestamps truncated to µs; cancel reason on an immutable revision; failures classified via `PostgresFailure`; assisted-vessel exact reference open |
| DEV-082 Short/frequent tug job board | 8 | ACCEPT-WITH-NOTES | board filters; assignment/readiness shown Unknown until S7 |
| DEV-100 Fuel/Bunker/BDN/ROB ledger | 13 | ACCEPT-WITH-NOTES | ledger never auto-corrects; `HISTORY_TRUNCATED`, `TANK_REFERENCE_UNAVAILABLE`; supplier/compliance/applicability blocked on sources |
| DEV-101 Port Activity & SOF | 8 | NOT ACCEPTED | lifecycle reverted from the bundle (`ShipOpsLifecycleTests.cs:47`) |
| DEV-102 Operational Downtime / LoH | 8 | NOT ACCEPTED | lifecycle reverted from the bundle (`ShipOpsLifecycleTests.cs:81`) |

Overall: **S6 ACCEPT-WITH-NOTES — 7 tasks / 71 SP of 87. DEV-101 and DEV-102 not accepted. Blocking findings for the accepted set: none.**

## Open human/expert gates (not claimed, not blockers)
Image/scan OCR engine and a real masked certificate corpus; issuer/authority verification; tank, supplier and BDN sources; SOF signature authority; Loss of Hire claim assessment; assisted-vessel reference model.
