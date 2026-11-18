# Work order — bounded owning declaration domain

Status: **SOURCE AUTHORED; verification DEFERRED; canonical DEV-054 acceptance OPEN**.

Task: DEV-054 / T-S8-003, S8, 13 SP. This document describes the domain/wire
subpackage, not completed canonical screens or an accepted operational lifecycle.
Backend persistence/API, actual plan context, seed fixtures and UI are coordinated
separate work packages. A local commit is a source checkpoint, not validation.

## Controlled requirement and dependencies

Active baseline is `poseidon-v46.3-20261030`, selected through
[active sources](../sources/active.json). Original workbook cells were read using
bounded standard-library XLSX extraction after bounded source-index retrieval;
the whole chunks archive was not loaded into model context.

| Source | Exact locator | SHA-256 |
|---|---|---|
| 18.01 Development Backlog | `Backlog!A55:Q55` | `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674` |
| 14.02 Product-Wide Data Dictionary | `Data Dictionary 2316`, rows 1426–1463 | `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018` |
| 14.02 lookup source resolution | `Lookup Kaynak Çözümü`, rows 304–313 | same dictionary hash |
| 11.01 controlled traceability | `1900 Alan v46` and `2126 Alan v46.2`, rows 1427/1440; active 2316 dictionary field IDs retained below | `bc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9` |
| 13.02 visual catalog | paragraphs 1915/1917 and 1920/1922 | `b0b5e9fb3cad5137d76a4de912c3a60351b667d0aeb08252856e3a9445928b45` |
| 15.01 API specification | paragraphs 1315/1318 and 1322/1325 | `68955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837` |

Backlog G55 requires `RELEASED→IN_PROGRESS→EVIDENCE_SUBMITTED`, readings,
spares/photo/document and a hard prohibition on verification/closure without
required evidence. L55 requires DEV-029 evidence/files and DEV-053 maintenance
planning. Existing source implementations are engineering dependencies, not
accepted policy approvals, evidence adoption or permission to release work.

DD rows have BO=`REVIEW_REQUIRED`; AR conditional predicates, BB numeric bounds
and BF/BG/BQ controlled transition/authority cells are blank in this bounded
source. No default, approved enum catalog, person selector, operational numeric
limit or release/verification rule is derived from these empty cells.

## Scope and source-to-wire mapping

The center has 14 field occurrences and detail has 24: **38 occurrences, 29
unique source keys**. Four unique system-state keys are server-owned; vessel
context is trusted outside the declaration body. The remaining **24 unique DD
facts** plus **one backlog execution-stage declaration** produce 25 draft facts.
This count does not establish either full screen, its actions or canonical
acceptance. Shared source fields have one canonical value in the owning work
order; no duplicate detail aggregate is invented.

| Fact / context | Center DD row | Detail DD row | Transport boundary |
|---|---|---|---|
| vessel_id | 1426 | 1441 | Trusted route/server scope; forbidden client field |
| work_order_no | 1427 | 1440 | Text, required source fact; may be incomplete in draft |
| equipment_id | 1428 | 1442 | Required Reference; Known unavailable |
| job_id | 1429 | 1443 | Reference; Known unavailable, never joined to TugJob |
| work_order_type | 1430 | — | Code; no approved options, Known unavailable |
| priority | 1431 | 1445 | Code; no approved options, Known unavailable |
| criticality | 1432 | 1446 | Code; no approved options, Known unavailable |
| planned_start | 1433 | 1447 | Strict calendar Date |
| due_date | 1434 | — | Strict calendar Date |
| assigned_to | 1435 | 1451 | SourceConflict; Known unavailable |
| status | 1436 | 1463 | System state; forbidden client field |
| permit_status | 1437 | — | System state; forbidden client field |
| risk_status | 1438 | — | System state; forbidden client field |
| sync_state | 1439 | — | System state; forbidden client field |
| description | — | 1444 | Narrative |
| planned_finish | — | 1448 | Strict calendar Date |
| actual_start | — | 1449 | Strict calendar Date |
| actual_finish | — | 1450 | Strict calendar Date |
| executed_by | — | 1452 | Reference; Known unavailable |
| meter_before | — | 1453 | Opaque numeric lexical value; unit unavailable |
| meter_after | — | 1454 | Opaque numeric lexical value; unit unavailable |
| findings | — | 1455 | Narrative |
| work_done | — | 1456 | Narrative; no inferred completion |
| spares_used | — | 1457 | Text; no stock write or approved catalog |
| measurements | — | 1458 | Text; no invented unit |
| photo_evidence | — | 1459 | Reference; Known unavailable |
| document_evidence | — | 1460 | Reference; Known unavailable |
| verification_result | — | 1461 | Unassessed literal Text; no human verification record |
| verified_by | — | 1462 | Reference; Known unavailable |
| execution_stage | Backlog 55 | Backlog 55 | Literal RELEASED / IN_PROGRESS / EVIDENCE_SUBMITTED declaration |

