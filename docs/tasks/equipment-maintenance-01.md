# EQUIPMENT-MAINTENANCE-01

Status: AUTHORED_UNVERIFIED, NOT ACCEPTED. Execution is DEFERRED by the 01.11.2026 product-first direction. This is a local implementation checkpoint within the larger foundation bundle, not a completed canonical sprint or release gate. Coordinator owns publishing, fresh independent exact-target QA and required CI.

## Reconciliation and authority

Receiving HEAD was `9c94f66`; the worktree was clean. Crew and earlier modules were preserved. No reset, force push, publication, dependency update, scanner work or extra agents. Read AGENTS.md, backend/UI skills, active.json, architecture, automation, security, audit, seed and dependency contracts. The supplied `docs/reviews/reg-boundary-tls-01-80b057a6d183.json` records approved local engineering scope for PR11, not acceptance of this task.

Ordinary ordered `cherry-pick -x` integrated the exact support origins:

1. `dea9169d0eba123bf3d5cf469af3e97de68f5dc5` → `3cae07d` (domain policy and authored tests).
2. `fa58d3ddb35f67970c75d8055fcc93a8a37c59a3` → `4f8466a` (actual hierarchy tree and 15 authored component cases).
3. `a6d6bd30d6e9ffdbabc9b5313262064a5e1af79d` → `ed9e9e1` (exact operator load-error copy).

No support commit or origin-equivalent patch was present before integration. Their authored status remains UNVERIFIED. The policy is called by the real Equipment mutation; the tree is imported by the real workspace and fed its complete authorized API snapshot.

## Sources and dependencies

Baseline `poseidon-v46.3-20261030`, root `analiz/POSEIDON GUNCEL 30.10.2026`. Retrieval used bounded `py -3.13 -X utf8 scripts/query_sources.py ... --limit 4` and bounded reads of cited active locators; no whole chunk file entered context. MCP graph tools were unavailable; `graphify query` located the existing persistence/API/UI integration points. Graph is derived, not source authority.

| Source | Locator and task use |
|---|---|
| 18.01 Development Backlog | Backlog rows53/54: DEV-052/T-S8-001 (8SP, DEV-020), DEV-053/T-S8-002 (13SP, DEV-052). No SP acceptance change. |
| 14.01 ERD | paragraphs45–48,104–109: shared Technical domain, Asset fields/hierarchy, WorkOrder distinct completion/verification. |
| 11.01 Traceability | v46 Delta rows3/6: equipment-hierarchy and maintenance-plan screen/service names. |
| 14.02 Dictionary | Screen Coverage196 rows140/143 inventory; **Data Dictionary2316 rows1352–1367 and1406–1425** actual field contracts. Inventory alone is not the field contract. |
| 14.02 Lookup resolution | rows300–303: equipment→Asset; PMS job is explicitly NOT TugJob; Role reference selection/physical mapping still requires review. |
| 13.01 Roles | paragraphs57–59 and field-contract V columns: Chief Engineer/Technical Manager/Master context; no AI safety authority. |
| 13.02 Screens | paragraphs855/873,1895–1897 equipment-hierarchy / maintenance-plan entries; 15.01 paragraphs1287–1313 provide matching service names. |
| 13.04 Design | paragraphs10–24: role-first, explicit blockers, explainability, human authority, progressive details. Existing React/CSS foundation retained. |
| 16.01 OpenAPI skeleton | lines1601–1680: normalized `/maintenance-plans` resource alias, draft persistence not execution approval. |
| 19.01 Tests | paragraphs78–83: S-UAT-03 critical PMS/defect evidence and verification; not satisfied by a draft calculation or component test. |
| 18.01 postponement | Backlog row110 DEV-109/T-PMS2-003: authority, reason, risk review, new due, criticality, readiness recalculation, audit and maximum extension policy; depends DEV-055/DEV-105. |
| 20.01 Pilot | Ship Pilot row12 SHIP-UAT-11: critical overdue/postpone must not silently become green. Historical NOT RUN remains NOT RUN. |
| 01.03 ISM guide | paragraphs445–446,516–519: PMS criticality is linked to defect, standby test, safeguards and Chief Engineer/technical context. |
| 10.03 towing pack | Towing Equipment Rule Pack row15 EQ-PMS-01: maintenance alone does not establish tug equipment readiness or Chief Engineer/Master sign-off. |

## Implemented behavior

