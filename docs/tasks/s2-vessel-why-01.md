# S2 vessel context and safe Why source lineage

Status: AUTHORED; build, tests, contract generation and independent acceptance DEFERRED to the integrated S2 bundle. This checkpoint does not accept any canonical task or add accepted story points.

## Controlled requirements

Baseline: `poseidon-v46.3-20261030`, resolved through [active sources](../sources/active.json). The original `06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx` was read at the following actual row locators, with SHA-256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.

| Requirement | Source locator | Original acceptance criterion | Scope here |
|---|---|---|---|
| DEV-020 / T-S2-001 / 8 SP | `sheet:Backlog/row:21` | IMO no, flag, class, ship type, GT, build/trading/cargo context versioned kaydedilir; provenance/freshness görünür. | Preserve existing owning profile commands, immutable revisions, provenance and observation metadata; add a full field/history/reseed regression and safe exact source lineage. UI visibility belongs to the coordinated UI package. |
| DEV-023 / T-S2-004 / 8 SP | `sheet:Backlog/row:24` | Exception cards priority/source/Why/owner/next action taşır; loading/empty/error/offline states deterministiktir. | Preserve real scoped card composition and pagination. Withhold the whole inaccessible regulatory source before cards/paging. UI state acceptance remains in the UI package. |
| DEV-024 / T-S2-005 / 8 SP | `sheet:Backlog/row:25` | Critical state için source/reason/input freshness/blocker listesi döner; unauthorized source detail sızmaz. | Explicit metadata-only source lineage; owning-source permission gate; selected observation time correctness. |

DEV-020 depends on DEV-005/DEV-014; DEV-024 depends on DEV-016/DEV-023. Earlier accepted S0/S1 engineering evidence is separate from S1 external publication and S2 acceptance. The actual original API specification `05_TECHNICAL_ARCHITECTURE/15.01_POSEIDON_v46.3_Product_Wide_API_Integration_Specification_TR.docx`, paragraphs 328–333, names `why-engine` and `/api/v1/why-engine` (SHA-256 `68955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837`). Existing implemented routes remain unchanged.

## Product changes

The existing [regulatory profile](../../backend/Poseidon.Api/RegulatoryDraftEndpoints.cs) already stores the source-required vessel fields in controlled versioned drafts. Saves preserve explicit Unknown/Missing, xmin conflict handling, exact idempotent outcomes, immutable history, attributed audited SaveChanges and add-missing synthetic imports. This package does not introduce a duplicate vessel model or claim that a declaration is verified/current.

[Today and Why](../../backend/Poseidon.Api/TodayEndpoints.cs) now reuse the owning current-profile read gate. Without `file.read`, a profile bound to a construction file is not readable: Why returns an attributed 403 before loading profile content, counts, dates, provenance or revisions. Binding/readability and conditional content/metadata are selected in one SQL statement snapshot, closing the earlier separate-binding-check/content-read race; restricted values remain null in the SQL projection. Today reports the generic RegulatoryDraft `Unavailable` coverage, omits its card and paginates only visible cards. It discloses no file identifier/reference, source time, field counts or inferred DraftAbsent result for that inaccessible source. Ordinary profiles with no restricted binding keep their existing behavior. The gate follows actual tenant/vessel scoping; it does not accept request actor/tenant/permission inputs.

Successful Why responses add optional `source` metadata. New handlers always supply it; omission remains compatible for historical/older response objects:

| Card | `source.kind` | `source.id` | `source.revision` |
|---|---|---|---|
| regulatory-draft | RegulatoryDraft | Scoped vessel ID | Current draft revision; null only when no draft exists |
| maintenance-plan | MaintenancePlan | Scoped plan ID | Exact current plan revision, matching maintenance detail |
| tug-job | TugJob | Scoped job ID | Current request revision, matching tug detail; this is not the draft-plan revision |

The object contains no source document text, reference, file pin, signature or qualification. Existing top-level reason, freshness, provenance and blocker vocabularies remain unchanged. The coordinated UI package extends strict decoding to cross-bind this optional metadata and handle unavailable regulatory coverage. Existing generated contracts are deliberately unchanged until actual host/OpenAPI/type generation at the S2 gate; this checkpoint is not generated parity evidence.

[Current maintenance projection](../../backend/Poseidon.Api/MaintenanceCurrentReader.cs) obtains assessment and actual observation time from one scoped [owning resolver](../../backend/Poseidon.Api/MaintenanceObservationEndpoints.cs) result. Why reports the selected row's observation time, including a revoked/stale declaration, and null when that selected time is unknown. It never replaces missing selected-source time with the client's legacy inline plan timestamp. Freshness remains Unverified; no unresolved age policy, stale declaration or Unknown is promoted to Current/Ready.

