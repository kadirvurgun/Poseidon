# S6-DOWNTIME-CORE-01 — authored source foundation / NOT RUN

DEV-102 (S6,8 SP), T-OPS2-006. Active source `poseidon-v46.3-20261030`.
Backlog 18.01 row103 (SHA256 09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674);
DD14.02 Data Dictionary2316 rows2226–2239 and Screen Coverage196 row192
(SHA256 3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018);
11.01 196 Ekran v46.3 row192 and 2316 Alan v46.3 rows2226–2239
(SHA256 bc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9).
Source classes remain candidate/unverified. DEV-055 critical-defect and DEV-099
Engine owning drafts exist as source only; their actual validation/closure gates
remain open. This is not a completed canonical DEV-102 or accepted S6 gate.

## Delivered source behavior and acceptance criteria

- Actual owning `/api/v1/operational-downtime` list/capabilities/detail/history and
  POST/PUT support incomplete declarations with tenant/current-role/actual Ship
  vessel scope and explicit `operations.downtime.read/write`; no mode.cross Tug
  bypass. Closed JSON, bounded body/query, antiforgery, UUID keys, source pin,
  expected actual xmin, actor-exact fingerprint/receipt and immutable snapshots.
- Domain contract covers14 DD fields/11mutable. Trusted vessel is derived, LoH
  flag and closure state remain Unknown. Fixed engineering DRAFT/REVIEW_REQUIRED
  cannot grant VERIFIED/CLOSED, financial insurance/hire calculations, source
  approval, return to service, expert appointment or a safety decision. Empty
  drafts work; generic unresolved reference Known values fail closed.
- `engineeringInterval` is an unassessed exact lexical tick count over two captured
  explicit-offset timestamps, not a replacement for the source's declared min.
  UTC StartedAt is only an ordering projection; lexical declaration remains exact.
- Separate optional engineering origin binds real Defect/WorkOrder/Engine exact
  revision/version/hash/source. It never fills the unresolved DD linked_* refs or
  copies text into draft fields. New/changed pins must be current. Established
  unchanged historical pins remain usable only with current access, not rebased.
- Context GET exposes bounded actual source facts, fixed unqualified state and
  canonical href, excluding responsible people, owner IDs and evidence contents.
  Current-only source launch rejects a stale explicit revision. Capabilities expose
  only origin kinds actually readable with current source permissions.
- Fresh scope resolves actor outside old transactions. Returned ledger revisions,
  owning current head, returned AND current origin heads, nested Defect assets and
  WorkOrder plans plus current plan/asset head metadata compose into ONE final fresh
  SQL statement. Final synchronous membership fingerprint follows the await; the
  same fence repeats after mandatory durable read audit. No multi-query boolean
  gap is substituted for final composition.
- Caller-owned SERIALIZABLE transaction uses audited SaveChanges for head,
  revision and receipt; PostgresFailure classifies any wrapped 40001/40P01 and
  unique-race failure. Exact immutable receipt is not editable current state.
  A postcommit authorization/audit problem withholds payload with503 while
  preserving the committed record and exact retry key/outcome.
- EF migration09010000, snapshot, tenant vessel and receipt/revision FKs, xmin,
  retention/ownership/append-only guards, attributed allowlisted AuditMetadata and
  security vocabulary are authored. Source inventory115 mutation templates,
  107 critical templates/117 concrete critical operations is a definition only.
- Optional administrative fixture authors only a missing incomplete fictional
  Ship draft and immutable revision; existing operator changes survive reseed.
  It is not startup activation or normal demo import; no origin, receipt, financial
  assertion, evidence, closure, signature, credential or real user is seeded.

## Test sources and exact execution status

`DowntimeTests` authors actual host/real PostgreSQL cases: exact receipt recovery,
MVCC/version history, audit attribution/content exclusion, tenant/Ship/mode/role
scope, closed JSON/source/readonly state denial, three actual typed origin kinds,
current-only new binding versus retained history, forged pins, source revocation,
transitive asset/plan context, optional reseed/operator preservation, UTC page
ordering, same-key duplication and competing-version one-effect behavior.
`DowntimeFenceTests` authors advisory-barrier tests for read-audit head/role races,
three source-head changes during attempted audit and rollback, postcommit
revocation/withheld receipt reauthorization, actual DB append-only/ownership and
cross-tenant FK rejection. S1 exhaustive source counts include the two new writes.

All tests/build/typecheck/lint/contract generation/migration generation or
application/browser/lab/provider/CI/runtime verification are **DEFERRED / NOT RUN**
by active user cadence. Normal local commits and bounded source reads are not PASS.
Root integrates Startup/shared navigation and UI, then one AST-only refresh; no
push, PR, preview activation, deployment or publication occurs in this package.

## Remaining gates

Actual build/model parity/migration application, real API/PostgreSQL execution,
independent QA/CI and complete genuine actor critical-write captures remain open.
Approved conditions/ranges/units beyond explicit source labels, referenced lookup
catalogs, LoH decisions/claim evidence/closure authority, full offline enrollment
and conflict journey are unavailable. Offline capability remains UNAVAILABLE.
DEV-055 declaration or DEV-054 work-order draft does not establish defect closure,
verified maintenance or an accepted safety chain. All canonical acceptance stays
unchanged until its actual gates pass.
