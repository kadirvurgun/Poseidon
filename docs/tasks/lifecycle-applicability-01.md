# LIFECYCLE-APPLICABILITY-01

AUTHORED_UNVERIFIED / NOT ACCEPTED. Software authoring before
REGULATORY-CHAIN-BUNDLE-01. Every product check is DEFERRED / NOT RUN.
This is a finite draft-authoring/interpreter/configured-deny delivery, not complete
DEV-026/027, canonical sprint acceptance, legal adoption or foundation readiness.
Selected40 tasks/272SP and canonical8/970SP remain unchanged;194/970 is a target,
not earned or accepted progress. DEV-005/008/020 remain explicitly OPEN.

## Receipt and predecessor reconciliation

Receiving HEAD `8e069bcf3fa56bbf0696d788376c7fa266f31dd2` was clean. Read current
AGENTS, backend/UI skills, active30.10.2026 baseline, architecture, automation,
security, seed and dependency policies. Read actual
[latest continuation](../phases/FOUNDATION-CONTINUATION-2026-11-02.md), its JSON
evidence and [selected source report](selected-source-reconciliation-2026-11-02.md).
Historical80b remains the independent code baseline; no later author tests become
passing evidence by inheritance. MCP graph tools were unavailable; local scoped
Graphify query preceded targeted source reads. No agents or private setup.

Before product edits, read prepared origin
`af3c97109c8bbbf13d8ae12c3ef33ee92410a65e` and its task through git show.
It already exists as receiving8e069bc with normal `-x` attribution. Inspected actual
decoder/tests: actual permitted string required, arrays/objects are not coerced,
conversion spy negative and invalid-calendar negative cases both retained, shared
`date-validation.timestamp` retained. No duplicate cherry-pick; snapshot-basis
`300cfe35a025267282a399d00d50f44d73e5821a` was not integrated. This records source
inspection, not an executed decoder result. Coordinator's pending receipt/source
reconciliation before paid QA still applies.

## Controlled locators and traceability

[Exact bounded source extracts and source hashes](lifecycle-applicability-01.sources.json)
preserve39 locators. They are requirement evidence, not accepted maritime facts.
`query_sources.py` bounded phrase retrieval preceded exact indexed locator reads;
no whole chunks corpus was loaded into model context. XML-only paragraph numbering
was not substituted for canonical indexed locators.

| Source | Locator / application |
|---|---|
|18.01 Backlog|Backlog27 DEV-026/T-S3-001/8SP;28 DEV-027/T-S3-002/13SP. Versioned lifecycle without current-source overwrite; flag/type/GT/build/cargo/trading predicates and reasons.|
|00.02 Controlled Register|Controlled Register16:09.02 CURRENT-CONTRACT/P0 required with09.01;17:09.03 CURRENT-CONTRACT.|
|09.01 specification|paragraph5,12–13,18–41: no false green, separate visibility/compliance, context/reasons/replay;42–58: three values/critical blockers/controlled unknown policy;124–146: exact six lifecycle states;148: versioned DSL;183–207: operators;234–237: conflicts, no numeric precedence, human override does not rewrite source.|
|09.02 matrix|Input Catalog2–24; API Contract2–9; Rule Lifecycle2–7; Conflict & Resolution2–9; Golden Profiles2–7; Golden Tests2–7 (GOLD-001..006), plus10/11 referenced future behavior.|
|09.03 schema|Exact contract SHA309afa07b393fb96556a0ea4ec70098b1e2b8648ab6daec181f5a81ce9ac8825. Local version identity; no URL/schema download.|
|09.04 examples|Exact sample bytes copied into test-only contracts/golden/applicability.sample-rules.json. All exampleOnly/DRAFT/PENDING annotations retained. Never runtime seed/catalog.|
|09.05/09.06/09.07|Read request, response and OpenAPI support files. Example caller-shaped facts are not trusted server authority.09.07 evaluate explicitly rejects example/non-PUBLISHED/unverified sets; simulate is non-mutating.09.06 says illustrative outputs confer no authoritative activation/readiness/approval.|
|14.01 ERD|paragraph119–124 RegulatorySource / RuleVersion; source lifecycle, effective dates and approval state. Tenant-local design retained rather than optional global records.|
|14.02 dictionary +11.01 trace|Data Dictionary2316 rows15/17–21 and1342 Alan rows15/17–21: requirement_id, version, legal_state, effective_date, applicability_rule, responsible_role; FR-/TC-requirement-master-{field}. Responsible role requires a real active scoped binding; no user-entered role label is approval.|
|13.02 screen catalog|paragraph1354/1355/1357 requirement-master;1374/1375/1377 source-governance. Projections remain BA/DEV-review candidates; no invented approved field/codebook.|
|15.01 API|paragraph104–108 simulation versus persisted authoritative evaluation;531–534 requirement-master projection. Existing UTC/OIDC/tenant/scope/receipt/error controls retained.|