One `technical.Assets` aggregate owns System/Equipment/Component nodes with versioned maker/model/serial/location/parent and draft declarations. Composite tenant/vessel FKs reject cross-scope parents, owners, jobs, plans and revision lineage. Mutable Asset/Plan use PostgreSQL xmin and logical revisions. History/reference tables have EF guards and PostgreSQL mutation/truncate rejection. All product business writes and receipts use attributed SaveChanges; SQL is controlled schema DDL or trigger reads/locks, not a product write bypass.

Equipment mutations authorize before snapshot, receipt lookup or projection and read the whole vessel predicate in a SERIALIZABLE transaction. The supplied `EquipmentHierarchyPolicy.ValidateReparent` validates that snapshot. A database trigger additionally takes a tenant/vessel advisory transaction lock, checks ancestors, rejects cycles and immutable ownership changes. SSI/serialization failures are returned as409, never silently retried with a stale base version. These are authored race protections; actual PostgreSQL behavior is **not yet demonstrated**.

Maintenance job references belong to Technical, never TugJob. Job definitions are immutable operator-authored references with UUID replay comparison. Owner selections come from tenant/vessel-scoped server-provisioned memberships with `maintenance.owner.eligible` and valid local role references, never caller/IdP role labels. Only selected eligible Role IDs are persisted to the normalized owner reference table. No names, subjects or credentials are exposed by selection. Replays reauthorize, and owner selections are rechecked against current provisioning before returning a receipt.

Plan revisions freeze actual declared inputs, Asset revision, job/owner reference, provenance, explicit reference UTC clock/timezone, algorithm version and calculation result. Decimal transport/storage stays base-10 strings; parser rejects exponents, culture-dependent commas, negative/excess precision and unsupported ranges rather than rounding. Calendar day/month/year calculations use an explicit local date (including end-of-month semantics). Meter and condition calculations require explicit observation/source version; absent inputs never mean zero or false. Without a source freshness duration, historical meter/condition inputs require review. A same-reference-time declared observation supports a relative planning calculation, never independently verified adapter data or fitness. Missing job/owner/criticality and Unknown criticality yield ReviewRequired. NextDue remains a computed value, not a free deadline override.

Warning lead, grace, approval-rule and postponement declarations are preserved but do not manufacture an approved policy. Incomplete drafts can be saved without a human approval workflow. The postpone endpoint reauthorizes and durably audits denials; currently no valid policy-backed activation is possible. Request-supplied authority/reason/deadline cannot activate deferral. Corrections append draft revisions; they do not complete or verify a work order.

The TR/EN workspace is linked from actual fleet details at `/app/equipment-hierarchy/{vessel}` and `/app/maintenance-plan/{vessel}`. `?id=UUID` selects records; selection/back-forward and scope changes preserve the complete forest. Filtering uses a separate matching-record list and never hands an orphaned subset to the tree. The editor, actual plan/job/owner selectors, exact decimal inputs, why-due/review text, observation/reference timestamps, history, loading/empty/denied/error states, conflict/discard flow and frozen same-attempt replay use real APIs. Native modal dialog provides focus containment for dirty navigation. No canned rows or green readiness state. Offline writes are unavailable; ambiguous attempts remain in memory and must be retried before editing/navigation. Routine load errors use the exact handed-off operator copy.

## API aliases and contracts

Current screen routes `/api/v1/equipment-hierarchy` and `/api/v1/maintenance-plan` take explicit `?vessel=UUID`; the workspace uses their vessel-path forms. `/api/v1/assets` aliases Equipment, and `/api/v1/maintenance-plans` aliases MaintenancePlan. Each uses the **same** handlers, scope, tables, action fingerprints and receipt namespace. There is no duplicate screen/alias table. History, references and jobs use vessel-path routes. Server-derived tenant/actor and explicit capabilities remain mandatory across aliases. `DirectoryRead` is only the authentication/base scope prerequisite, never a technical permission grant.

Named response DTOs and Produces metadata are authored for OpenAPI export. `technical-api.ts` is explicitly an **authored consumer adapter**, not falsely described as generated. Actual OpenAPI export/type generation and adapter replacement/parity checks are DEFERRED with the user-prohibited contract-generation run. Existing generated output is preserved. This criterion remains open until the gate.

## Field / FR / TC mapping

Every field below retains its dictionary row's `FR46-{screen}-{field}` and `TC46-{screen}-{field}` IDs. Those are source trace IDs, not passing execution receipts.

