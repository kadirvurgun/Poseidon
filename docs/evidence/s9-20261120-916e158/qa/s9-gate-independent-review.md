# S9 sprint gate — independent QA review (916e158)

- Target: PR #47 head `916e158888180656eab32ba899deafca474013c7`, merged into main as `f1c2d513978fffa232a00d7a2edbae56ec0d46af`.
- Reviewer: independent QA subagent (did not author S9 code), own read-only detached worktree, 20.11.2026.
- CI evidence for 916e158 (both `completed`, re-verified by the coordinator with `gh run view` before this record):
  - push run `44766423323` — success
  - pull_request run `44766440442` — success
  - critical-write coverage gate (pull_request `api-test-counts` artifact, `integration.json`): `PASS_ACTUAL_CURRENT_CRITICAL_SCOPE` 271/271 operations, 251 critical route templates, 0 invalid rows; integration partition `EXACT_DISJOINT_COMPLETE_MULTISET` (4 shards, 1147 cases)
- Canonical criteria: 18.01 Development Backlog rows 59–64, 90–91, 112, 115.
- Delivery: WP-A `e802d19` (DEV-058/059), WP-B `660720f` (DEV-060/061), WP-C `cac2a0e` (DEV-062/063/114), WP-D `fc0cb69` (DEV-089/090/111), integration `c678218` (safety → departure readiness input, voice → safety event NAMED DRAFT hand-off, migration `20261122050000`).

## Rounds
1. **c678218 — NOT ACCEPTED.** CI red on push `44741104744` and pull_request `44741180574` (same 4 tests / 5 cases, deterministic). 7 tasks ACCEPT-WITH-NOTES on code review; REJECT DEV-061, DEV-089, DEV-114.
   - B1 (DEV-114): ISPS-restricted controlled-document file stages leaked name/size/SHA-256 through generic vessel file list/detail; the access route answered 403 instead of 404, confirming existence.
   - B2 (DEV-061): route parameter `{action:guid}` on the safety action step collided with the reserved `{action}` command-variant placeholder of the S1 / endpoint-profile security harness (`S1ScopeCoverageTests.cs:180`, `EndpointProfileRegistryTests.cs:332`), stopping S1 coverage at an exception.
   - B3 (DEV-089): test `FieldActor` crew actor kept the Tug Master appointment (`FieldOperationsApiTests.cs:99`), so no API test proved a non-Tug-Master cannot start/transition/abort.
   Fixes: `bd3ff2f` (`{action}` → `{actionId}` + contract guard; crew actor without Tug Master appointment), `8fabe42` (B1 ISPS files hidden on generic routes, 404 on access; tug authority negatives for crew/AI), `fb9d64a` (towage start requires a GO whose AssessedState is READY; HTTP start success path), `916e158` (typed stateful voice fixture).
   CI in between: `bd3ff2f` push `44750185306` green, pull_request `44750199203` red (frontend voice hand-off test race, fixed by the stateful fixture); `fb9d64a` push `44758751409` / pull_request `44758765486` red only in Lint/typecheck (`S9Workspaces.test.tsx:92` TS2322), all PG shards and the coverage gate passed.
2. **c678218..916e158 re-check — ACCEPT.** B1, B2, B3 RESOLVED; N2 (towage READY check at start) SOUND; DEV-061/089/114 ACCEPT-WITH-NOTES. Overall ACCEPT conditional on 916e158 push + pull_request green — satisfied (`44766423323`, `44766440442`).

