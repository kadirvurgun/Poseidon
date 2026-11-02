# CONTROL-EVIDENCE-01

AUTHORED_UNVERIFIED / NOT ACCEPTED. Product authoring before
REGULATORY-CHAIN-BUNDLE-01. All product checks are DEFERRED / NOT RUN.
194/970SP is the user's target, not earned progress. Canonical8/970SP,
2/121tasks,0/196accepted and the selected40-task/272SP inventory remain unchanged.

## Receiving work and receipts

Receiving HEAD `dc15b6fbed2ec2360200a7e7a2c779e70bb2e880` was clean. No partial
diff was discarded. Read actual AGENTS cadence, backend/UI skills, active30.10
baseline, architecture, automation, security, seed and dependency policies; read
LIFECYCLE-APPLICABILITY-01, SECURE-FILES-01, FILES-SOURCE-COMPLETION-01,
FOUNDATION-CONTINUATION-2026-11-02 and its JSON evidence, plus selected source
reconciliation. Independent historical base80b and predecessor authored work are
preserved; predecessor evidence is not a current passing result.

Graph MCP tools were unavailable. Scoped `graphify query` preceded targeted
code reads. Local `query_sources.py` bounded queries DEV-028, OperationalControl,
Evidence, control-library, evidence-graph, operational_control, verification_rule
preceded exact indexed locator reads. OperationalControl returned no exact hit;
this was not treated as missing authority or a substitute for ERD reading.
No whole chunks corpus was loaded into model context. No model-backed RAG,
corpus validation, MCP connection or legal/source admission is asserted.

Actual receiving history had no receipt for
`70756561f35e3be38ae34a122f7596defa5f8927`. Normal `git cherry-pick -x` created
`46c08d5`; it changes **docs/knowledge.md only**. No preparation branch merge,
snapshot-basis300cfe3 integration, reset/rebase/amend/force push, newer document
overwrite or unrelated support-code integration was performed.

## Exact controlled source and acceptance mapping

[Exact95 indexed source records and original hashes](control-evidence-01.sources.json)
are retained as controlled candidate evidence, not accepted maritime authority.

| Source / trace | Literal scope and implemented authoring |
|---|---|
|18.01 Backlog29 DEV-028 / T-S3-003 /8SP|Owner/frequency/evidence rule/verification rule/state/requirement links and orphan critical control report. Exact dependencies DEV-026/027 remain unaccepted.|
|18.01 Backlog30 DEV-029 / T-S3-004 /13SP|Immutable evidence metadata/checksum/provenance, many-to-many links and stale/expired/superseded display. Exact dependencies DEV-018/028 remain unaccepted.|
|14.01 paragraphs6,10,12,125–126,129,132,136,142,146,191,221,247|Shared domain aggregates; immutable binary outside DB, exact lineage, verification/decision separation, no deletion of previous evidence; controlled architecture candidate, not physical schema approval.|
|14.02 Data Dictionary2316 rows611–631|evidence-graph field contract, tenant/authorized-vessel context, actual authorized owner, append/link/supersede and generic field policies. Physical mappings/codebooks and several defaults remain REVIEW/TBD.|
|11.01 2316 Alan v46.3 rows611–631|FR-/TC-evidence-graph-{record_id,vessel_imo,requirement_ref,responsible_role,record_datetime,status,evidence_ref,verifier,instrument,clause,edition,effective_date,applicability,applicability_basis,control_id,control_owner,required_evidence,verification_method,last_verified,effectiveness,notes}. Actual row IDs retained in source artifact.|
|13.02 paragraphs219–224,1364–1368|operational-control-library,0 fields/0 actions, BA/DEV review candidate. No fabricated per-field FR/TC for this zero-field source screen. Local CE traces supplement backlog acceptance.|
|13.02 paragraphs639–644,1714–1718|evidence-graph21 fields/6 actions; governed projection, not an approved upload/verification workflow.|
|13.04 paragraph101|Understandable file type/size/malware/checksum feedback via retained actual VesselFiles transport.|
|15.01 paragraphs7,12,83,99–100,135–136,239,545–548,1035–1038,1622|Canonical resources, metadata-first actual file transport, source projections and human verification boundary. `/controls` and `/evidence` implement backlog routes; `/operational-control-library` and `/evidence-graph` expose the same owning aggregate as source projection aliases.|

