# S11 sprint gate — independent QA review (e0639fa7)

- Target: PR #49 `integration/s11-bundle-20261122` head `e0639fa7a720639b5eb4a2c884624053542cfc19`, merged into main as `c6a7036f9e71c0b792346da0d6e343b1f3d2539d` (21.11.2026 03:17Z).
- Reviewer: independent QA subagent/worker (did not author S11 code), own read-only detached worktree `.runtime/worktrees/s11-gate-qa`, 21.11.2026. No code changed, nothing pushed, Docker not started. Round 1 source: `scheduled/workers/s11-gate-qa.result.md`; round 2 re-check recorded in `docs/claude-handoff.md` (claude/loop `5b2e7ce2`).
- CI evidence for e0639fa7 (both `completed`, re-verified by the coordinator with `gh run view` before this record):
  - push run `44856983692` — success
  - pull_request run `44856990162` — success
  - critical-write coverage gate (pull_request `api-test-counts` artifact, `integration.json`, coverage run `41a2b949ad434e5e93685acc09501b85`, PR merge-ref target `140a7661`): `PASS_ACTUAL_CURRENT_CRITICAL_SCOPE` 303/303 operations (288 S10 + 15 S11), 278 critical route templates, 0 invalid rows, 16980 observed rows, `missing_operation_ids` empty; integration `PASS_EXACT_DISCOVERED_SCOPE` 1169/1169, partition `EXACT_DISJOINT_COMPLETE_MULTISET` (4 shards 293/292/292/292); unit scope `PASS_EXACT_DISCOVERED_SCOPE` 460/460
- Canonical criteria: 18.01 Development Backlog rows 70–76, 94–95, 118–119.
- Delivery (merges into the bundle): `3e82cca5` S10 record (`e92d562`), `63ad66de` status reconcile (`38284bb`), `d9e1294d` WP-A (`310d77c`, DEV-069/070), `6fb09be5` WP-C (`b9bb93f`, DEV-073/093), `ee3fa670` WP-B (`fc8073c6`, DEV-071/072/117/118), `53dd7845` WP-D (`212e0aea`, DEV-074/094/075; real pilot gate readers `PilotGateOwnedSources`). Integration fix: `e0639fa7` (telemetry producers wired, GO/CONDITIONAL_GO only when all gates PASS + P0=0 else 422 `pilot_gate_deferred`, vessel without review record → UNKNOWN, S11-WPB phase report, `NotificationTests` time-bomb fix).

## Acceptance scope — SOFTWARE ONLY
S11 is accepted for the software deliverables only. Every human/operational part of the S11 acceptance criteria remains an **open gate** (listed below). S11 acceptance does **not** mean pilot readiness: the go/no-go engine cannot produce GO today because SECURITY, OFFLINE, DR and TRACE gates are structurally DEFERRED, and no owner/SME/UAT/Product Owner sign-off exists.

## Rounds
1. **53dd7845 — NOT ACCEPTED (1 blocker + process).** 10 tasks ACCEPT-WITH-NOTES (software scope); DEV-072 REJECT.
   - B1 (DEV-072): no instrumentation — `PilotTelemetryQueue` (`frontend/src/product/pilot-telemetry.ts:102`) was created only in tests; no journey/AI correction/sync conflict/evidence search producer; backend `PilotMetricBatches.Add` only on the POST path (`PilotMetricsEndpoints.cs:189`). Metrics would remain UNKNOWN permanently in pilot use.
   - B2 (process): pull_request run `44851819969` was still in progress at QA time; it later finished **red** (push `44827968239` on the same commit was green). Diagnosis: `NotificationTests.cs:291` date time bomb — fixed fake clock 2026-11-19 mixed with a fixture computed from the real date. This is a time-dependent defect, not a concurrency race; fixed in `e0639fa7` (fixture anchored to the fake clock), no assertion relaxed.
   - Notes N1 (DEV-075: `PilotGateEngine.cs:209-210` allowed CONDITIONAL_GO with DEFERRED gates, contradicting AC "gates PASS"), N2 (REGULATORY/DATA readers ignored pilot vessels without a campaign/report), N3 (DEV-073 closed campaign not reopened on later source revision), N4 (DEV-074 design conformance only checked when `RequireDesignConformance=true`), N5 (missing `docs/phases/S11-WPB-*.md`; status `as_of` mismatch), N6 (pilot metric complementary suppression single-dimension only).
