# S5 compliance experience — UI source package

Status: **AUTHORED / UNVERIFIED**. S5 remains **0 accepted / 32 SP**. This is a locally reviewable source package, not a completed sprint or release. Test, build, typecheck, lint, browser and generated-contract execution is **DEFERRED until actual PR preparation**, following the user's larger-bundle cadence. No push, PR, runtime, provider or preview changes were made by this author.

## Controlled sources and trace

Active source: [active baseline](../sources/active.json), `poseidon-v46.3-20261030`.
Original 18.01 workbook SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`; original Backlog rows40–43 were read with bounded extraction, not substituted historical/generated requirements.

| Task / trace / original locator | Acceptance and source implementation |
| --- | --- |
| DEV-039 / T-S5-001 / row40 /8SP | Identity/profile/flag/class/type/GT/cargo/trading completeness and critical missing data REVIEW_REQUIRED. A four-step journey retains the actual 20-field profile draft across steps, saves incomplete data using the existing profile command, and displays a server assessment bound to the exact saved profile revision. Vessel code/name remain the owning fleet register's identity; the wizard provides the guarded Back to vessel route rather than duplicating a second identity mutation. |
| DEV-040 / T-S5-002 / row41 /8SP | Rule version, inputs, source, result and reviewer state. Shared reason drilldown now presents exact rule/source/requirement revisions, recorded lifecycle, Unknown predicates, projected activations and integrity hash. Immutable authorized captured facts, knowledge, provenance, source observation, freshness limit and input/rule-set hashes are grouped separately from currentness and human authority. No source-authorized rule override contract exists: manual override remains unavailable; generic admin is not an override appointment. |
| DEV-041 / T-S5-003 / row42 /8SP | Backend-owned current qualified catalog mapping is consumed with requirement→control links, owner/frequency/evidence/verification policies and distinct completion/verification/effectiveness. Empty graph pages do not imply zero critical orphans. Critical coverage is null/unavailable; no zero-orphan acceptance is claimed. |
| DEV-042 / T-S5-004 / row43 /8SP | Scoped Digital Compliance Passport presents current or retained applicability, immutable inputs/reasons, source pins, bounded control/evidence records, blockers and freshness drilldown. Missing authoritative certificate catalog remains UNAVAILABLE, with no fabricated inventory, readiness or completeness score. |

Additional controlled locators read:

- 11.01 matrix `196 Ekran v46.3` rows86/103 (fleet/applicability), SHA256 `bc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9`.
- 14.02 dictionary `Screen Coverage 196/row:86`, `Data Dictionary 2316/rows:251–253`, and `Read Model - Projection Gap/row:36`, SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`. Passport has zero controlled input fields in the original screen metadata; a scoped read projection is explicitly required, with missing/stale critical data never READY. Source placeholders and proposal lookup values are not serialized as approved data.
- 13.02 catalog paragraphs261–266, SHA256 `b0b5e9fb3cad5137d76a4de912c3a60351b667d0aeb08252856e3a9445928b45`, and actual 12.01 HTML prototype metadata `vessel-compliance-passport` (four actions, projection fields not an approved physical model).
- 13.04 design paragraphs29–34,80–89,110–114,124–131, SHA256 `83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c`: context hierarchy, action alignment, Unknown/state text, TR/EN, focus, Why and responsive critical journeys.
- 15.01 passport paragraphs594–595, SHA256 `68955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837`; backend adds the canonical query alias while this UI consumes its coordinated per-vessel route.

MCP graph tools were unavailable. Scoped local Graphify discovery preceded targeted implementation reads. Root will refresh the shared derived AST graph once after normal integration. Architecture, automation and the repository UI skill were read; source proposals remain candidate/unverified.

## Owning behavior and contracts

[Routing](../../frontend/src/product/workspace-location.ts) adds only `/app/fleet/{id}/onboarding` and `/app/fleet/{id}/passport`. Unsupported suffixes, encoded separators and any query parameters on these new application routes are rejected. Existing profile URLs remain intact. [ProductApp](../../frontend/src/product/ProductApp.tsx) routes both views through the existing unsaved-change/save-in-progress/history guards; the selected context, guarded Switch vessel action and sign out remain accessible. Focused workflows suppress unrelated overview charts/list/detail panels. Main heading and document title reflect the active workflow.

[RegulatoryProfile](../../frontend/src/product/RegulatoryProfile.tsx) retains its existing endpoint, version, CSRF and frozen idempotency key/body. All declaration fields remain in memory across steps. The owning profile response's `vessel.canEdit` gates editable inputs and save actions. Uncertain writes freeze both fields and step navigation; retry sends the same captured command. Server errors on a hidden step have a labeled route to that step. Raw server validation text is not displayed. Normal profile mode still exposes the original complete form.

