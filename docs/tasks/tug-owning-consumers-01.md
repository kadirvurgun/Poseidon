# TUG-OWNING-CONSUMERS-01

Status: **AUTHORED_UNVERIFIED / INCOMPLETE / NOT ACCEPTED**. FOUNDATION product
authoring toward the user's 194 genuinely accepted SP objective. Canonical
8/970SP, 2/121 tasks, 0/196 accepted screens remain unchanged. No local engineering
completion, independent QA, source adoption or release is claimed.

## Receiving and source authority

Received HEAD `96c2901ef5aaccbfbc716e49dec7c7a3456ef4c4`, with an already dirty
Tug owning-capacity continuation. Inspected status/diff and retained all predecessor
code/tests, qualification omission coverage, actual medical/offline exclusions,
Safety normal receipt `d52270f`, regulatory checkpoint `43536c9` and the historical
independent `80b057a6d1830eb6c5fe5e33fc0adfc7ca132c44` baseline. No reset,
preparation-branch merge, duplicate receipt, account/auth/private configuration
change, extra agent or publication. Earlier receiving evidence remains immutable
historical evidence, not current passing checks.

Read AGENTS, backend/UI skills, active.json and architecture/automation/security/
seed/dependency rules. MCP graph tools were unavailable; `graphify query` preceded
targeted code inspection. Bounded `query_sources.py` retrieval preceded original
XLSX rows and DOCX XML paragraphs (including table paragraphs). Baseline:
`poseidon-v46.3-20261030`, `analiz/POSEIDON GUNCEL 30.10.2026`.

| Exact original locator | Requirement / interpretation |
|---|---|
|18.01 Development Backlog TR.xlsx, Backlog81–83, SHA09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674|DEV-080/081/082, T-TUG-001/002/003, each8SP; profile/type/equipment/role, audited intake, planned window/assigned tugs/BP/readiness/conflict/priority/next action|
|Same workbook Backlog84–85|DEV-083/084 are later assignment/Master readiness scope, not a dependency invented for the earlier board|
|14.02 Product Wide Data Dictionary TR.xlsx, Data Dictionary2316:1902–1931,1932–1945,1951–1958, SHA3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018|1935/1936 are system board projections; **1951/1952 are owning mutable planned_start/end**. 1937/1957 are assigned references,1939 available BP,1940 readiness,1941 conflict,1942 priority,1945 next action|
|11.01 Controlled Implementation Traceability Matrix TR.xlsx, 2316 Alan v46.3 corresponding rows, SHAbc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9|FR-/TC-tug-job-board-{planned_start,planned_end,assigned_tugs,available_bollard_pull,readiness_state,conflict_state,priority,next_action}; FR-/TC-tug-job-detail-{planned_start,planned_end}|
|08.01 Regulatory Requirement Traceability Matrix TR.xlsx, Requirement Matrix54; Kaynak Register19|TUG-BP-1 / TC-TUG-BP; current certified/measured BP, certificate/test/date/provenance, Shore planner + Master; current source review remains real|
|10.01 Romorkor Regulatory Applicability Pack TR.docx, paragraphs189–198|Master scalar insufficient; certificate provenance; required BP; source-governed margins; expired/unverified review; multi-tug capability display is separate from final hydrodynamic/safety decision|
|10.02 Romorkor Crew Minimum Manning ve Towing Equipment Spesifikasyonu TR.docx, paragraphs293–299,363–380|Issued vessel MSMD ownership, unverified not READY; source-reviewed certificate age and measured/certified loss restriction; no universally hardcoded5-year/10% production default|
|06.01 PRD SRS Gemi Romorkor TR.docx, paragraphs195–215|Versioned reviewed authority, actual API intake/board/detail; later assignment/readiness remain separate|
|14.01 Product Wide ERD Data Architecture TR.docx, paragraphs95–109; §6/§9|Canonical person/assignment/credential/asset/work ownership and lineage; completion is not verification|
|13.02 196 Ekran Gorselli Kullanim Katalogu TR.docx, paragraphs1064–1083|Distinct intake/board/detail/assignment screens; not one table per screen|
|15.01 Product Wide API Integration Specification TR.docx, paragraphs1533–1554|/api/v1/tug-jobs, /tug-job-board, /tug-jobs/{id}; scoped plan/evidence/offline subroutes are implementation extensions|
|21.01 External Legal Flag Class Signoff Pack TR.docx, paragraphs6–11,18–28|Blank template; vessel/operator/source/date-specific real review; changed edition re-evaluation; no universal class or MSMD substitution|
|19.01 Test Pilot UAT Release Gates TR.docx, paragraphs11–22|No hardcoded unsourced BP/weather limits; tenant/object/job authority; Master authority; AI cannot release/start/abort/complete|
|09.01 Dynamic Applicability Engine Specification TR.docx, paragraphs305–319,446–483|Evidence freshness/verification and scope; GP-TUG-HARBOUR/ESCORT/OCEAN remain distinct; GOLD-003 Unknown→review; GOLD-004 unverified MSMD cannot yield full READY|
|20.01 Pilot Signoff Final Freeze Execution Workbook TR.xlsx, Tug Pilot6|TUG-UAT-05 certified BP/source/date/verification, no unsafe final simple-sum decision|

