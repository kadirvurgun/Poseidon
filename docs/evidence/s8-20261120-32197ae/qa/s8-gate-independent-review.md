# S8 sprint gate — independent QA review (32197ae)

- Target: PR #46 head `32197aeb749222ed26cba05b9a442636d422a222`, merged into main as `aba6e23f1ff4718dabc634e2aac440b6bfa8f2df`.
- Reviewer: independent QA subagent (did not author S8 code), own detached worktree, 20.11.2026.
- CI evidence for 32197ae (both `completed`, re-verified by the coordinator with `gh run view` before this record):
  - push run `44727495974` — success
  - pull_request run `44727506220` — success
  - critical-write coverage gate (pull_request `api-test-counts` artifact, `integration.json`): `PASS_ACTUAL_CURRENT_CRITICAL_SCOPE` 231/231 operations, 216 critical route templates, 0 invalid rows
- Canonical criteria: 18.01 Development Backlog rows 53–58, 88–89, 104, 108–111.
- Delivery: WP-A `4452973` (DEV-052/053/108/109), WP-B `9b3f1f4` (DEV-054/057/110), WP-C `c8e89d5` (DEV-055/103/056), WP-D `780b796` (DEV-087/088/107), integration `fed55e6`.

## Rounds
1. **fed55e6 — NOT ACCEPTED.** B1: postponement, defect lifecycle transitions and service report linking accepted a non-human actor (DEV-109/055/103). B2: PMS Command Center criticality rule could rank equipment without a known criticality (DEV-056). Non-blocking: N1 High-Risk Work ordering, N2 implicit QUIET state.
   Fixes: `6500490` (B1 `human_actor_required` on postponement / defect lifecycle / service report; B2 command-center criticality rule + `EQUIPMENT_CRITICALITY_UNKNOWN`; N1 HRW ordering; N2 explicit `QUIET`).
2. **6500490 — code fixes correct; acceptance tied to CI.** PR run `44715012076` shard 0 ended `INTERRUPTED_DEADLINE` at 1793 s (shards 1–3 at 1657/1771/1787 s; push run `44715002835` green at 1115–1202 s per shard): a capacity limit, not a product defect.
   Fixes: `7638301` (per-test start/end markers for deadline diagnosis), `32197ae` (API gate deadline 1800 → 3000 s).
3. **6500490..32197ae gate-infra commits — ACCEPT-WITH-NOTES.** Notes: `tests/Poseidon.Api.Tests/TestRunMarkers.cs:28` `AppendAllText` I/O error was not caught, so a marker write failure would fail the test (fails closed); deadline headroom is ~1.5x, watch per-shard elapsed time.

## Verdicts (32197ae)
| Task | SP | Verdict | Notes |
|---|---|---|---|
| DEV-052 Equipment hierarchy pilot | 8 | ACCEPT-WITH-NOTES | criticality catalog not supplied |
| DEV-053 Maintenance plan + trigger | 13 | ACCEPT-WITH-NOTES | — |
| DEV-054 Work order execution + evidence | 13 | ACCEPT-WITH-NOTES | required evidence sets per WO type not supplied |
| DEV-055 Critical defect lifecycle | 13 | ACCEPT-WITH-NOTES | human actor required on lifecycle transitions (B1) |
| DEV-056 PMS Command Center | 8 | ACCEPT-WITH-NOTES | unknown criticality surfaces as `EQUIPMENT_CRITICALITY_UNKNOWN` (B2) |
| DEV-057 WO offline execution | 8 | ACCEPT-WITH-NOTES | — |
| DEV-087 Towage operation plan | 13 | ACCEPT-WITH-NOTES | — |
| DEV-088 Towage risk + briefing | 8 | ACCEPT-WITH-NOTES | absent PTW evaluates to `UNKNOWN` |
| DEV-103 Service & Engineer Report linked to WO/equipment | 8 | ACCEPT-WITH-NOTES | human actor required (B1) |
| DEV-107 Fleet High-Risk Work exception view | 8 | ACCEPT-WITH-NOTES | ordering (N1), explicit `QUIET` (N2) |
| DEV-108 PMS measurement point model | 8 | ACCEPT-WITH-NOTES | thresholds tenant-configured only, otherwise `UNCONFIGURED` |
| DEV-109 Work Order postponement governance | 8 | ACCEPT-WITH-NOTES | human actor required (B1); postponement policy / max extension not supplied |
| DEV-110 Low-stock + technical auto requisition | 8 | ACCEPT-WITH-NOTES | ERP export only from `APPROVED_FOR_ERP_EXPORT`; no real ERP integration |

Overall: **S8 ACCEPT-WITH-NOTES — 13 tasks / 124 SP. Blocking findings: none.**

## Open human/expert gates (not claimed, not blockers)
Postponement policy and maximum extension; equipment criticality catalogs; measurement thresholds (tenant-configured only, `UNCONFIGURED` otherwise); required evidence sets per work-order type; PTW source (absent → `UNKNOWN`); ERP export only in `APPROVED_FOR_ERP_EXPORT` state, no real ERP integration.
