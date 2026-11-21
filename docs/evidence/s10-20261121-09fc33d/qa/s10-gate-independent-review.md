# S10 sprint gate — independent QA review (09fc33d)

- Target: PR #48 `integration/s10-bundle-20261121` head `09fc33dfb3bbe1ef56901e4edda84d1dce946949`, merged into main as `6bba4f3b3d994a3a6df00c174155f7b59735f01b`.
- Reviewer: independent QA subagent/worker (did not author S10 code), own read-only detached worktree `.runtime/worktrees/s10-gate-qa`, 20.11.2026. No code changed, nothing pushed.
- CI evidence for 09fc33d (both `completed`, re-verified by the coordinator with `gh run view` before this record):
  - push run `44802159747` — success
  - pull_request run `44802178895` — success
  - critical-write coverage gate (pull_request `api-test-counts` artifact, `integration.json`): `PASS_ACTUAL_CURRENT_CRITICAL_SCOPE` 288/288 operations, 263 critical route templates, 0 invalid rows, 16813 observed rows; integration partition `EXACT_DISJOINT_COMPLETE_MULTISET` (4 shards, 1160 cases); unit scope `PASS_EXACT_DISCOVERED_SCOPE` 421/421
- Canonical criteria: 18.01 Development Backlog rows 65–69, 92–93, 114, 116, 120–121 (S6 addendum: rows 102–103).
- Delivery (merges into the bundle): `3a04251` S9 record, `cca5417` S6 DEV-101/102 redo (`4c41e57`), `135e353` WP-D (`488cc45`, DEV-067/068), `8610dbe` WP-A (`b3bbbfc`, DEV-064/065/113), `3da305f` DEV-115 (`62bf499`), `ea52af5` WP-B (`d8f2b66`, DEV-091/092/119), `812cf00` WP-C (`479cb0a`, DEV-066/120). Integration fixes: `30d5140` (assurance mapping/receipt audit scope Unresolved → Tenant), `65bbc3a` (SIRE test FileHost + clean scanner), `5033aff`/`6ea3a2b` (OpenAPI; conflict center 6 sources), `4880bdc` (AssuranceImport audit revision before/after), `09fc33d` (tug offline human gate).

## Rounds
1. **6ea3a2b — NOT ACCEPTED (2 blockers).** CI red on push `44787515329` and pull_request `44787618820` (same step, deterministic). 9 S10 tasks ACCEPT / ACCEPT-WITH-NOTES; REJECT DEV-092 and DEV-113. S6 DEV-101/102 merge: no regression.
   - B1 (DEV-092): tug offline MaterialConflict resolution had no human-actor gate (`TugOperationsOfflineEndpoints.cs:314-325` replay with `ParentCommandId`, `:407` conflict `choices`). A non-human `tug.write` membership (ActorKind `AI`/service) could resolve its own conflict with `useCaptured`. Other offline modules enforce it (`DailyOfflineEndpoints.cs:241,311`, `WorkOrderOfflineEndpoints.cs:213,285`, `CertificateRenewalOfflineEndpoints.cs:218,297`). No AI-actor negative test (`TugOperationsApiTests.cs:40-123`).
   - B2 (DEV-113, CI red): `AssuranceImport` Modified audit row on decide (`IndustryAssuranceEndpoints.cs:282-284`) carried no before revision/version (`AuditMetadata.cs:1037-1039`); critical-write coverage gate `CRITICAL_WRITE_COVERAGE_FAILED_OR_UNPROVEN` / `AUDIT_BEFORE_AFTER_UNPROVEN`, 2 invalid rows (`S10WpaAssuranceApiTests.cs:42`, decide calls `:86`, `:102`; rejected at `CriticalWriteCoverageCapture.cs:214-215`). All 4 shards `PASS_EXACT_SHARD` 1160/1160.
   Earlier red: `30d5140` push `44777872773` — single failing test SIRE `S10WpaAssuranceApiTests.cs:105` (integration shard 1, 1 of 289), fixed by `65bbc3a` (legitimate: only `Host()` → `FileHost(root, scanner)` + clean `ScanPeer`; asserts 16 → 16; fail-closed product behaviour unchanged).
   Fixes: `4880bdc` (decided imports prove before/after; revision none → 1 on stage, 1 → 2 on decide) — CI push `44794227692` + pull_request `44794237190` green, coverage gate passed; `09fc33d` (tug offline replay with `ParentCommandId` by a non-human → 403 `human_authority_required`; conflict endpoint returns `choices=[]` for non-humans; PG test for AI/Service/System actors).