2. **53dd7845..e0639fa7 re-check — ACCEPT.** B1, N1, N2, N5 RESOLVED: `PilotTelemetryHost` mounted in `main.tsx` (canContribute + opt-out, 5 metric call sites); GO/CONDITIONAL_GO require all gates PASS and P0=0, otherwise 422 `pilot_gate_deferred`; vessel without a review record → UNKNOWN; `docs/phases/S11-WPB-20261122.md` added. DEV-072 and DEV-075 PASS. Minor notes: role adoption measured only on the landing screen; 2 of 9 journeys wired. Overall ACCEPT conditional on e0639fa7 push + pull_request green — satisfied (`44856983692`, `44856990162`).

## Verdicts (e0639fa7) — software scope
| Task | SP | Verdict | Notes | Open human gate |
|---|---|---|---|---|
| DEV-069 Pilot data import templates | 8 | ACCEPT-WITH-NOTES | row-level insert-only `ImportProvenance` in the same audited commit; paged provenance ≤100; no ExecuteUpdate/ExecuteDelete/raw SQL/IgnoreQueryFilters in diff; RowNumber assumes a header row | — (real pilot data import happens in pilot weeks) |
| DEV-070 Pilot data reconciliation report | 5 | ACCEPT-WITH-NOTES | source = accepted + rejected + notCommitted, duplicate ⊆ rejected (DB CHECK); UNKNOWN stays UNKNOWN; sign-off human + assigned data owner only, author ≠ signer (DB CHECK), uploader separated; new import → SUPERSEDED; no seeded signature | **data owner sign-off** |
| DEV-071 Pilot backup & restore rehearsal | 8 | ACCEPT-WITH-NOTES (AC partially evidenced) | `dr_backup.py`/`dr_restore.py`/`dr_rehearsal.py`: libpq env only, fixed stage codes, no secrets in output; RPO/RTO measured only by a real run; `rollbackProcedureSignOff: OWNER_SIGNATURE_PENDING`, `acceptance: NOT_ACCEPTED`; runbook signature empty; **rehearsal never ran** | **real DR rehearsal, measured RPO/RTO + target approval, signed rollback procedure** |
| DEV-072 Pilot metrics instrumentation | 5 | ACCEPT (re-check) | B1 resolved in `e0639fa7`; privacy-safe schema (closed codes, no identity/free text, tenant-salted bucket), <5 contributors → SUPPRESSED, no observation → UNKNOWN never 0; role adoption only landing screen; 2 of 9 journeys wired | retention period/policy ref (DPO), MinContributors=5 approval |
| DEV-073 Pilot regulatory SME review | 8 | ACCEPT-WITH-NOTES | items start REVIEW_REQUIRED; stale pin → REVIEW_REQUIRED; CONFIRMED only by human, non-author, VERIFIED-qualified assigned reviewer; ambiguity needs tracked issue; closed campaign not reopened on later revision (N3) | **SME review/sign-off**, reviewer qualification verification |
| DEV-074 Pre-pilot role UAT | 8 | ACCEPT-WITH-NOTES | S-UAT-01..06 verbatim + locator; missing threshold → THRESHOLD_UNSET → UNKNOWN; only runs confirmed by a second human count; open BLOCKER → FAIL, BLOCKER cannot be ACCEPTED_RISK; best state `CRITERIA_MET_PENDING_HUMAN_GO`; design conformance conditional (N4) | **UAT threshold agreement, participant runs, second-human confirmation** |
| DEV-075 Pilot go/no-go review | 3 | ACCEPT (re-check) | 9 gates, one source each, missing/duplicate → UNKNOWN; engine never emits GO; default `NO_GO_RECOMMENDED`; decision human Product Owner only, review hash + sequence, AI 403; GO/CONDITIONAL_GO only with all gates PASS + P0=0 (422 `pilot_gate_deferred`); SECURITY/OFFLINE/DR/TRACE fixed DEFERRED (`PilotGateSources.cs:74-79`) → **GO impossible today** | **Product Owner go/no-go decision**, support/on-call evidence, approved record path for SECURITY/DR/OFFLINE/TRACE |
| DEV-093 Tug regulatory source/applicability review | 8 | ACCEPT-WITH-NOTES | authority labels + owner matrix; LIMIT_SOURCE_MISSING until source + edition; manufacturer/industry = GUIDANCE_NOT_LAW; class/type approval/"certified" claims 400 `unsupported_approval_claim` | **SME sign-off**, flag/class/port/company source ownership and editions |
| DEV-094 Tug role UAT | 8 | ACCEPT-WITH-NOTES | separate TUG scope T-UAT-01..10, TUG_MASTER/TUG_OPERATIONS_RESPONSIBLE/TUG_CREW, blocker=0 rule | **tug UAT threshold + runs** |
| DEV-117 PMS type approval roadmap | 5 | ACCEPT-WITH-NOTES | DRAFT, "not a certificate/approval claim", target class UNSELECTED, gap + owner, evidence path; `validate_roadmaps.py` valid | **class selection + roadmap approval** |
| DEV-118 ISO 27001/27701 readiness roadmap | 5 | ACCEPT-WITH-NOTES | ISMS/PIMS scope, control/evidence owners, provider/tenant/data boundaries, audit readiness NOT_READY, no GDPR/KVKK claim | **roadmap approval (Security Owner + PO)** |

