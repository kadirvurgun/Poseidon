# DOMAIN-COMPLETION-01 — checkpoint A

Status: **AUTHORED_UNVERIFIED / INCOMPLETE / NOT ACCEPTED**. No canonical SP acceptance, publishing, merge, operational decision, GDPR/KVKK approval or production migration activation. Execution of every suite/build/typecheck/contract generator/migration/browser/provider setup remains **DEFERRED** by the user. Source whitespace inspection is not product verification.

Receiving tree was clean at actual Import checkpoint `19f17b6`. No partial edits were discarded. Backend/UI skills, AGENTS, active source baseline, architecture/automation/security/seed/dependency contracts and actual Crew/Technical/Tug/Import task reports were read. Graph discovery used the available local Graphify graph; no MCP code graph tools were available. Actual final Import source register has **111 records**, not 111 imported fields. `DEV-020` remains an actual vessel-profile dependency; no vessel-create story was inferred.

## Exact handoff receipts

Normal `git cherry-pick -x`, in dependency order:

| Origin | Local receipt |
|---|---|
|2eebb178b2963458d9d99bc66f51b3ee23568210|dd00773|
|5fb574bc894b79e0343c746b6d40a6f6907a6049|d778162|
|744676ff177e7beba0fcc1c844ef00abd70aa78c|c0b4dc8|
|028daff55a8c10b8dbb10c5e8328a2ee9bc6d19a|eb0cc9f|
|fd6f995cb51d14597e3d6effa2ca52bebf943609|af1d5dc|
|4952842700b3607e2844c7ae85578af46c38a609|3d36eff|
|b3b30351250571f4e5cc41de13bc5085d43a85d7|edd680c|
|a8e352e39d8aaf31c3e46cc342e739a6c42a0bde|8d7b5fb|
|f9e53b7bc97e7e5a7f04b3dea06515158ce70880|1c27c27|

The 13 same-session Import permission/CSRF cases and seven boundary regressions are retained. Cookie/CSRF and read-before-write-denial assertions were not weakened. Explicit Xunit imports are retained. Driver inventory is ten cases including UUID binding, cross-namespace device races and discard release; cipher inventory is twenty cases. All remain authored, unexecuted support rather than application journey proof.

## Implemented software (configured behavior, not approval)

* Numeric import declarations use absolute ASCII lexical boundaries; no trim/coercion. XLSX includes a valid linked styles part/content type and built-in49 Text format on editable columns before entry. No OpenXML dependency.
* The central trusted actor resolver now retains explicitly provisioned Import/Certificate capabilities that the previous checkpoint filtered out. It also recognizes only named profile/assignment capabilities; caller roles and arbitrary profile capabilities grant nothing.
* Normalized person-owned `crew.PersonalProfiles` with fifteen separately authenticated encrypted columns: nationality, birth, passport/seaman-book numbers and expiries, company start, three exact lexical experience measures, vessel-type experience, emergency contact, preferred language, phone/email. Typed DTOs, no arbitrary field bag. AES256-GCM uses fresh96-bit nonce/128-bit tag and binds tenant/person/field/key ID. Explicit operator secret references are required even in Development; unavailable configuration/key fails closed. Older keys may remain enrolled for authorized reads; removal retains ciphertext and reports unavailable. This is protected storage software, not key-custody/retention/legal approval.
* Ordinary scoped GET/PUT and safe metadata-only history, per-field version/purpose/read/write rules, exact controlled values with default deny. Current rights precede receipt recovery; xmin, immutable revision metadata, audited SaveChanges and durable receipt participate in one transaction. Profile values/ciphertext/value hashes do not enter audit summaries, search, counts or error text.
* Actual Import schema v2 includes all fifteen fields plus explicit profile base/policy versions. Mapping/preview/atomic trial commit call ordinary profile validators/handler; mapped null clears explicitly, unmapped fields remain untouched. New Crew staging tables are encrypted under the explicit protection contract. Historical plaintext Crew batches are fail-closed unavailable; a controlled historical protection conversion is not silently performed. Field revocation filters batch/history before response/count/paging or receipt recovery. Production commits remain restricted exactly as before.
* Operator-visible `/imports/coverage` returns the versioned source coverage/unavailable contract, filtered by current category access. It explicitly is not the writable template schema; actual111-record states live in `import-foundation-01-source-fields.json`.
* Reachable TR/EN protected profile editor uses authorized typed fields/options, exact text numerics, no private browser persistence, explicit partial patches, frozen uncertain retry body/UUID, scoped cancellation, current denial clearing and parent dirty guards. Private medical/qualification state remains separate.
* Current-effective assignment software uses explicit operator status/period/provenance/timezone policy. Draft is always excluded. End is exclusive; multiple candidates return Ambiguous. Actor-visible vessel filtering precedes selection; actual rank/department comes from its scoped Position. Ordinary assignment declarations accept configured statuses only with separate capability and matching policy version; they confer no safety/qualification approval. Status is preserved in immutable revision history. Missing source lifecycle policy remains unavailable, never an arbitrary Draft selection.
* EF migration/designer/snapshot authored; six add-missing empty synthetic profile/revision inserts preserve edits without private values or fake protected bytes. Seed audit expectations75/76 replace69/70; these counts are authored expectations, not observations.