Documents49/50 proposals are not approved calculation catalogs or permits. The
implemented algebra is an explicitly selected **supported calculation mechanism**,
not a normative maritime rule. Its formula/aggregation/rounding/scale/age/loss and
job-type/area applicability must come from the versioned source qualification
workflow and independent authorized human commands. There is no default derating,
escort norm, approval, production reference record or expert appointment.

## Authored implementation and acceptance cases

| Local trace | Concrete behavior / code / authored tests |
|---|---|
|TOC-BP-01|`TugCapability.cs`, `TugCapabilityCommands.cs`, `TugCapabilityBinding.cs`: incomplete typed certificate/test and equipment-condition evidence in existing owning Evidence commands. Exact current equipment revision/xmin, profile xmin, condition revision/xmin, source locator and document. Composite tenant/vessel FKs; immutable bindings; audited head/revision/binding/receipt transaction. ControlEvidence UI uses real equipment/condition selectors. No request verification flag. Domain, PG and actual-host command tests cover missing data, exact decimals, unsupported context, independent review, expiry, changed profile/equipment/source/authority, race/replay/rollback/history/denial.|
|TOC-SOURCE-02|Versioned `TugCalculationPolicy` travels with exact source bytes and existing RegulatoryQualification commands/history/current controls. Separate human source qualification and independent evidence verification. Required actual verification mechanism remains a blocker until implemented/deployed, never replaced with acknowledgment. `TugCalculationEditor` authors nullable choices and source references; strict wire tests retain incomplete drafts and reject missing/coerced nested fields.|
|TOC-CONSUME-03|`TugOwnedCapacity.cs` consumes current heads only; verifies current qualification, source edition, evidence/control/evaluation lineage, file transport, dates, current human grants through the plan end, exact profile/equipment/condition versions. Only a common exact qualified aggregation policy permits complete assigned-tug total; no partial sum. `tug-planning-api.ts` recomputes decimal total in integer millionths. Positive actual-host/PG test builds qualification/file/control/evidence/review via HTTP commands, not inserted verified rows; fixtures/actors are explicitly fictional.|
|TOC-BOARD-04|Board/detail consume actual assigned-tug BP, current certificate/condition facts, crew assignment/certificate date blockers and equipment/PMS due facts. Missing manning/fitness/rest/competence qualifications remain explicit. Credential document policy is applied before projection/counting. `TugOperationalConflicts` reads recorded operational assignment observations and current visible crew assignment overlaps separately from draft geometry. It never treats DecisionReference text as Master acknowledgment. TR/EN evidence UI exposes provenance/Why/revision/expiry, withholds after denial/offline and refreshes on focus/reconnect/30s. This is a bounded refresh, not push invalidation.|
|TOC-OFFLINE-05|`TugOfflineEndpoints.cs`, `TugOffline.cs`, `tug-offline.ts`, `TugOfflinePanel.tsx`: separate **tug.draft.window.v1** adapter, not a widening of crew watch-group. Genuine authenticated enrollment, nonextractable AES-GCM/P-256 vault, existing atomic encrypted store/quotas, enrolled-key proof, exact frozen signed body, current actor/tenant/mode/job/plan/request/policy/reference checks, server lease/online renewal, restart recovery, revoke/lost-key lock, exact outcome/history/conflict read. Ordinary audited ApplyPlan runs inside the same serializable outcome transaction. Material conflict needs a new reasoned keepServer/useCaptured command, unique parent and current xmin; noops still append revision and consume xmin. Outcome capture time/offset and received time remain distinct.|
|TOC-TEST-06|`TugCapabilityTests.cs`, `TugOwningCapacityTests.cs`, `TugOfflineWindowTests.cs`, `TugCapabilityPersistenceTests.cs`; React calculation/evidence/offline panel tests; strict capability/offline wire tests; signed-host `tug-cases.ts` offline restart/recovery/replay/revoke journey and preserved prior journeys. Public-shell test verifies only exact scoped Tug navigation falls back to integrity-checked public bytes, never API/protected data. All authored, **NOT RUN**.|