2. **6ea3a2b..09fc33d re-check — ACCEPT.** B1 and B2 RESOLVED; DEV-092 and DEV-113 ACCEPT. Overall ACCEPT conditional on 09fc33d push + pull_request green — satisfied (`44802159747`, `44802178895`).

## Verdicts (09fc33d)
| Task | SP | Verdict | Notes |
|---|---|---|---|
| DEV-064 Pilot audit-ready pack | 8 | ACCEPT-WITH-NOTES | re-authorised on every read, revoked assignment rejects bytes after awaits (`AuditPackEndpoints.cs:174-187`); unauthorised items only as `Excluded` count; "not an official record" label in API/export/UI; source hash re-check Match/Mismatch/SourceUnavailable; compile human-only |
| DEV-065 Evidence/data quality signals | 8 | ACCEPT-WITH-NOTES | no READY value; material signal → `REVIEW_REQUIRED`; undeclared freshness → UNAVAILABLE, never Current; departure readiness gets UNKNOWN `DATA_TRUST_*` inputs |
| DEV-066 Full pilot offline journey | 13 | ACCEPT-WITH-NOTES | certificate renewal offline edits only DRAFT/PREPARING metadata; signed idempotent replay, differing digest 409; resolution human-only; attachment "Stored" only CleanTransport + checksum; Playwright `pilot-offline-journey.spec.ts` passed in CI |
| DEV-067 Pilot workload performance baseline | 8 | ACCEPT-WITH-NOTES (AC partially evidenced) | tooling only; `Category=Performance` excluded from gate; `slaClaim:false`, `agreedEnvelope.state=NOT_AGREED`; **measurement never ran** on real PG |
| DEV-068 Pre-pilot security gate | 8 | ACCEPT-WITH-NOTES (AC partially evidenced) | no fabricated exceptions; local run `INCOMPLETE` (exit 2), critical 0 / high 0 / medium 49 (41 at WP-D, +8 S10 read routes) |
| DEV-091 Tug operation history | 5 | ACCEPT-WITH-NOTES | filters + keyset paging, unknown key 400; cancelled jobs stay visible; tenant + tug scope |
| DEV-092 Tug job/briefing/execution offline | 13 | ACCEPT (re-check) | B1 resolved in `09fc33d`; idempotent replay, 409 `offline_command_reused`; SERIALIZABLE + `PostgresFailure`; Master release / GO / start / transition never offline |
| DEV-113 SIRE 2.0 import + offline evidence | 13 | ACCEPT (re-check) | B2 resolved in `4880bdc`; importer cannot accept (separation of duties), accept human-only; diff hash re-verified, explicit decision per CHANGED item, ABSENT not applied; findings stay OPEN; non-tanker 409; photo evidence clean SecureFile png/jpeg, VIDEO 422 |
| DEV-115 v46.3 competitor-delta regression pack | 13 | ACCEPT-WITH-NOTES | honest inventory at HEAD: 196 screens → ROUTED 45 / API_ONLY 11 / MISSING 140; 2316 fields; 11/11 new screens ROUTED; `v463-new-screens.spec.ts` 22 cases passed in CI |
| DEV-119 Tug end-to-end buyer demo | 5 | ACCEPT | demo seeds no decision/execution/ack/verification (`TugDemoFixtures.cs:3-8`); opt-in; `tug-demo.spec.ts` passed |
| DEV-120 Ship operations buyer demo | 5 | ACCEPT | read-only, no second entry form; single-entry reuse counted only from server origin pin; "unverified / unsigned" label; `ship-demo.spec.ts` passed |

Overall: **S10 ACCEPT-WITH-NOTES — 11 tasks / 99 SP. Blocking findings: none remaining.**

## S6 addendum — DEV-101 / DEV-102 (accepted via PR #48)
- Redo branch `feature/s6-wpd-redo-20261120` head `4c41e57` (`1d98b5b` re-apply, `c416367` PG row guards for SOF finalize/downtime lifecycle columns, `4c41e57` audit appended lifecycle events without a before revision); push CI `44738600604` green; merged into the S10 bundle as `cca5417`.
- Independent QA on `4c41e57`: **DEV-101 Port Activity & SOF — ACCEPT**; **DEV-102 Operational Downtime / Loss of Hire — ACCEPT-WITH-NOTES** (only "recorder cannot verify/close" is enforced; verifier may equal closer — the AC asks for "verified closure", not counted as a defect).
- S10 gate QA regression check of the merge: no regression — model in sync (`has-pending-model-changes`: none), permissions `operations.port.finalize` and `operations.downtime.review` in TrustedPermissions, `sof_*` / `downtime_*` codes in SecurityEventCodes, S1 counts +2. Final evidence: 09fc33d push + pull_request CI green with `ShipOpsLifecycleTests` in the PG shards.
- Note: migration `20261121040001_S6WpdSofDowntimeLifecycle` has an older ID than the S9 migrations already on main (`20261122*`); applies out of order on an S9-migrated database (no production data). Recorded, not a blocker.

