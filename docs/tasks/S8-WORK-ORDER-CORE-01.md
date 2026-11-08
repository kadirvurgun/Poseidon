# Work order owning API and persistence foundation

Status: **SOURCE AUTHORED / verification DEFERRED / canonical DEV-054 OPEN**.

DEV-054 / T-S8-003 is a canonical S8 task, 13 SP. This is an owning draft
subpackage alongside the domain and UI sources, not an accepted release,
verification, evidence submission or complete work order lifecycle.

## Requirements and acceptance boundary

Active baseline: `poseidon-v46.3-20261030`; see [active source](../sources/active.json).
[Exact original source locators, workbook hashes, dictionary mapping and unavailable
lookup/authority cells](S8-WORK-ORDER-DOMAIN-01.md) govern this package:
18.01 Backlog row55 / DEV-054; 14.02 Data Dictionary2316 rows1426–1463;
Lookup Kaynak Çözümü304–313; 15.01 API specification1315/1318 and1322/1325.
Dependencies DEV-029 and DEV-053 supply engineering files/planning foundations;
their source implementations do not qualify evidence, policy or authority.

The source acceptance criteria implemented here are: owning incomplete drafts
persist 25 canonical facts with Missing/Unknown preserved; actual tenant/vessel,
permissions and trusted write attribution gate every route; revisions and actual
committed xmin are immutable; exact same-owner commands recover exactly one
committed effect; new plan links resolve actual current owning data; reads and
outcomes recheck current scope and exact source/head after independent audit.
All test-based acceptance remains open because execution is deferred.

## Delivered behavior

- Technical WorkOrder, WorkOrderRevision and WorkOrderReceipt EF entities have
  tenant/vessel compound relationships, exact owning revision/version receipts,
  nullable plan-pin completeness, PostgreSQL xmin and bounded immutable history.
  A source-authored migration and matching snapshot add one-revision head guards,
  immutable history/receipt and truncate protection. Every business write uses
  attributed audited SaveChanges in a SERIALIZABLE transaction. PostgresFailure
  classifies PostgreSQL/serialization failures, including wrapped statements.
- `/api/v1/work-orders?vessel=UUID` owns capabilities, list, detail, history,
  POST draft creation and PUT exact-version draft updates. Lists/history page25.
  Query keys are closed and scoped; strict commands require id, expectedVersion,
  fields, sourceVersion and **explicit nullable plan**. Idempotency-Key is one
  UUID header. No caller actor/tenant/authority, unexpected fields or silent
  source-contract changes are accepted.
- `workorder.read` additionally requires `maintenance.read`, `equipment.read`
  and actual tenant/Ship/Tug vessel scope. Writes require `workorder.write`,
  trusted role attribution and current edit-vessel scope. Current-only owning
  GETs can establish editability; receipt and every history response stay
  read-only. Actor/key receipts pin exact normalized facts, plan/source context,
  resource identity, committed version and durable outcome. Reused keys with
  different commands conflict, and competing versions never silently rebase.
- The source plan reader is
  `/api/v1/vessels/{vessel}/work-order-plan-origins/{id}?revision=N`.
  It joins actual MaintenancePlan head/revision and its actual linked
  AssetRevision. A pin contains the **actual current plan head xmin** and a
  fingerprint of exact immutable plan/asset contents. Pre-save historic JSON
  does not manufacture a past committed xmin. New/changed links require current
  plan context; already established history can remain while actual source
  read rights and relationships persist. Requests for an older source context
  return `plan_context_stale`. They do not synthesize historical versions.
- The safe source projection includes plan/asset IDs/revisions, optional job ID
  and allowlisted asset description. It excludes owner roles, user identities,
  notes, serial number, calculation/observation inputs and all raw snapshots.
  It never automatically copies a plan, job, equipment or evidence reference
  into the unresolved dictionary lookups.
- Bounded source capture returns a composable expression, not a final Boolean.
  One fresh final vessel SQL combines exact returned revisions, exact current
  head metadata, current-and-returned source plan/asset visibility and proposed
  current source pins. Trusted rights are synchronously resolved again after
  database awaits and repeated after the success audit. An old receipt/history
  cannot bypass a newer owning head's protected source relationship.
- Independent WorkOrderRead, WorkOrderCommand and WorkOrderPlanOriginRead HTTP
  Attempted/Success/denial observations complement transactional EF audit.
  Safe EF metadata includes trusted actor, role, vessel scope, operation name,
  PREPARATION_COMMAND and actual revision/version; narratives, hashes, bodies,
  plan contents and key values are withheld. Postcommit authorization/audit
  loss returns503 `command_outcome_withheld` without undoing a durable effect;
  exact original command recovery requires current authorization again.
- Optional administrative WorkOrderFixtures adds only missing synthetic draft
  heads/revisions with all facts Missing and no plan, evidence, permit,
  credential, verification, authority or receipt. It requires an attributed
  administrative transaction and actual vessel existence. It is not invoked by
  startup or ordinary seed import. Existing operator drafts/history survive
  repeated import. [Seed requirements](../seed-data.md) still apply.

## Authored verification and remaining gates

Actual API/real PostgreSQL test sources cover draft-state separation from
RELEASED/EVIDENCE_SUBMITTED and a literal verification-result declaration;
strict source/query/body/explicit-null handling; unavailable evidence, identity
and catalog lookups; actual xmin/history and safe audit metadata; exact replay,
changed-key-body and stale versions; concurrent duplicate keys and competing
versions; current plan/context adoption, forged pin/source/version rejection;
established historical links and stale current-context rejection; cross-tenant,
other vessel, Ship/Tug mode, permission and trusted-role denial; optional fixture
idempotence/operator preservation; read-audit head/rights interleavings; plan
head advancement during attempt audit with transaction rollback; source-context
head/permission advancement after read audit; postcommit withheld outcome and
reauthorized recovery; compound foreign keys, partial plan constraints and
immutable database retention guards.

**None were run.** Test/build/typecheck/lint/contract generation, model parity,
migration execution, provider/lab/browser/CI and runtime activation are DEFERRED.
Source whitespace checking is not a passing product check or independent QA.
The source inventory becomes113 mutation templates /105 critical templates /
115 concrete critical operations; these are definitions, not observed coverage.
Root maps MapWorkOrders and MapWorkOrderPlanOrigins and integrates navigation/UI.

The server state remains DRAFT / REVIEW_REQUIRED for every declaration. The
backlog stage literals and `verification_result` text do not authorize release,
submission, verification, closure or effectiveness. Capabilities for those
transitions and dedicated offline execution remain unavailable. Missing source
conditional predicates, approved catalogs, evidence adoption, risk/permit,
qualified human authority/transition matrix, independent target-commit QA and
all later bundle checks remain open. No S8 or overall acceptance is claimed.