Offline source pin is **14.02/11.01 rows1951–1952**, the owning editable times,
with board projections1935–1936. Their specific mutable field contracts and Q
requirements are used together. Q alone does not authorize every record field.
Assignments/lead Tug require online controlled-reference resolution and remain
outside this adapter. Derived available BP/readiness/conflict cannot be captured
as client authority. Actual times and operational transitions belong to later
operational scope; intake/other fields are not silently added. Protected personal/
medical data, raw evidence bytes, binary uploads, approvals and release are excluded.
This narrow adapter does not claim all source offline scope is complete.

Hand-authored migrations `20261102190000_TugCapabilityBindings` and
`20261102200000_TugOfflineWindow` accompany EF models. No snapshot/designer/OpenAPI
generation or application here. Seed adds only an incomplete fictional capability
draft/revision/binding, add-missing and preserving edits. No source-qualified
policy, measured BP, condition factor, clean file, verification, enrolled device,
key, replay outcome or human decision is seeded. Three extra inserts change the
existing100/101 audit expectations to103/104; these are authored expectations.

## Verification, limitations and handoff

**NOT RUN**: restore, build, lint, typecheck, EF/model parity/migration/seed execution,
contract generation, all domain/PostgreSQL/API/React/Python/security suites,
browser/signed-provider journeys, independent QA and CI. No discovered/pass count,
test duration or result hash exists. Source whitespace and Graphify AST maintenance
are the only permitted maintenance observations, recorded in the phase report.

Remaining software is explicit: full crew/MSMD/qualification/fitness/rest/
competence owning evaluators and their current evidence consumers; other required
rule-owning inputs; source-specific formulas/provider mechanisms beyond the
supported algebra; broader source-authorized Tug offline fields; complete
multi-human signed-host BP/source qualification journeys; final generated consumer/
EF model integration reconciliation. Operational Tug assignment acknowledgment,
actual execution-window/state writers and full Master release are later083/084,
not quietly declared implemented by reading old observations. Current source/file/
rights changes are re-evaluated on each request; an atomic cross-module read snapshot
and event-driven client invalidation are not claimed. Real-world deployment source
adoption, certificates, SME coefficients, provider trust and pilot/human safety
decisions are **external inputs**, distinct from those software gaps.

Next: **CREW-FULL-SCOPE-01**, then remaining rule owning inputs and the one combined
FOUNDATION gate, including the outstanding Tug cases/integration. Coordinator owns
target freeze, independent exact-target QA, CI, publishing and merge. This local
UNVERIFIED checkpoint is reviewable authoring, not accepted DEV-080/081/082 or SP.
