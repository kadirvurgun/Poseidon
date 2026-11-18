# S8-DEFECT-DOMAIN-01 — incomplete critical-defect declarations

Source authoring only; all new execution is **DEFERRED / NOT RUN** by the active user cadence. This is a DEV-055 dependency package for the open S6 DEV-102 work, not accepted S8 or completed S6.

## Authority and dependencies

- Active baseline: `poseidon-v46.3-20261030`.
- DEV-055 / T-S8-004 / 13 SP: 18.01 Backlog row56, SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`. Literal acceptance: “Detect→Assess→Safeguard→Action→Verify→Effectiveness; critical defect without safeguard cannot close; temporary measure expiry monitored.”
- Dependencies DEV-030 and DEV-052 have existing engineering evidence/audit and versioned Asset/AssetRevision sources. Their existence does not approve a defect evidence lookup or satisfy a canonical acceptance gate.
- Supporting declaration fields: 14.02 Data Dictionary 2316 rows447,457,461–463,465, SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`. These belong to `asset-reliability`; this package does not claim to implement that entire 23-field screen.
- DD448 status candidates and DD450/464 human decision fields do not provide an approved closure/return-to-service transition matrix. DD conditional AR and BF/BG/BQ gaps remain open. No claimed expert or Master sign-off is created.

## Engineering capture contract

[DefectPolicy](../../backend/Poseidon.Domain/DefectPolicy.cs) and [wire schema](../../contracts/technical/defect.draft.v1.json) retain 11 optional facts with explicit Known/Missing/Unknown states. Omitted facts become Missing, never No/zero/NotApplicable. Supporting source fields retain their source locator; restriction/action/temporary measure/expiry/lifecycle stage derive from Backlog56 and are engineering declarations.

`lifecycle_stage=Verify` is literal unassessed capture. It does not attest that verification occurred. The owning aggregate remains a declaration requiring review. Neither stage nor a written safeguard enables closure, effectiveness or safe service. Required human/evidence/transition policies remain unavailable, including for fully populated drafts.

The optional exact asset pin is engineering lineage, not an invented physical mapping for DD451 `asset_name`. Only an actual authorized current Asset head can establish a new/changed pin. AssetRevision JSON snapshots were written before SaveChanges; their embedded Version is not a trustworthy committed xmin. The initial context must use the actual current head xmin and exact immutable snapshot hash. Existing established historical pins retain their captured version; no historical committed version is fabricated.

## Acceptance cases and authored checks

- DEFECT-DRAFT-001: permit incomplete drafts while preserving Unknown and blocking readonly/unknown properties or hidden values.
- DEFECT-SOURCE-002: bind new asset context to actual tenant/vessel/source revision and current xmin; preserve immutable historical lineage under current rights.
- DEFECT-TIME-003: compare explicit-offset instants at an injected server instant; equal expiry is expired. Return assessedAt with a time-relative declared observation. No measure/deadline yields Missing/Unknown, never a clear safeguard.
- DEFECT-AUTHORITY-004: reject Closed/Verified/approved/verifier commands; retain human verification, effectiveness, closure and offline blockers even for a literal Verify declaration.
- DEFECT-TRANSPORT-005: reject duplicate properties, ambiguous local dates, unknown offsets and unsupported source codes; retain original lexical offset and narrative formatting.

[Domain test source](../../tests/Poseidon.Domain.Tests/DefectPolicyTests.cs) covers partial/uncertain inputs, exact UTC expiry boundaries, rejection of unapproved decisions, immutable roundtrip and duplicate JSON keys. API/real PostgreSQL/component/journey sources are delivered with their owning tasks. No source review, local commit or AST navigation result is runtime QA or acceptance.