Overall: **S11 ACCEPT-WITH-NOTES (software scope) — 11 tasks / 71 SP. Blocking findings: none remaining. Open human gates: as listed; none claimed.**

## Commands run by QA (53dd7845, round 1)
| Command | Result |
|---|---|
| `.tools/dotnet build Poseidon.slnx -c Release` | PASS, 0 warnings / 0 errors |
| Domain.Tests (non-integration) | 1613/1613 PASS |
| Persistence.Tests (non-integration) | 144/144 PASS |
| Api.Tests (non-integration) | 459/459 PASS |
| `dotnet ef migrations has-pending-model-changes` | no changes |
| `py -m unittest discover -s scripts -p "test_*.py"` | Ran 651, OK (skipped=11) |
| `py scripts/validate_roadmaps.py` | valid |
| diff grep ExecuteUpdate/ExecuteDelete/FromSql/ExecuteSql/SqlQuery/IgnoreQueryFilters | 0 |
| `py -m unittest discover -s scripts -p "test_project_status*.py"` | Ran 7, OK |

Cross-checks: 13 new permissions in `TrustedPermissions`; new action/result codes in `SecurityEventCodes`; every S11 `Fail(...)` code registered; migrations `20261124061000/062000/063000/064000` 14-digit and ordered after S10; default seed unchanged, new fixtures opt-in (`ImportReconciliationFixtures`/`RegulatoryReviewFixtures`/`UatFixtures`) with no signature/approval/decision. Frontend tests not run locally by QA; CI frontend-and-browser green.

## Non-blocking notes
- N3 DEV-073 a source revision after closure does not reopen a CLOSED campaign; gate may stay PASS.
- N4 DEV-074 design conformance only enforced when the threshold requires it; AC says unconditionally reviewed.
- N6 pilot metric complementary suppression works within one dimension only (AREA × JOURNEY differencing not evaluated).
- DEV-069 RowNumber = i+2 assumes a header row.
- DEV-072 role adoption only on the landing screen; 2 of 9 journeys wired.
- `NotificationTests` time-bomb class: follow-up scan of other tests mixing fixed fake clocks with the real date.

## Open human/operational gates (not claimed, not blockers for software acceptance; blockers for pilot)
DEV-070 data owner sign-off; DEV-071 real DR rehearsal, measured RPO/RTO + target approval, signed rollback procedure; DEV-072 retention policy (DPO) and MinContributors approval; DEV-073/093 SME review/sign-off, reviewer qualification, source editions; DEV-074/094 UAT thresholds, participant runs, second-human confirmation, blocker removal; DEV-075 Product Owner go/no-go decision, support/on-call evidence, approved record path for SECURITY/OFFLINE/DR/TRACE (currently DEFERRED → GO impossible); DEV-117 class selection + roadmap approval; DEV-118 roadmap approval. No signature, approval or GO was produced or claimed.
