# S1-AUDIT-CONTEXT-01 — attributed command and aggregate audit context

Status: **AUTHORED; execution DEFERRED; acceptance false**. This package joins the
larger S1 bundle. No build, restore, test suite, scan, browser journey or provider
operation was executed for this authoring checkpoint. Existing S0 acceptance is
separate; S1 story points remain unaccepted.

## Source and acceptance criteria

Active baseline: `poseidon-v46.3-20261030`. DEV-016 / `T-S1-004` is the original
[18.01 backlog](../../analiz/POSEIDON%20GUNCEL%2030.10.2026/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx),
`sheet:Backlog/row:17`, 8 SP, dependencies DEV-014 and DEV-015. Source SHA256:
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Literal criterion: “Actor, role, tenant, vessel, before/after, reason, timestamp,
correlation ID kaydedilir; kritik write coverage hedefi %100.”

The controlling [14.01 ERD](../../analiz/POSEIDON%20GUNCEL%2030.10.2026/05_TECHNICAL_ARCHITECTURE/14.01_POSEIDON_v46.3_Product_Wide_ERD_Data_Architecture_TR.docx),
SHA256 `c3f5e612d69e33d2e5e3a1227644cb9a0ec8937bdcf74b20d2b5a60b968344ab`,
paragraph141 requires append-only before/after **summary**, source and correlation;
paragraph143 requires separate protected-data policies and redaction. Bounded
local source retrieval, original XML paragraph reread and independent source
interpretation preceded this change. Raw snapshots, protected values, private
hashes and invented human reasons cannot fill audit fields.

Acceptance requires trusted command/role provenance on every current web write,
actual aggregate scope, privacy-safe changed-field or immutable revision lineage,
truthful reason semantics and atomic persistence. The integrated current-route
inventory, actual runtime coverage denominator and independent final QA belong
to the coordinated bundle; this author cannot self-accept DEV-016.

## Actual implementation

[ActorContext](../../backend/Poseidon.Persistence/Entities.cs) gains an optional
trailing `AuditCommandContext` with `EndpointName` and `ReasonCode`. `EndpointName`
is the server route catalog's stable operation identifier, never a request body,
role label or caller reason. It admits 1–80 ASCII letters/digits/underscore/
hyphen/dot, beginning with a letter. The six technical reason codes are exactly:

* `PREPARATION_COMMAND`
* `FILE_TRANSPORT`
* `OFFLINE_LIFECYCLE`
* `REGULATORY_WORKFLOW`
* `HUMAN_REVIEW_WORKFLOW`
* `SOURCE_QUALIFICATION`

These describe ordinary software operations. They grant no permissions, human
authority, justification, safety approval or expert sign-off.

[PoseidonDbContext](../../backend/Poseidon.Persistence/PoseidonDbContext.cs)
requires current `Source=WebCommand` writes to have `Type=User`, a valid command
and an actual bounded, distinct, nonempty GUID role-reference snapshot before
database access. Existing source-null manual/historical and explicit system
contexts remain possible without fabricated command or role data. Any explicitly
supplied malformed command is rejected even on a manual context. Current web
aggregate scope must resolve; an unknown future receipt cannot silently acquire
tenant-only semantics. The API's separate coordinated route catalog and denial
guard supply trusted metadata and preserve normal HTTP denial responses.

Maintenance observation revocations resolve the actual parent observation's
vessel; evaluation-rule and evaluation-qualification children resolve their real
evaluation parent. Direct vessel bindings omitted from the old switch now retain
their actual vessel. Generic command receipts resolve the real tracked or
tenant-filtered persisted resource, including equipment, maintenance, Tug,
planning, credential, crew assignment, import, document, file, vessel/profile and
catalog actions. Company, tenant-person and source/requirement receipt targets
are checked without inventing a vessel. Async saves use async database reads;
sync saves retain the synchronous path. No `IgnoreQueryFilters`, raw SQL business
mutation, administrative request context or audit bypass is introduced.

Person, identity revision and personal profile/revision are actual tenant-person
aggregates. Their `VesselId` remains null even if a fixture's person ID happens to
equal a vessel ID. Their scopes and receipts explicitly say `TenantPerson`;
vessel-owned assignments, credentials and documents retain their real vessel.