## Verdicts (916e158)
| Task | SP | Verdict | Notes |
|---|---|---|---|
| DEV-058 Departure readiness aggregation | 13 | ACCEPT-WITH-NOTES | any FAIL → NOT_READY, missing category → UNKNOWN `SOURCE_MISSING`; emergency and certificate inventory always UNKNOWN |
| DEV-059 Master departure decision | 8 | ACCEPT-WITH-NOTES | server-provisioned human Master; RELEASE only on CURRENT + READY (policy + DB CHECK); RELEASE unreachable until emergency source exists |
| DEV-060 Safety event / near-miss capture | 8 | ACCEPT-WITH-NOTES | confidential reporter null by DB CHECK; vault access human + purpose + reason, append-only |
| DEV-061 Analysis → barrier/action → effectiveness | 13 | ACCEPT-WITH-NOTES | B2 resolved; separation of duties on triage/review/verify/effectiveness/closure; Closed ≠ Effective (N1) |
| DEV-062 Voice-to-event draft | 8 | ACCEPT-WITH-NOTES | human confirm/reject; hand-off only as NAMED DRAFT, no submit (N7) |
| DEV-063 Voice draft evaluation | 5 | ACCEPT-WITH-NOTES | 12-note masked synthetic golden set, critical fabricated = 0 |
| DEV-089 Towage execution | 13 | ACCEPT-WITH-NOTES | B3 resolved; start needs current GO on READY, appointed human Tug Master only; verifier ≠ starter/finisher (N2, N4, N10) |
| DEV-090 Tug daily log | 8 | ACCEPT-WITH-NOTES | idempotent + ExpectedVersion; reconciliation advisory; offline single in-memory command (N5) |
| DEV-111 Drydock–cert–survey–WO link | 8 | ACCEPT-WITH-NOTES | revision-pinned links through owner gates; readiness = linkage completeness only (N9) |
| DEV-114 Document compare + security controls | 8 | ACCEPT-WITH-NOTES | B1 resolved; compare = metadata + SHA-256 only (N8) |

Overall: **S9 ACCEPT-WITH-NOTES — 10 tasks / 92 SP. Blocking findings: none remaining.**

## Non-blocking notes (from round 1, still open unless marked)
- N1 Safety: investigation save/submit and action create do not require a human actor; "root causes are human statements" is enforced only through the human review gate.
- N2 Towage: start relied on the hash without a separate READY check — **resolved in `fb9d64a`** (re-check: SOUND). Residual: DB CHECK still allows a CONDITIONAL GO row (`TugReadinessStorage.cs:210`).
- N3 Departure × safety: an open LOW/MEDIUM event with UNKNOWN investigation need → `NO_OPEN_HIGH_SEVERITY_EVENT` PASS while coverage is `SeverityPolicyUnconfigured`; review when the severity policy gate closes.
- N4 Towage timeline events need only `tug.write`, not a human actor (cannot start or abort).
- N5 Daily log: offline is one in-memory command, lost on reload; server always reports `SYNCED`; durable capture unregistered (60.02 p284). Review table has no reviewer ≠ author DB CHECK.
- N6 Departure: assessment read before the SERIALIZABLE tx; a later source change shows as STALE.
- N7 Voice: a candidate left in `SAFETY_EVENT_REPORT_PERMISSION_REQUIRED` cannot be linked later (no retry route); voice-created safety drafts audit as `VoiceDraftCommand`.
- N8 DEV-114: version diff compares metadata and SHA-256 only (58.02 §33.3 forbids text/OCR diff); acknowledgement does not require an earlier access record.
- N9 Drydock: equipment existence check reads `db.Assets` without `equipment.read` (vessel-scoped).
- N10 Towage: the start gate refuses only a Cancelled job.
- Residual: critical-write branches without a success-path PG test (safety triage RETURN/resubmit, review RETURN/REJECT, VERIFY NOT_VERIFIED, close from TRIAGED; towage ABORT, verify REJECTED; daily log RETURN; drydock CANCEL/REPLAN/REJECT; document content-access DOWNLOAD; departure RELEASE unreachable). Coverage gate 271/271 passed. Confidential reporter linkage through EF audit actor/receipts is readable with `audit.read` (SAFETY-PRIVACY-AUDIT-LINKAGE gate). Round-1 local script suite had 2 non-reproducing failures in 1 of 3 runs (names not captured).

## Open human/expert gates (not claimed, not blockers)
Conditional departure release policy; statutory certificate inventory; emergency/drill source (absent → no vessel can be READY, RELEASE unreachable); tug weather source (towage start unreachable in product); severity policy; anonymous intake gateway; confidential reporter audit linkage (privacy owner); local STT engine + DPIA; controlled-copy print renderer; drydock scope catalogue; durable offline tug log capture.