No document49/50 proposal is an admitted role, catalog, permit or signature.
POL-DEFAULT-01 partial-draft behavior is retained. Evidence-graph verifier,
last_verified and effectiveness are controlled decision fields; no ordinary
author request can write them. Row630's Date/Time versus effectiveness enum
tension is not silently turned into a production catalog. The21-field source
projection is not claimed completely implemented/accepted by this authoring slice.

| Local trace / acceptance | Actual code behavior | Authored meaningful tests, all NOT RUN |
|---|---|---|
|CE-01 / DEV-028|Vessel-scoped Control head/xmin plus immutable revisions. Nullable owner/frequency/policy/critical declarations allow incomplete drafts. Every save, including unchanged content, increments revision and checks xmin. DRAFT is the only stored lifecycle; submit returns409 after authorization/CSRF. No enabling path or default operational frequency.|ControlEvidencePolicyTests; persistence stale-noop and API exact-retry/race tests.|
|CE-02 / FR/TC-evidence-graph-control_owner/responsible_role|Owner selects an actual uniquely provisioned tenant member and role reference with explicit control.owner.eligible and current vessel/mode scope; references are identifiers, not free text authority. Missing selection stays null. Replayed writes recheck eligibility.|API actual owner/role selection and revoked eligibility retry; malformed/missing owner tests.|
|CE-03 / requirement_ref/clause/control_id/applicability|Many-to-many immutable snapshot links pin exact Source, Requirement, optional Rule and Control/Evidence revisions. Tenant/vessel composite FKs prevent cross-object scope bypass. Supersession appends a distinct evidence record; old bytes and links are retained. TargetVersion stores the original observed target xmin, not reconstructed current head state.|PG cross-vessel FK/append-only tests; API multiple requirements/two controls/relink/as-of replay and later-control-edit test.|
|CE-04 / evidence_ref/record_datetime|Evidence draft metadata has explicit missing-file state. A real reference requires exact StoredFile ID/revision/checksum, CleanTransport stage and vessel-owned non-protected owner. FileObjects.OpenAsync reads the actual configured local/S3 provider version; length/SHA256 checked again. Pending/Receiving/missing/dirty/protected-owner references are rejected. No binary copy or direct download route is introduced.|Actual HTTP stage/byte-upload/protocol scan/reference/corruption/missing-byte case; pending/malicious quarantine negative; retained secure-file owner/capability suites. Protocol peer is not scanner efficacy evidence.|
|CE-05 / status/verifier/last_verified/effectiveness|Clean file existence, business completion, verification and effectiveness are separate. Output remains UNKNOWN/UNVERIFIED/UNKNOWN. Missing source admission/verification and policy prerequisites are visible blockers. No author-supplied actor, tenant override, Verified or Effective member is accepted.|API authority-member negatives and submit denial; strict consumer authority/coercion/extra-key/cross-vessel regressions.|
|CE-06 / effective_date/edition|Replay uses immutable revisions and explicit asOf. Declared not-yet-valid/expired/stale dates have explicit declared labels. Source-qualified freshness remains UNKNOWN; no date becomes Ready/Effective. Old revisions and replaced evidence remain available to earlier decisions. Incomplete supersession drafts without clean files do not supersede another evidence record's time projection.|Domain boundary/chronology/unknown/supersession cases; API as-of before relink and retained target xmin.|
|CE-07 / DEV-028 orphan report|Scoped, paged critical draft controls without requirement links are reported. Unknown criticality is not coerced to false. Authoritative report available=false/reason=AUTHORITATIVE_APPLICABILITY_UNAVAILABLE even when draft findings are empty. It is not the separate DEV-041 current critical-requirement coverage evaluator.|API critical versus unknown orphan and unavailable semantics; strict all-clear rejection; signed-host journey.|
|CE-08 / audit/retry|Business head/revision/link/write-audit and exact JSON receipt commit in one audited EF transaction. Live permissions/scope/owner/file checks precede receipt lookup. Original outcome xmin/bytes survive later edits. Duplicate/serialization failures roll back before resolving receipt or409. Storage failure is503. Safe reason codes; content/owner text, provenance and receipt keys are not copied into diagnostics/audit summaries.|API race, reused-key, no-op conflict, revoked read and owner eligibility, receipt failure rollback; PG rollback/immutable history tests.|
|CE-09 / source screen roles +13.04|Real TR/EN React route from existing register, paged authorized vessel/owner/requirement/rule/control/file selection, actual retained upload component, links/source history, immutable history and asOf drilldown. Responsive and keyboard controls; loading/empty/error/permission/offline/conflict/frozen retry/dirty-context states. Upload attempt is retained through offline/reconnect; no runtime mocks.|ControlEvidence.test.tsx; strict consumer tests; VesselFiles offline regression; two registered genuine-host Playwright cases. Mocks are test-only and do not prove backend journeys.|
|CE-10 / seed/security|Per synthetic vessel, missing-only fictional Control/Evidence draft heads/revisions. No file, observed time, policy, owner appointment, verifier, receipt, sign-off or credential seeded. Ordinary system-seed write audit records actual fixture import, not fabricated observations. Reimport preserves operator changes.|PG reseed/preservation; seed expected96/97 audits replaces84/85 solely as authored expectations (+12 rows for3 vessels), not observed counts.|