Existing business mutation and generated audit still share the EF save and its
transaction. Committed credential/import receipt xmin mapping is completed;
command receipt revision comes from its real `RegisterRevision`. Human/control/
qualification receipts retain the actual committed tracked head revision. The
first save cannot know the database's future xmin: it is never backfilled into an
immutable audit row. Where applicable the second audited receipt save records
that returned xmin within the same transaction.

## Closed summary and reason contract

[AuditMetadata](../../backend/Poseidon.Persistence/AuditMetadata.cs) emits an
array of at most20 objects, at most2048 characters. Every entry has exactly
`field`, `before`, `after`, `evidence`; values are strings or null. New provenance
fields have `before=null`, `evidence=Allowlisted`:

| Field | Actual value |
| --- | --- |
| `CommandName` | Server-supplied stable operation identifier |
| `TechnicalReasonCode` | One of the six closed codes above |
| `ScopeKind` | `Vessel`, `Tenant`, `TenantPerson`; unresolved manual/historical metadata explicitly stays `Unresolved` |

New current web metadata cannot use `Unresolved`. Normal current technical writes
use `RecordedControlledCode`. Existing controlled rule/control/human reason codes
remain separate `ReasonCode` fields; protected human rationale/conflict content
stays in its original immutable protected history with exact references. Current
technical provenance does not replace those reasons. Non-web system operations
say `SystemOperation`; missing source-null user context says
`HistoricalUnavailable`. No historical authority or reason is backfilled.

Existing changed-field allowlists preserve exact safe measurements and revision
references; arbitrary code/name values remain `ValuesWithheld`, bodies remain
`RevisionReference`. Former object-shaped catalog/qualification/Tug-offline
summaries now use the same array contract. Qualification/fact hashes, source
hashes, arbitrary policy text and rationale are not copied. Safe special fields
are `State`, `Revision`, `ActiveRevision`, `AuthorityId`, withheld `Reason`,
`RuleId`, `RuleRevision`, `QualificationRevision`, `AttestationId`, `ServerVersion`,
`UpstreamId`, `Lifecycle`, `Outcome`, and `Binding`.

Actual revocation records say `Lifecycle=Revoked`. Newly created ownership
references say `Binding=OwnerReference`; offline enrollment says
`Binding=ServerOwnedContext`; validated legacy-storage conversion says
`Binding=LegacyStorage` before and `ProtectedStorage` after while preserving
captured revisions. Modified enrollment retains its real server-owned binding
on both sides while key/expiry values stay withheld. Actual receipts say
`Binding=CommittedResource`, person records `TenantPersonRecord`, configured
positions `PositionReference`, and draft job references `JobReference`.
Actual immutable evaluations say `Outcome=EvaluationRecorded`; import
preview/row outcomes say `PreviewRecorded`, `valid` or `rejected`. These are
recorded software effects, not authority or content-verification claims.

## Authored tests and remaining gate

[AuditCommandContextTests](../../tests/Poseidon.Persistence.Tests/AuditCommandContextTests.cs)
cover pre-database missing/malformed/duplicate-role and command guards, finite
vocabulary, unknown current receipt scope, redaction and four person aggregate
projections, plus actual retained binding on modified offline enrollment without
secret/key/expiry copying. Projection tests explicitly suppress database
persistence; they are not PostgreSQL evidence.

[AuditContextPostgresTests](../../tests/Poseidon.Persistence.Tests/AuditContextPostgresTests.cs)
author real database checks for revocation ownership, actual equipment receipt
xmin/revision, tenant-person profile receipts, cross-tenant parent rejection and
a second-save receipt constraint failure that rolls back the company plus all
business audit in the same transaction. Synthetic roles/actors are disposable
software-test fixtures, never production appointments.

All checks are **DEFERRED** until the complete S1 gate. Run existing immutability,
retry/replay, conflict, privacy and source-specific human authority regressions
as well as these tests. Actual endpoint/model coverage and independent QA must
measure the entire current critical-write denominator before claiming100%.
This package does not establish full HTTP/user-action audit coverage, production
role rollout, legal privacy compliance, retention/DR, pilot acceptance or S1
completion. No schema change, migration or new operational seed is required.