## Delivered behavior and acceptance cases authored

| Local trace | Actual authored software | Meaningful test source |
|---|---|---|
|LA-01 / T-S3-001|Tenant-local RuleDraft head with xmin; append-only RuleRevision, pinned09.03 contract/content hash and exact tenant Source/Requirement revision FKs. Null document/links allow incomplete drafts. Rule ID stays immutable across later documents. Every no-op appends/checks version. Source/requirement mismatch rejected.|Domain/PG/API sources below.|
|LA-02 / FR/TC-requirement-master-legal_state/version/effective_date|All exact states DRAFT, ADOPTED, AWAITING, FUTURE_EFFECTIVE, IN_FORCE, SUPERSEDED can be authored/proposed. Actual stored lifecycle remains DRAFT. Explicit review capability/context/reason records requested state and BLOCKED_SOURCE_ADMISSION in a new immutable revision, never a signature or approval. No clock/hash/upload/role-string adoption.|Boundary, review, wrong-scope, receipt and immutable-history cases.|
|LA-03 / T-S3-002|Bounded typed interpreter for all/any/not and supported scalar operators; every predicate contributes a reason and review blocker. Strong Kleene truth retained separately from review disposition. False AND unknown stays false at predicate truth but REVIEW_REQUIRED at rule disposition. True OR unknown also retains review. No arbitrary code/reflection/SQL/JS evaluation.|Nine pair truth combinations, NOT, mixed false/true with Missing/Unknown/Stale/Unverified/Conflicting, malformed/deep/mixed/duplicate DSL, zero/false/exact decimal/date cases.|
|LA-04|Audited EF author/review commands, atomic revision/head/audit/exact receipt, tenant composite FKs, safe controlled reason codes, original xmin on retries, reauthorization before receipt. Unique/concurrency failures roll back then resolve same receipt or conflict; storage failures become safe503.|Actual-host/PG authored race, retry after later save, reused key, no-op conflict, receipt-failure rollback and capability denial.|
|LA-05|Server context resolver reads real scoped vessel and immutable regulatory profile revision as-of. Versioned23-field catalog; unavailable owning adapters stay Unknown/AdapterUnavailable. Draft Known declarations remain Unverified, with exact values and revision provenance. Numeric inputs remain strict decimal strings; rule numeric constants use exact JSON numeric lexemes parsed without floating point.|PG and API changed profile/rule snapshots; missing/zero/false/numericprecision/freshness domain cases.|
|LA-06|POST simulate resolves real server context, computes bounded reasons and exact snapshot/rule-set hashes; returns simulation=true, preview=true, authoritative=false. Keeps candidate obligation conflicts and both rule references without priority winner. No evaluation/activation/readiness/domain receipt is persisted.|Golden/contract/host non-mutation and conflict cases.|
|LA-07|POST evaluate is explicitly configured-denied409/source_admission_pending after live capability/vessel scope. GET evaluations history/detail is tenant/vessel scoped before paging/Why and returns only retained rows. Immutable evaluation/link/receipt storage and DB guards are authored for later admitted chain; no production write path bypasses the admission gate.|API evaluate denial/no trace and denied detail; PG composite lineage, rollback and append-only guards.|
|LA-08|Real React work area reachable from source/requirement register list. TR/EN labels, keyboard controls, responsive forms, rule/history editor, review requests, server vessel selection, as-of with offset, non-mutating preview reasons/conflicts and retained-evaluation reader. Strict closed response decoders reject authority expansion/private fields. Dirty-context discard, loading/empty/error/offline/permission/conflict and retained-command retry states. Exact ruleJson text avoids JS rounding of DSL constants.|Component/contract behavior cases including exact numeric-text transport; two registered genuine-host browser journeys; NOT RUN.|
|LA-09|Idempotent synthetic seed adds one incomplete DRAFT head/revision per existing tenant; no legal rule, evidence, reviewer, evaluation, receipt, credential, sign-off or observed input. System seed actor remains attributed by ordinary audited import. Reseed preserves operator work.|PG seed/preservation/tenant tests. Existing two-tenant audit expectations80/81 become84/85 as authored expectations only, never observed counts.|
|LA-10|Exact controlled sample/golden interpretations retained. GOLD001/002 project needs only;003 unknown class→review;004 applicability is independent of unverified MSMD/readiness;006 known ocean-only exclusion. GOLD005 has a concrete missing validated escort rule in09.04 and remains unavailable rather than fabricated.|ApplicabilityGoldenTests explicitly records aliases and missing escort source; does not assert a qualified manning/escort evaluator.|