## Non-blocking notes
- DEV-064 (a) pack list endpoint shows other packs' `ItemCount` and `ManifestHash` metadata to any assignment holder (no content). (b) no integration test that a superseded item enters the pack (domain + guard test only).
- DEV-065 (a) `DataQualityPolicy.Floor` unused (dead code). (b) no integration test that departure readiness drops on DATA_TRUST. (c) every profile field UNVERIFIED and freshness UNAVAILABLE → vessels with a profile source stay REVIEW_REQUIRED (cautious; usability impact to product owner). (d) readiness result is actor-dependent (without `control/evidence.read` → DATA_TRUST_UNAVAILABLE).
- DEV-066 attachment bytes held only in memory (durable offline binary closed; CERT-RENEWAL-OFFLINE-CLASSIFICATION gate open); attachment lost on reload, record commands are not.
- DEV-067 `pilot-hardening.yml` is `workflow_dispatch` only and was not on the default branch (`gh run list --workflow pilot-hardening.yml` → 404); `PilotPerformanceBaselineTests` never ran on real PG; "p95 envelope recorded" not met. Pre-pilot condition: first real measurement, then a human-agreed envelope.
- DEV-068 "scans pass" not evidenced for HEAD; missing checks secret-scan-repository, secret-scan-client, control-evidence; run with `--with-clamd` and an API evidence run id before pilot; medium findings (`READ_ROUTE_DENIAL_NOT_STATICALLY_COVERED`) need security owner review.
- DEV-091 (a) outcome filter computed over all executions in SQL, row outcome only from visible tug executions (may disagree under partial visibility). (b) no foreign-tenant vessel test (random GUID 404 only). (c) safety event link honestly `UNKNOWN`.
- DEV-092 no PG test for briefing-ack and execution-event offline paths; no device-key signature (P-256) unlike WO/Daily/Cert offline, only lease + CSRF ("device key policy" gate open).
- DEV-113 (a) capture step does not re-check tanker classification. (b) `AssuranceFixtures` has no test.
- DEV-115 (a) phase report `S10-DEV115-REGRESSION-20261121.md:21` shows pre-integration 42/11/143; HEAD is 45/11/140. (b) `route-snapshot.v1.json` not refreshed after integration; new S10 URLs not under URL-stability guard (`--write-snapshot` pending).
- Cross-checks: 6 new `assurance.*` permissions + 2 S6 permissions in TrustedPermissions; all new failure codes in SecurityEventCodes; S1 route templates 272, critical routes 263, operations 288 match `contracts/security/s1-current-scope.v1.json`; migrations 14-digit and in sync; default seed unchanged, new fixtures opt-in and produce no verified evidence or signatures.

## Commands run by QA (6ea3a2b)
| Command | Result |
|---|---|
| `.tools/dotnet build Poseidon.slnx -c Release` | exit 0, 0 warnings / 0 errors |
| `dotnet test --project tests/Poseidon.Domain.Tests --no-build` | exit 0, 1579/1579 |
| `dotnet test --project tests/Poseidon.Persistence.Tests --filter-not-trait Category=Integration` | exit 0, 132/132 |
| `py scripts/api_test_gate.py --scope unit` | exit 0, `PASS_EXACT_DISCOVERED_SCOPE` 417/417 |
| `dotnet ef migrations has-pending-model-changes` | exit 0, no changes |
| `py -m unittest discover -s scripts -p "test_*.py"` | exit 0, 634 OK (11 skipped) |
| `py scripts/pre_pilot_security_gate.py` | exit 2 `INCOMPLETE`: critical 0 / high 0 / medium 49 |
| `py scripts/regression_inventory.py` | exit 0 `inventoried`: 45/11/140, snapshot regressions 0 |
| `gh run download 44777872773 -n api-test-counts` | diagnosis of the 30d5140 red |

## Open human/expert gates (not claimed, not blockers)
Agreed p95 performance envelope (never measured on real PG; manual `pilot-hardening.yml`); secret-scan exceptions/pin; OCIMF SIRE 2.0 licensed format; finding closure workflow; data freshness policy; durable offline binary capture; demo data stays synthetic; DEV-068 medium findings need security owner review; device key policy for tug offline; SOF signature authority; Loss of Hire claim assessment.