## Source and traceability

Active `poseidon-v46.3-20261030` baseline. Actual44 Crew source FR46/TC46 records and their full contracts remain in `crew-roster-01.fields.json`, dictionary SHA `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`, Data Dictionary2316 rows1572–1615. Backlog SHA `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`, row48 DEV-047/T-S7-001; row70 DEV-069/T-S11-001; rows53/54 DEV-052/053; rows81–83 DEV-080/081/082. Bounded source retrieval read those actual records; 16.01 remains an API skeleton, not an offline field whitelist. Import field register records per-field software/configuration/source-context/external workflow gaps, never a wholesale imported count.

## Authored verification (execution DEFERRED)

New `CrewProfileTests`: nine actual host/PostgreSQL cases for all typed fields/exact precision/protected persistence, partial preservation/stale writes, same-session field revocation, purpose/catalog/version denial, ordinary/import parity/encrypted stage, revoked batch/history, unavailable policy, current-effective period/position lineage and111-record coverage semantics. `CrewProfileProtectionTests`: key availability/binding and five lexical theories. `CrewEffectiveAssignmentTests`: five source-period/provenance/ambiguity cases. `CrewPersonalProfile.test.tsx`: five meaningful UI cases for field masking, exact partial patch, uncertain same-attempt retry, denial clearing and stale-context cancellation. Existing Crew fixture mock explicitly models unavailable new policies, retaining its original assertions. Imported regressions remain unchanged except the shared source-workbook helper now maps required/supplied columns rather than inventing blank writes to every newly added optional field.

No suites, build, typecheck, EF/OpenAPI generation, migrations, browser or provider setup executed. No PASS, skipped, independent QA or accepted claim.

## Remaining software versus external source gates

**Unimplemented software, still required:** person-owned secure document/photo metadata+bytes/version/stage/scan/expiry/reauthorization; existing-person name/employee import update parity and protected legacy identity storage; complete Crew controlled rank/department lookup semantics; durable application offline capture/read/restart/reconnect/recovery/conflict/history and atomic domain+offline receipt APIs (helpers are integrated only as handoffs at this checkpoint); real-host application browser journeys for new scope; Tug scoped type/context lookup, editable versioned plan/assignment data/history/real board consumers, complete source-label localization/audit revision drilldown and evidence-aware BP/conflict adapters; normalized versioned PMS meter/condition observations and actual policy/adapter integration. These are not external approval excuses and are not marked complete. Vessel DEV-020 field scope remains as recorded in the actual111-record register.

**Genuinely external/deployment/source decisions:** BH legal field classification/purpose/lawful basis/retention, production controlled catalogs/natural keys/numeric precision and lifecycle semantics, actual protected-storage key custody/rotation deployment, approved offline field/device/session/expiry policy and enrollment/time authority, verified BP/certificate/crew/equipment evidence and required authorized human decisions, actual production import activation/DEV-070 reconciliation/backup/freeze/rollback/owner sign-off. The explicit synthetic profile policy example is software test configuration only, not any of these approvals.

Current phase: larger domain foundation authoring. Next: continue the required remaining software, then one deferred combined gate and independent exact-target QA/CI under the coordinator. No publication or merge by this worker.