## Deliberate boundaries and remaining work

Capabilities are server-only: control.read/control.write, evidence.read/evidence.write,
control.owner.eligible plus existing directory/file/applicability/regulatory reads.
Nothing is provisioned by this task. This first graph consumer requires tenant-wide
catalog read and both graph-family read permissions to avoid partial resource/count
leaks. Vessel/mode scope precedes every list/history/report/owner/receipt operation.
Only vessel-owned ordinary files are supported; document-version/person/protected
file owners require their owning permission adapters and are explicitly rejected.
The existing provider/scanner/configuration defaults and live signed downloads
remain unchanged; no production provider, secret, membership, account or lab setup.

The version envelope on a history row is the **current editing head token**;
immutable revision/hash/source/file/links and saved target xmin are historical.
Replay of a command returns its exact original envelope from its immutable receipt.
No historical reconstruction of old memberships or source qualification is invented.
Bounded pages25, link sets32 and command32KiB are engineering bounds, not maritime
policies. Queries reject extra/repeated members, malformed offsets and non-offset
asOf. Text policy/validity declarations are not admitted executable policies.

Actual admitted source/policy/current-obligation integration, qualified freshness
policy evaluation, source-specific ownership/reviewer appointment semantics,
verification/separation of duties and the HUMAN-DECISION-TRACE-01 write chain remain
software integrations, alongside genuine external source/legal/human authority
gates. A DRAFT rule and a provisioned reference do not close those gaps. This task
does not make authoritative applicability evaluate available or declare the full
selected regulatory chain/foundation ready. No canonical sprint/release gate closes.

## Actual changed files and checks

Backend: ControlEvidencePolicy.cs; ControlEvidenceStorage.cs;
20261102150000_ControlEvidence.cs; ControlEvidenceFixtures.cs;
ControlEvidenceEndpoints.cs; FoundationIdentity.cs; Startup.cs;
PoseidonDbContext.cs; AuditMetadata.cs; SeedImporter.cs.

Frontend: ControlEvidence.tsx; control-evidence-api.ts; RegulatoryRegister.tsx;
VesselFiles.tsx; product.css. Tests: ControlEvidencePolicyTests.cs;
ControlEvidencePersistenceTests.cs; ControlEvidenceTests.cs;
ControlEvidence.test.tsx; control-evidence-api.test.ts; VesselFiles.test.tsx;
control-evidence-cases.ts; shared browser cases.ts; PostgresSeedTests.cs.

All domain/PostgreSQL/API/React/Playwright tests, build, lint, typecheck, restore,
OpenAPI/consumer generation, EF scaffold/designer/snapshot reconciliation/parity,
migration/seed execution, provider/scanner/browser/lab/security scans and independent
QA/CI are **DEFERRED / NOT RUN** until REGULATORY-CHAIN-BUNDLE-01. DDL/model are
authored; generated model artifacts are not claimed synchronized. No discovered,
passed, failed, skipped or duration counts and no tested compatibility claims.
No dependency/lock changes. Only source retrieval/authoring, whitespace inspection
and AST maintenance occurred. Exact source-only outcomes and file hashes are in the
[phase report](../phases/CONTROL-EVIDENCE-01.md) and its evidence inventory.

Coordinator owns independent exact-target review, the later combined execution,
publication and any acceptance. Local UNVERIFIED commit is a reviewable checkpoint.
