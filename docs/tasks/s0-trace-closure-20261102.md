# S0 trace closure review — 2026-11-02

Task: DEV-007 / T-S0-007. State: authored validator hardening and source review; execution DEFERRED, not accepted. No canonical task/story-point/screen credit is awarded.

## Controlling source

Active baseline: `poseidon-v46.3-20261030`. [Bounded source receipts](s0-trace-closure-20261102.sources.json).

- 18.01 Backlog row 8 A/F/G/M: DEV-007 requires the ten pilot journeys to have requirement–screen–API–entity–test trace, no critical orphan and backlog/test linkage. Its test ID is T-S0-007; it does not require inventing canonical field identifiers for a zero-field screen.
- 11.01 Pilot Scope v46.2 rows 2–11 A/B/C/D: the actual ten identities, scopes and exit proofs. Row 4 is mode-aware Today; row 10 is safety capture/evidence/audit/offline; row 11 is mixed-fleet daily UAT/value proof.
- Existing current definition forms are retained: Today uses original 14.01 paragraph 6 projection principle and paragraphs 26/34/38 entity names, original DEV-023/T-S2-004; Safety uses events-learning current196 row 95 and all nineteen current2316 rows 424–442, original DEV-060/T-S9-003 in Backlog row 61; mixed UAT composes capabilities 1–9, original DEV-121/T-V463-007 row 122 and all sixteen original Ship/Tug UAT definitions plus 19.01 paragraph 184 value proof.

The earlier report of missing Today/Safety/mixed-fleet source definitions was stale. The actual machine package already has 149 field chains across eight field-bearing screens plus the closed Today projection and mixed-fleet composition forms. No replacement fields, invented FR/TC IDs, alternate empty Inbox or new one-screen mixed-fleet shortcut were introduced.

## Concrete defects addressed

| Trace | Static defect | Authored correction and negative case |
|---|---|---|
| S0-TRACE-ID-01 | Python boolean/integral-float identities compare equal to integers; `True` could alias capability 1, while a noninteger could reach row arithmetic. | Exact integer type/range required before capability processing. Synthetic regression covers True, 1.0, string and null; current-source regression covers True with otherwise genuine source references. |
| S0-TRACE-DEV-TC-01 | Backlog ID and backlog test were individually source-resolved but could point to different genuine source rows. | Both references must retain the same source and locator. Regression pairs Safety DEV-060 with the real but unrelated DEV-020 test. |
| S0-TRACE-FIELD-01 | The chain's descriptive `field` could be relabelled while its FR/TC references remained genuine. | Exact original column D value must match on the same field row. Regression changes the field label without changing legitimate FR/TC references. |
| S0-TRACE-EXIT-01 | Ordinary field-bearing capability exitProof was not resolved against original Pilot Scope column D. | Every source-addressed capability resolves its exact exit proof. Regression replaces Safety's source proof with an unsupported approval claim; implementation/execution gaps remain and acceptance stays false. |

Five additional regression methods are authored in [test_pilot_trace.py](../../scripts/test_pilot_trace.py); [pilot_trace.py](../../scripts/pilot_trace.py) contains the narrow fail-closed checks. The machine trace definition, 149 chains, existing projection/composition forms and all declared implementation/execution gaps are unchanged.

## Independent QA schema-fallback correction

Independent QA identified a second concrete bypass: changing/removing `schemaVersion` and deleting `sourceIdentity` selected the legacy generic-graph branch, whose PLANNED nodes did not resolve the canonical field-chain inventory. The public validator and CLI now require exactly `poseidon.pilot-trace.v1` before any graph processing. The four expected source keys and their path/hash pairs must match the active `poseidon-v46.3-20261030` manifest; original source-byte validation still follows. Every current capability must have sourceIdentity regardless of attacker-controlled schema metadata, and missing identity cannot select the generic-graph branch.

The old synthetic graph-invariant tests now explicitly call a private `_validate_legacy_graph` fixture helper. Public validation and the CLI have no legacy mode, environment switch or fallback. Duplicate nodes, broken references, disconnected paths, orphan inventory and missing execution evidence checks remain intact.

Five additional regression methods cover public rejection of the legacy fixture, missing/unknown/nonstring schemas combined with stripped identities and weak PLANNED graph replacement, exact-v1 identity stripping, active-manifest pin mutation, and CLI nonzero/fixed diagnostic behavior for an unknown schema. They are authored, **not executed**. Existing current package/source definitions and 149 chains were not modified by this correction.

## Real implementation reconciliation

Today currently maps `/api/v1/poseidon-today` and `/api/v1/why-engine` in [TodayEndpoints.cs](../../backend/Poseidon.Api/TodayEndpoints.cs). Its actual tug adapter [TodayTugReader.cs](../../backend/Poseidon.Api/TodayTugReader.cs) reads scoped TugJob and current draft-plan revisions, rejects hidden assigned tugs and derives explicitly unverified planning observations. [TugStorage.cs](../../backend/Poseidon.Persistence/TugStorage.cs) owns actual TugJob persistence. [TodayTugTests.cs](../../tests/Poseidon.Api.Tests/TodayTugTests.cs) authors empty-source, current-owner revocation, paging and scoped explanation cases. These are authored implementation pointers, not current passing tests, operational release or a complete VoyageContext consumer. The source backlog `/api/v1/today` alias remains distinct from the actual endpoint.

The existing partial Today inventory names RegulatoryDraft for its earlier draft advisory path; this review does not reclassify that partial inventory as the complete mode-aware entity model. The retained projection definition correctly names Vessel/VoyageContext/TugJob without claiming all three are implemented.

No current Safety event capture handler/entity/UI or Safety-specific offline adapter was found in the scoped current code discovery. Its nineteen source definitions therefore remain DEFINED, with explicit implementation gap. Crew watch-group offline capture does not satisfy safety capture. Mixed-fleet operational UAT, measured benefit and signatures remain unexecuted and unavailable. No historical embedded runtime is counted as code or evidence.

## Verification and remaining gates

All validator execution, unit suites, builds, generation, browser, independent QA and CI remain **DEFERRED / NOT RUN** for this subpackage. This is static source/code review and test authoring only; no no-orphan PASS is claimed. The coordinator may run the combined S0 gate after all prerequisites are ready.

No new source-definition blocker was discovered for the ten-capability definition candidate. Independent BA/QA semantic review of the existing projection/composition associations remains necessary; software cannot grant that approval. The actual runtime, pilot decision, offline safety, source-owner and measured-value gaps remain separately declared and must not be erased to close S0 trace bookkeeping. No push, commit, PR, merge, graph update or shared status edit was performed by this agent.