## Explicit contract decisions, limits and open software

The supplied request sample's context/tenant/facts are illustrative; the real
request takes server-resolved vesselId, selected rule IDs and optional offset asOf.
No client ActorContext, admin seed, IgnoreQueryFilters, verification booleans or
arbitrary source authority is admitted. Tenant membership is resolved anew on each
request. Catalog read additionally requires tenant-wide regulatory draft scope.
Default-deny explicit capabilities: applicability.read, applicability.evaluate,
rules.write, rules.review. None is provisioned by this task. DPA/Regulatory Admin/
Control Owner source role names remain source responsibilities, not grants or
signed authority. RoleContext must come from provisioned assignment references.
Author commands additionally reject caller publication/source-verification claims
other than DRAFT/PENDING; lifecycle values are proposed instrument states only.

The UI/DTO uses nested prediction plus independent currentResult, snapshot and
conflict records, not a falsely compatible copy of09.06's flat illustrative DTO.
`simulation=true` is explicit. Actual generated OpenAPI/TS consumer reconciliation
is DEFERRED. No authoritative200 is currently supported. Persisted authoritative
evaluation creation, source admission/owner/reviewer qualification, separation of
duties, signed authority and current obligation projection remain **software
integration work** for the retained control/evidence/human chain, as well as
genuine external input/approval gates. Storage/history scaffolding is not that
write workflow. No persisted simulation is passed off as historical evaluation.

UTC is the explicit preview calendar; asOf is normalized to UTC/microseconds and
cannot be future. Effective-from is inclusive; effective-to uses an inclusive
calendar day in this preview version. ADOPTED/AWAITING/FUTURE_EFFECTIVE never turn
IN_FORCE when the clock passes; SUPERSEDED never emits new current need. These
calendar assumptions need source-owner review before any authoritative adapter.
Historical profile revision is resolved as-of; vessel mode/version is the actual
server object at capture, not an invented reconstruction of old vessel changes.
No complete historical source/profile replay authority is claimed.