Per-field trace IDs follow the source's exact key pattern:
`FR46-work-order-center-{key}` / `TC46-work-order-center-{key}` and
`FR46-work-order-detail-{key}` / `TC46-work-order-detail-{key}` for the rows in
this table. `execution_stage` is traced to Backlog55, not claimed to be a new DD
field. Engineering plan lineage is likewise outside the DD field count.

Every optional DD fact retains
`RequiredWhen=SOURCE_CONDITIONAL_UNRESOLVED`; source required facts and the
backlog literal have no invented conditional predicate. Missing and Unknown
remain separate and must carry null. Omitted facts normalize to Missing.

### Reference, assignment and plan boundaries

`equipment_id` has only logical candidate `Asset.id`; vessel and asset lookup
resolution explicitly do not establish a physical FK or approved selector.
`job_id` has a source conflict: maintenance jobs/templates are not TugJob.
`executed_by` has no authoritative catalog; `verified_by` mixes actor/person and
verification/decision concepts. `photo_evidence` / `document_evidence` recover
logical Evidence, not permission to adopt an arbitrary StoredFile or a verified
evidence record. Their Known facts remain unavailable.

`assigned_to` is explicitly named Assigned To but J=input/date, L=Date/Time,
M=Date and N=date. The policy preserves this source conflict rather than silently
interpreting it as either person identity or date. Unknown/Missing can be saved;
Known cannot.

An optional exact `WorkOrderPlanPin` carries id/revision/actual version/hash and
`sourceVersion=poseidon.maintenance-plan-context.v1`. This is engineering lineage
to an actual owning maintenance plan, with its actual asset dependency resolved
server-side. It never satisfies `job_id` or `equipment_id`, copies plan fields,
asserts a job catalog mapping or authorizes execution. The backend owns current
and historical source visibility, same-vessel tenant scope and fresh authority.

### Dates, readings and source state

The selected source date columns M/N are Date/date. Facts transport exact
`yyyy-MM-dd` calendar values and never infer a time, offset or browser zone.
Impossible/ambiguous dates and known reversed planned/actual ranges are rejected;
unknown/missing endpoints remain draftable. Due-date business windows remain open.

Numeric fields preserve original lexical values, including sign, leading and
trailing zeroes. A 48-character technical transport bound and plain decimal
grammar reject exponent/localized notation, units and non-finite tokens. No
rounding, precision/scale rule, non-negativity, meter delta, hours/unit assumption,
spares consumption, fitness or safety judgment is inferred.

The owning save remains **DRAFT / REVIEW_REQUIRED**. Source execution stages and
`verification_result` are unassessed declarations. None clears permit/risk,
required evidence, human verification or closure blockers. No release, stage
transition, submission, verification or closure endpoint is supplied by this
domain subpackage. No legal/safety sign-off, credential or evidence is seeded.

## Authored acceptance and remaining canonical criteria

1. Incomplete owning facts preserve Known/Missing/Unknown without hidden text,
   implicit defaults or unknown-to-ready coercion.
2. System states, tenant/vessel/actor inputs and invented fields fail closed;
   approved lookup/catalog and assignment-conflict gaps remain explicit.
3. Source calendar dates and numeric readings retain their semantics without
   unit/timezone/arithmetic invention; invalid lexical input and reversed known
   date ranges fail.
4. Requested execution stages and verification text never authorize operation,
   consume evidence or close a work order. All authority and evidence gates
   remain blockers after save.
5. Wire command and fact structures are strict, reject duplicate keys and align
   with backend/UI consumers; owning API must provide atomic audited EF writes,
   explicit actual version concurrency and exact idempotent receipts.
6. At the later bundle gate, real database/API isolation/lineage/receipt races,
   migration/seed preservation and enterprise UI/real-browser flows must pass
   independent target-commit QA. These are **open criteria**, not observed results.

Canonical DEV-054 is not complete: an approved release/transition/assignment and
permit/risk policy, required-evidence selection/adoption, attributed human
verification and evidence-gated closure are not established by this source
subpackage. Offline execution/replay also remains unavailable pending the owning
privacy and conflict policy.

## Files and verification

- [Domain policy](../../backend/Poseidon.Domain/WorkOrderPolicy.cs)
- [Wire schema](../../contracts/technical/work-order.draft.v1.json)
- [Domain test sources](../../tests/Poseidon.Domain.Tests/WorkOrderPolicyTests.cs)

Authored meaningful cases cover incomplete/unknown preservation, each execution
literal retaining authority/evidence/closure blockers, round-trip declarations,
caller-owned state/forged reference/catalog/assignment rejection, actual calendar
and culture boundaries, reversed known ranges, exact numeric preservation,
unsupported numeric notation/units, strict JSON shape, duplicate keys and text
budgets. They are **NOT RUN / DEFERRED** under the user's bundle cadence.

No test suite, build, typecheck, lint, contract generator, migration/model check,
browser/lab/provider/CI or runtime mutation was executed. No dependency was added,
no provider was provisioned and no push/PR was created. Whitespace source checks
and normal local commits are not runtime validation or independent QA.