| Dictionary rows | Field → implementation |
|---|---|
|1352–1356|vessel_id→Asset.VesselId; parent_equipment_id→ParentId; equipment_code→Code; equipment_name→Name; equipment_level→Level|
|1357–1361|system_family→SystemFamily; equipment_type→EquipmentType declaration; criticality→Criticality declaration; maker→Maker; model→Model|
|1362–1367|serial_number→SerialNumber; location→Location; commission_date→CommissionDate; status→Draft only; responsible_role→ResponsibleRoleId; notes→Notes|
|1406–1410|vessel_id→Plan.VesselId; equipment_id→AssetId; job_id→Technical MaintenanceJob; plan_type→Inputs.PlanType; trigger_type→Inputs.TriggerType|
|1411–1415|interval_value→exact string; interval_unit→explicit unit; meter_source→MeterSource; last_done_date/meter→declared baseline (no completed-work claim)|
|1416–1420|next_due_date/meter→immutable computed result; warning_lead/grace_period→unapproved declarations; postponement_allowed→unapproved declaration, never authority|
|1421–1425|approval_rule→unapproved reference; owner_role→normalized eligible Role ID; criticality→declaration/Unknown; applicable_requirement→unverified reference; status→Draft only|

Physical columns/relations and supported arithmetic are authored engineering mappings, not source approval of controlled catalogs. Generic unresolved dictionary lookup rows are not promoted to approved production enums. Asset snapshots retain original pre-save version context; the actual committed xmin is returned by PostgreSQL and recorded by the receipt save, not fabricated in the pre-save snapshot.

## Acceptance criteria and authored coverage

| Criteria | Authored evidence | Execution |
|---|---|---|
|tenant/vessel hierarchy, self/orphan/cycle and concurrent reparent protection|support policy tests; TechnicalPersistenceTests; TechnicalTests two actual API snapshot connections|DEFERRED|
|exact versions/history/audit and restart/replay/rollback|TechnicalPersistenceTests and TechnicalTests, receipt reauthorization/restart and alias cases|DEFERRED|
|deterministic dates/DST/month ends/precision/Unknown/missing adapters|MaintenanceCalculationTests; real API calculation assertions|DEFERRED|
|scope/role spoofing/revocation/owner eligibility, invalid postponements|TechnicalTests actual host/PostgreSQL, denied read/history/reference/mutation, invalid deferral audit|DEFERRED|
|current migrations/model, seed idempotency and operator preservation|TechnicalPersistenceTests and updated existing seed count expectations46→61,47→62|DEFERRED|
|connected TR/EN tree/plan, deep URL/filter, dirty/conflict and immutable retry|15 supplied tree cases + TechnicalWorkspace.test.tsx (mock UI only)|DEFERRED|
|actual host keyboard/typeahead/aria-owns, responsive, plans/denied/back-forward|technical-cases.ts in the existing private signed-provider/TLS runner|DEFERRED|
|controlled lost response after actual commit|separately named equipment-controlled-response-loss-real-commit; route.fetch commits to actual host, then deliberate response abort|DEFERRED controlled transport fault, not ordinary network evidence|

No suite, build, restore, typecheck, OpenAPI generation, migration application, browser, provider, TLS or independent QA was run by this worker. Git diff review and AST graph maintenance are not product verification. No skips, PASS, WCAG approval, backend isolation proof, canonical SP acceptance, safety/pilot sign-off or GDPR claim.

## Remaining work and gate inputs

Actual combined build/migration/model/export/typecheck/backend/component/real-host browser verification is required, followed by fixes and fresh independent exact-target QA/required CI under coordinator control. The private browser runner is reused unchanged; its disposable writer must explicitly receive technical read/write/history and owner-eligible capabilities, restricted user none. This worker did not change identity architecture or enroll/provision an account.

Open source policy work remains: approved type/criticality/status/plan/unit catalogs; current-effective Role lookup semantics and grants; authorized operational meter/condition adapters with freshness/version/unit contracts; warning/grace semantics; DEV-109 WorkOrder authority/reason/risk/evidence/max-extension/readiness rules. No approved WorkOrder postponement, defect/safeguard/readiness integration, completion/verification or durable offline capture is claimed by this draft foundation checkpoint. Source dictionary field coverage does not establish these gates. Continue the full194SP foundation target within970 total; previous authored modules and actual historical evidence remain preserved.