Runtime limits are conservative authoring bounds:32KiB rule,49KiB command,
depth8/128 nodes/32 children or list values/64 activations,64 selected rules.
Root/field/operator/version/shape/duplicate/enum/date/decimal validation rejects
invalid inputs rather than silently matching. The09.03 numeric-comparison constant
is a JSON number (its exact lexeme), while operational measurements retain existing
decimal strings. UnknownPolicy is fixed to REVIEW_REQUIRED in this package.
NOT_APPLICABLE/FAIL_CLOSED config cannot be caller-selected; even PRODUCT_CONTROL
does not itself establish a qualified fail-closed control policy.

09.02's v1 admitted input paths contain no typed collection adapter. CONTAINS is
implemented/tested in the collection primitive; runtime rule validation rejects
using it on the scalar catalog or invented paths. A qualified collection adapter
is an explicit remaining software/catalog binding, not fake text-substring support.
Unknown collection is not empty; EXISTS false requires authoritative meaningful
emptiness. No approved vocabulary is invented for class/job/cargo examples.

Source tension is retained:09.06 mode mismatch examples can suggest short-circuit
exclusion, whereas09.01 paragraph56/09.02 critical-unknown rows and the task require
material review blockers not be erased. This implementation conservatively keeps
all unknown blockers; all-known ocean exclusion still returns NOT_APPLICABLE.
Numeric priority never resolves legal conflict. Candidate contradiction records
are review requests, not legal precedence or actual source-owner adjudications.
Documents49/50 proposals supply no new approved roles/catalogs/permits.

All S0 contracts and encrypted medical/profile/import, identity/person files/photo/
legacy, watch-group capture/replay, Tug planning/navigation and their prior test
source are preserved. No dependency/version/lock change. No auth/account setup,
provider, real maritime data, source adoption, credential, human approval or safety
decision was created. EF audit is not claimed as complete HTTP/privacy compliance.

## Changed source/test inventory and deferred handoff

Product: ApplicabilityEngine.cs; ApplicabilityStorage.cs;
20261102140000_LifecycleApplicability.cs; ApplicabilityFixtures.cs;
ApplicabilityContextResolver.cs; LifecycleApplicabilityEndpoints.cs;
FoundationIdentity.cs; Startup.cs; PoseidonDbContext.cs; AuditMetadata.cs;
SeedImporter.cs; LifecycleApplicability.tsx; applicability-api.ts;
RegulatoryRegister.tsx; product.css.

Tests: ApplicabilityEngineTests.cs; ApplicabilityGoldenTests.cs;
LifecycleApplicabilityPersistenceTests.cs; LifecycleApplicabilityTests.cs;
DirectoryWorkspaceTests.cs host capability fixture; LifecycleApplicability.test.tsx;
applicability-api.test.ts; lifecycle-applicability-cases.ts and common browser
manifest; PostgresSeedTests.cs expected-count additions; Domain.Tests.csproj
test-only sample inclusion; exact test-only sample JSON; RegulatoryRegister.test.tsx
dirty-entry regression and .gitattributes preserving test-source bytes.

All unit/PostgreSQL/API/React/Playwright tests, build, lint, typecheck, restore,
actual OpenAPI/contract generation, model/designer/snapshot generation/parity,
migration application, seed counts, scans, private provider/lab/browser and
independent QA/CI are **DEFERRED / NOT RUN**. No execution/discovery/pass/skip
counts, duration, tested compatibility or acceptance is claimed. The DDL migration
and runtime model are authored; designer/snapshot reconciliation is still required
at the combined gate, not silently described as synchronized generated evidence.

Only source retrieval, code/test/document authoring, whitespace inspection and
Graphify AST maintenance are permitted now. See the
[phase report](../phases/LIFECYCLE-APPLICABILITY-01.md) for exact source-only results.
Coordinator owns target-receipt/source review, later independent QA and publication.
Next: CONTROL-EVIDENCE-01 then HUMAN-DECISION-TRACE-01, reconcile the entire retained
foundation branch and run one REGULATORY-CHAIN-BUNDLE-01 gate. No acceptance here.