[Compliance consumer](../../frontend/src/product/compliance-api.ts) follows the authored backend [S5 contract](./s5-compliance-core-01.md):

- `GET /api/v1/vessels/{id}/compliance-onboarding`: closed ten-field set, any order; exact vessel and saved revision; Known/Missing/Unknown; all critical fields; `canSubmit:false`, freshness Unverified. Complete declarations remain DECLARED_COMPLETE_UNVERIFIED. Dirty local changes never appear in the saved assessment.
- `GET /api/v1/vessels/{id}/compliance-passport?offset=N`: exact protected projection,25 controls/evidence per page, at most64 requirement rows/25 linked control references. Null unsupported certificate/critical coverage remains visible and distinct from zero.
- Review includes `inputs`, `inputHash`, `ruleSetHash`. Available readable history can be stale (`available:true,current:false`); retained input/result trees remain historical and REVIEW_REQUIRED. Missing evaluation has null capture/hashes. The existing closed owning input snapshot validator is reused, including vessel/profile binding, controlled fact keys and protected provenance shape. Hashes indicate integrity, never approval. `manualOverrideAvailable:false` is mandatory.
- Backend review/control-plan endpoints are available independently; this UI consumes the atomic composed passport rather than assembling projections from separate response moments. Its exact scoped reason anchor is `/app/fleet/{id}/passport#applicability`.

[Passport](../../frontend/src/product/CompliancePassport.tsx) and saved onboarding assessment withdraw their data on vessel/request scope, online/visibility/focus revalidation. Loading, denied/unavailable and offline states remain explicit. No command is auto-sent by viewing or moving between steps. Certificate coverage, source admission, verification, effectiveness, human approval and operational readiness are never inferred from declarations, source hashes or presence of evidence.

The shared [reason presentation](../../frontend/src/product/ApplicabilityReasonView.tsx) also improves the existing lifecycle/applicability screen, preserving its author/review/qualification/history command handlers and frozen retries. [Enterprise styles](../../frontend/src/product/compliance-enterprise.css) use existing tokens, responsive grids, shared action groups and shadcn Button; no dependency or focus/contrast threshold changes.

## Authored regression sources — execution DEFERRED

| Test trace | Meaningful authored coverage | Source |
| --- | --- | --- |
| S5-CONSUMER-01 | Unordered closed field set; duplicate/missing/private fields; Known/Unknown/complete invariants; foreign vessel; no Ready/certificate/override escalation; stale capture and source hashes; private input denial; exact local drilldown and paired source pins. | [compliance-api.test.ts](../../frontend/src/product/compliance-api.test.ts) |
| S5-EXPERIENCE-01 | Saved revision mismatch; unsaved assessment notice; complete declarations still cannot submit; missing coverage stays unavailable; correct guarded navigation; vessel/access withdrawal; offline revalidation; captured facts/hashes; Unknown reason and projected activation wording. | [ComplianceWorkspace.test.tsx](../../frontend/src/product/ComplianceWorkspace.test.tsx) |
| S5-ONBOARDING-01 | Cross-step retained values and exact decimal save through owning endpoint/CSRF/version; uncertain exact key/body replay with navigation frozen; read-only controls; hidden-step accessible validation; real parent back-navigation dirty guard and beforeunload prevention. | [RegulatoryProfile.test.tsx](../../frontend/src/product/RegulatoryProfile.test.tsx) |
| S5-ROUTE-01 | Fleet route roundtrips, preserved legacy profile, invalid company/unknown/encoded/query routes. | [compliance-location.test.ts](../../frontend/src/product/compliance-location.test.ts) |

Synthetic transport fixtures are test-only, with Unknown facts and no admission/verification claims. Root independently authored desktop/mobile wizard browser source and owns later whole-bundle and actual-host checks. No component, contract, browser, API, build, typecheck, lint or generation result is claimed here. Whitespace source review alone is not product validation.

## Open acceptance and next gate

Manual rule override appointment/reason/audit admission remains unavailable, so DEV-040 is not fully accepted. An authoritative vessel certificate catalog, critical requirement universe and zero-critical-orphan proof are not established. Current qualified source data is a bounded catalog, not the full regulatory obligation universe. External expert/pilot signatures and operation readiness remain outside this package's authority. Pagination/truncation wording and final source labels are coordinated by root/backend before acceptance. Independent desktop/mobile visual review, actual actor/file/context revocation journeys, full generated contract/build/component/browser/integration checks and canonical S5 acceptance remain open until the actual PR gate. Root owns canonical status/phase reports; this task document does not advance them.
