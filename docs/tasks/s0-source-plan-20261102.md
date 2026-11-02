# S0-SOURCE-PLAN-20261102 — DEV-008 / T-S0-008

State: AUTHORED_UNVERIFIED. No plan adoption, legal validation, operational appointment or canonical acceptance is claimed.

## Exact requirement and scope decision

18.01 Backlog row 9 A/G/K/M (SHA `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`) requires source owner, version, validation status and reviewer for the seven ISM/SOLAS/STCW/MLC/MARPOL/Flag/Class pilot categories. Its named deliverable is a plan. [The independent source-scope review](../reports/dev-008-source-scope-review-2026-11-02.md) accepts a reviewed responsible-role plan approach; it explicitly does not approve this new artifact or grant DEV-008 acceptance. It does not impose live named people, tenant memberships or a runtime assignment API on this S0 plan.

08.01 Kaynak Register row 1 identifies Owner as the column populated by external issuers IMO/ILO/UAB/IACS/Class organizations; those values are preserved separately from internal responsibility. 18.01 Backlog row 9 K names Regulatory Lead; RACI rows 1/4 I name Regulatory SME and applicability responsibility A/R. These are controlled role labels, not provisioned identities or competence sign-offs.

Actual later source/version SME sign-off is DEV-073, Backlog row 74, S11; Tug-specific source ownership review is DEV-093, row 94. 00.01 paragraph 116 retains release/final-legal-claim refresh. Open candidate validation questions are not silently solved by plan structure. [Bounded receipts](s0-source-plan-20261102.sources.json) retain original texts and hashes.

## Concrete typed plan

[pilot-ownership.v1.json](../../contracts/sources/pilot-ownership.v1.json) is an internal role responsibility-plan artifact, not a second operational source register. It pins the current candidate baseline and all three controlling workbook/document hashes; each candidate source has exact ID, external issuer, instrument declaration, candidate lifecycle, URL and controlled note references.

| Plan category | Candidate source ID / exact08.01 register row | Internal responsible owner | Reviewing role |
|---|---|---|---|
| ISM | SRC-ISM / 2 | Regulatory Lead | Regulatory SME |
| SOLAS | SRC-SOLAS / 3 | Regulatory Lead | Regulatory SME |
| STCW | SRC-STCW / 4 | Regulatory Lead | Regulatory SME |
| MLC | SRC-MLC / 5 | Regulatory Lead | Regulatory SME |
| MARPOL | SRC-MARPOL / 6 | Regulatory Lead | Regulatory SME |
| Flag | SRC-TR-ISM-2023-19 / 15 | Regulatory Lead | Regulatory SME |
| Class | SRC-IACS-M79 / 17; SRC-TL-OCEAN-TOW / 18; SRC-TL-BP / 19 | Regulatory Lead | Regulatory SME |

Every source version records `CANDIDATE_DECLARATION_AND_SNAPSHOT`, active candidate baseline, exact workbook SHA, row locator and the original instrument declaration. For example “SOLAS 1974 as amended” and “MLC 2006 as amended” are copied as candidate declarations. Flag's2023/19 is in its actual instrument title; Class's2022 ocean-towage snapshot is visible in the exact candidate source URL. No current authoritative edition or amendment is invented: `authoritativeEdition` is null and resolution OPEN. The candidate snapshot hash identifies supplied bytes, never the exact currently applicable law.

Each category has explicit UNVERIFIED validation, OPEN edition/applicability resolution, UNVERIFIED reviewer qualification and blocked activation. Derived plan tracking codes identify authoritative edition, pilot applicability and reviewer qualification work; they are labelled `DERIVED_PLAN_TRACKING_ONLY`, not invented canonical FR/TC/VAL IDs. The next controlled gates are recorded. Flag and Class are conditional candidate sets with selected operational context unresolved/null; this plan does not choose a real pilot flag, class pack or universal applicability rule.

## Acceptance and test mapping

| Trace | Required behavior | Authored evidence |
|---|---|---|
| S0-SOURCE-SCOPE-01 | Complete seven-category inventory and all nine selected source rows; no duplicates or dropped Class sources | Inventory and Class negative cases |
| S0-SOURCE-PIN-01 | Exact active candidate baseline, source paths/hashes/bytes, locator/column/value declarations | Source-reference validator, pin/version/changed-byte negative cases |
| S0-SOURCE-ROLE-01 | Distinct internal owner/reviewer plan roles; preserve separate external issuer | Controlled source-role checks; issuer substitution and same-role negatives |
| S0-SOURCE-VERSION-01 | Actual candidate declarations and typed version metadata; honest unresolved legal edition | Missing metadata, wrong-source version and false authoritative edition negatives |
| S0-SOURCE-STATE-01 | Candidate CURRENT remains a lifecycle assertion; validation and qualification stay UNVERIFIED, activation blocked | Unknown/CURRENT/VERIFIED/APPROVED/null, numeric truthiness and forged approval negatives |
| S0-SOURCE-PRIVACY-01 | No credentials, PII, memberships, invented pilot context or live appointment claims | Closed shapes, person/credential/runtime-context negatives; diagnostics never echo values |
| S0-SOURCE-REVIEW-01 | Independent adoption of this exact artifact is required; source-scope approach review is not artifact approval | Embedded self-approval rejected; validator always reports reviewRequired and accepted:false |

[pilot_source_ownership.py](../../scripts/pilot_source_ownership.py) reads local controlled sources only. It has no HTTP request, persistence write, source publication, legal calculation or runtime activation path. Its definition-valid result is never a review/approval receipt. Strict input loading rejects duplicate keys, nonfinite JSON and oversized input; diagnostics are closed codes.

[test_pilot_source_ownership.py](../../scripts/test_pilot_source_ownership.py) contains seventeen meaningful test methods, including current-source declarations and changed-byte isolation. All validator/test/build/typecheck/browser/QA/CI execution is **DEFERRED / NOT RUN** by this agent. Source retrieval and artifact writing are not verification PASS.

## Adoption and remaining work

The plan revision and exact bytes must receive a genuinely separate engineering/source-plan review. Its review receipt must identify reviewer, target digest/revision, date, decision and limits outside the authored artifact; it cannot reuse the scope-only report as final approval. The current artifact retains `AUTHORED_UNVERIFIED` and an external-review-required marker so structural validation cannot self-adopt it. This validator deliberately does not authenticate an approver or turn a claimed hash into legal authority.

Next: coordinator runs the combined S0 prerequisites gate, then independent exact-target review can adopt or reject this responsibility plan. DEV-007 dependency, actual legal edition/flag/class resolution, qualification and DEV-073/093/release-refresh gates remain separate. The historical free-text proposal is retained under [source-ownership-plan-01](source-ownership-plan-01.md) with current scope clarification. No runtime bindings, source-state mutation, PII, credentials, graph update, shared status edits, push, commit or PR were introduced.
