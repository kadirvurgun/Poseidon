# Operational downtime — bounded owning declaration domain

Status: **SOURCE AUTHORED; all execution DEFERRED; DEV-102 and canonical S6 acceptance OPEN**.

Task DEV-102 / T-OPS2-006, S6, 8 SP. This domain/wire subpackage permits an
incomplete operational declaration. It does not deliver verified closure,
accepted Loss of Hire determination, claim evidence adoption or accepted
dependency lifecycles. API/persistence and UI are coordinated separate source
work packages; no local source commit is validation evidence.

## Authority, locators and unresolved gates

The [active source baseline](../sources/active.json) is
`poseidon-v46.3-20261030`. Bounded `query_sources.py` retrieval and exact selected
source-index locators preceded direct standard-library XLSX extraction of all
14 original dictionary rows. The dictionary file SHA-256 was checked before
reading the selected cells; the full chunks archive was not loaded.

| Source | Exact locator | SHA-256 |
|---|---|---|
| 18.01 Development Backlog | `Backlog!A103:R103` | `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674` |
| 14.02 Product-Wide Data Dictionary | `Data Dictionary 2316`, rows 2226–2239 | `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018` |
| 11.01 Controlled Traceability | `2316 Alan v46.3`, rows 2226–2239; `196 Ekran v46.3`, row 192 | `bc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9` |
| 13.02 Visual Screen Catalog | paragraphs 2154–2157, screen 191 | `b0b5e9fb3cad5137d76a4de912c3a60351b667d0aeb08252856e3a9445928b45` |
| 15.01 API specification | paragraphs 1649–1651 | `68955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837` |

Backlog G103 requires start/end/duration/cause; defect/WO/event links;
operational impact; Loss of Hire candidate; claim evidence; verified closure.
L103 names DEV-055 and DEV-099. Existing defect, work-order and daily-log source
foundations are engineering dependencies; their presence does not establish
accepted closure/evidence/claim policies. Canonical dependency acceptance and
full backlog criteria remain open.

Original dictionary D is `Gemi` (Ship) for every selected row. Q/AR identify
six required fields; conditional AR predicates are blank. AO is
`NO_DEFAULT_DECLARED` throughout. Approved BB numeric formula/range/unit,
BF/BG edit/state policy and BQ approval evidence cells are blank. BO is
`REVIEW_REQUIRED`. Mutable reference AS/AT mappings are blank even where BT
states `RECOVERED_ENTITY_AND_READ_API`; recovered reads do not authorize
physical or approved semantic joins. UI catalog paragraph 2155 says four
actions while dictionary `Screen Coverage 196` row 192 says six actions; this
subpackage neither resolves that discrepancy nor claims full-screen acceptance.

## Source-to-wire decisions

| Source field | DD row | Owning behavior |
|---|---|---|
| vessel_id | 2226 | Required trusted current authorized Ship context; client writes rejected |
| voyage_id | 2227 | Conditional Reference; Missing/Unknown retained, Known unavailable |
| downtime_type | 2228 | Required candidate Code: Technical Failure, Planned Maintenance, Weather, Port Delay, Crew, Regulatory, Commercial, Other |
| start_at | 2229 | Required source Date/Time; explicit-offset lexical instant transport |
| end_at | 2230 | Conditional source Date/Time; explicit-offset lexical instant transport |
| duration_minutes | 2231 | Conditional lexical Number; source label `min`, no approved formula/range/precision |
| cause | 2232 | Required Narrative |
| linked_defect | 2233 | Conditional Reference; Known unavailable |
| linked_work_order | 2234 | Conditional Reference; Known unavailable |
| linked_event | 2235 | Conditional Reference; Known unavailable |
| operational_impact | 2236 | Required Narrative |
| loss_of_hire_flag | 2237 | Server-only source State with YES/NO/REVIEW_REQUIRED candidate options; returned fact Unknown/null |
| claim_evidence | 2238 | Conditional Reference; Known unavailable |
| closure_state | 2239 | Required server-only source State with OPEN/VERIFIED/CLOSED candidate options; returned fact Unknown/null |

The contract exposes **14 source fields / 11 mutable facts**. Omitted mutable
facts become Missing/null; explicit Unknown/null remains Unknown. Required or
unresolved conditional facts block later completion while allowing drafts.
Caller-supplied readonly, unknown keys, malformed/null facts and ambiguous JSON
are rejected. Unknown does not become NO, OPEN, NotApplicable or a legal
REVIEW_REQUIRED decision. Top-level engineering `DRAFT` and qualification
`REVIEW_REQUIRED` are separate from the two unassessed source state facts.

L2229/2230 says Date/Time while M/N retain text. The recorded technical
transport decision requires a complete ISO instant with seconds, optional
1–7 fractional digits and explicit Z/offset; unknown zone `-00:00` is rejected.
Original lexical offsets survive unchanged. Typed UTC projections provide
ordering; known end preceding known start is rejected without inventing the
missing endpoint or a local timezone.

Declared minutes retain their lexical value, sign, zeros and up to the technical
48-character number budget. No positivity, precision/scale, rounding,
conversion, consistency formula or compensation threshold is approved by the
selected source. A separate readonly interval observation provides exact
100 ns ticks **as a JSON string** only when both endpoints are Known and
ordered. It is UNASSESSED, unavailable for incomplete endpoints, never uses
the running clock and never fills or overrides declared minutes. It does not
measure legal hire time or prove Loss of Hire.

An optional `DowntimeOriginPin` carries kind Defect/WorkOrder/Engine plus exact
id/revision/actual uint concurrency version/hash/sourceVersion. It is explicitly
outside the 14 DD fields. Backend owning reads must separately prove scope,
current permissions, exact immutable revisions and current source for a new
pin. This type does not authorize a source lookup, copy protected source facts,
adopt StoredFile/Evidence as claim evidence, mutate the source or grant closure.

Per-field traceability retains `FR-operational-downtime-{field}` and
`TC-operational-downtime-{field}` from source AG/AH. No source action, human
sign-off, insurance determination or offline grant is manufactured.

## Authored acceptance cases and verification

Implemented policy and wire live in
[DowntimePolicy](../../backend/Poseidon.Domain/DowntimePolicy.cs) and the
[draft contract](../../contracts/ship-operations/operational-downtime.draft.v1.json).
[Domain test sources](../../tests/Poseidon.Domain.Tests/DowntimePolicyTests.cs)
cover incomplete/unknown drafts; readonly state/scope injection; unavailable
references despite plausible UUIDs; exact candidate options and source
conditions; lexical duration preservation; explicit-offset chronology; unknown
endpoint and zero intervals; exact subsecond and very large lossless tick
observations; malformed/duplicate/unknown-member stored JSON; narrative
round-trip and diagnostic redaction.

Tests, builds, typechecking, contract generation, lint, browser/lab, migrations,
runtime/provider work and CI were **NOT RUN / DEFERRED** under the user's
larger-package cadence. Tests are authored cases, not passing evidence.
Independent acceptance remains required before publication or merge to the
accepted branch. This worker had no local graph artifact; graph discovery used
the main checkout's bounded existing graph, which did not surface this new
aggregate. The integration owner will refresh the AST graph after source merge;
that graph remains navigation data, not verification evidence.