No new entity, migration, operational seed or mutation route was introduced. The S1 82-operation write denominator, public audit DTOs and production authentication/audit mechanisms remain unchanged. There is no seeded evidence, credential, authority or human sign-off.

## Authored verification and remaining gate

[TodaySourceLineageTests](../../tests/Poseidon.Api.Tests/TodaySourceLineageTests.cs) adds actual-host/owned-PostgreSQL test sources for:

- All source-required vessel master fields, exact decimal retention, explicit Unknown, immutable profile revisions, exact Why source revision and preservation after add-missing reseed.
- Empty and populated profiles bound by ordinary commands to a staged/scanned fictional construction file: current file permission withdrawal blocks owning profile and Why, returns identical generic unavailable feed behavior across pages, discloses no source data, restores access after the grant is restored and adds no business writes/receipts for reads. The protocol scanner peer is test infrastructure, not a deployed scanner or verified evidence claim.
- Revoked selected-observation time remaining attributable to its actual row while calculation remains unresolved.
- Unknown selected-observation time remaining null despite a client-supplied legacy timestamp.

Existing [draft Today tests](../../tests/Poseidon.Api.Tests/TodayTests.cs), [maintenance Today tests](../../tests/Poseidon.Api.Tests/TodayMaintenanceTests.cs) and [tug Today tests](../../tests/Poseidon.Api.Tests/TodayTugTests.cs) add exact source assertions, absent-vs-saved-incomplete profile semantics and current request-vs-plan revision assertions.

## S2-QA-001 history disclosure correction — authored, not validated

Independent source preflight on integrated commit `54679c560dc4ddad3c4ad21196090dd44f5f0e03` identified HIGH `S2-QA-001` in the existing profile history handler: an initial unrestricted-history check followed by a separate unfiltered page query could disclose a first file-bound revision appended between those statements. The issued preflight remains historical evidence (SHA-256 `502ad9c52b29f54169a4f289e47e2af8592d07a9b4b4342ce2f4a5d38d3780b0`); this authoring correction is not independent acceptance or execution evidence.

The [history handler](../../backend/Poseidon.Api/RegulatoryDraftEndpoints.cs) now uses an explicit single-query projection rooted in the scoped vessel. The whole-history restricted-binding predicate controls both its readability flag and its child page in the same PostgreSQL statement snapshot. If any revision binds a construction file and the reader lacks `file.read`, the child projection returns no content, dates, provenance or revisions, and the handler returns the existing whole-history 403. This remains true when the restricted revision lies outside the page or the offset is beyond the end. It does not return a partially filtered history, a count or a substituted empty page. Invalid paging keeps the previous permission-denial precedence and loads no child content. Existing authenticated actor/vessel/tenant scoping and attributed HTTP audits remain in place. No read locks, write path, migration or operational fixture are added.

[RegulatoryHistoryScopeTests](../../tests/Poseidon.Api.Tests/RegulatoryHistoryScopeTests.cs) authors two real-PostgreSQL/HTTP regressions. A deterministic command interceptor pauses the first history statement after execution but before EF consumption; an ordinary authorized writer then commits the first fictional file-bound profile revision before the reader resumes. The response must contain the earlier unrestricted snapshot or a 403, never a mix containing the concurrent restricted content. The earlier two-statement implementation would expose the new revision. A subsequent request must deny the now-restricted history. The second test removes the file binding from the current head through another ordinary command and checks that the retained restricted older revision still denies every requested page, including an out-of-range offset; another vessel's unrelated history remains readable, cross-vessel/tenant selectors remain denied, and original immutable revisions remain unchanged and reject mutation. These tests use the existing owned temporary file fixture and protocol scanner peer, not verified evidence or a production scanner. Both tests are AUTHORED ONLY; compilation, provider translation, real PostgreSQL interleaving and independent review remain DEFERRED to the S2 bundle gate.

Actual verification in this checkpoint: bounded original source retrieval and manual source/diff review only. `git diff --check` is a formatting check, not compilation or test evidence. Backend/unit/PostgreSQL, frontend/component, browser, full build/typecheck, model/contract generation and independent QA remain DEFERRED. No test has been skipped, run or marked PASS here. The coordinator will run the AST-only Graphify update once in the integrated S2 worktree after both packages are combined. At the integrated gate, run these new cases with the existing owning-profile, cross-tenant/role, Today, audit, S1 coverage and full required phase checks against the final integrated commit. Canonical S2 completion also requires the coordinated global context/role navigation, deterministic UI states and accessibility criteria; this backend checkpoint alone cannot satisfy those UI acceptance claims.
