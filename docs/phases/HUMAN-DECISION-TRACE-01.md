# HUMAN-DECISION-TRACE-01

Status: **AUTHORED_UNVERIFIED_NOT_ACCEPTED**. Current phase: regulatory chain product
authoring before REGULATORY-CHAIN-BUNDLE-01. This is not canonical sprint/release
acceptance or a locally validated package. Canonical8/970 and the selected40-task/
272SP ledger remain unchanged;194/970 is still a target.

Delivered source includes server-appointed human review commands, immutable
verification/decision events, reasoned rejection/revocation, expiry/supersession,
tenant/vessel composite persistence, audited atomic xmin/receipts, filtered trace
projection and real TR/EN review UI. There is no actual operational approval,
permit, credential, signature, human appointment or adopted source produced here.

Exact source/dependency/FR/TC/field/API/role locators, authored acceptance criteria,
state/command contract and limitations: [task](../tasks/human-decision-trace-01.md).
Predecessor3dba9c6 was clean and its29 recorded source files match the supplied
proof bytes; see [comparison](HUMAN-DECISION-TRACE-01.predecessor.json). This is a
source-only observation, not predecessor acceptance. Previous code/tests remain.

Product files:

- backend/Poseidon.Domain/HumanReviewPolicy.cs
- backend/Poseidon.Persistence/HumanReviewStorage.cs
- backend/Poseidon.Persistence/Migrations/20261102160000_HumanReviews.cs
- backend/Poseidon.Persistence/PoseidonDbContext.cs and AuditMetadata.cs
- backend/Poseidon.Api/HumanReviewEndpoints.cs, FoundationIdentity.cs, Startup.cs
- frontend/src/product/HumanDecisionTrace.tsx and human-review-api.ts
- frontend/src/product/ControlEvidence.tsx and product.css

Authored tests, all NOT RUN:

- tests/Poseidon.Domain.Tests/HumanReviewPolicyTests.cs
- tests/Poseidon.Persistence.Tests/HumanReviewPersistenceTests.cs
- tests/Poseidon.Api.Tests/HumanReviewTests.cs
- frontend/src/product/HumanDecisionTrace.test.tsx
- frontend/src/product/human-review-api.test.ts and human-review.test-fixtures.ts
- tests/Poseidon.Browser.Tests/register-specs/human-review-cases.ts and cases.ts

Exact changed-file byte inventory and source-only observations are recorded in
[evidence](HUMAN-DECISION-TRACE-01.evidence.json). All domain/PostgreSQL/API/React/
Playwright, build/lint/typecheck/restore, actual OpenAPI/client/EF generation,
designer/snapshot/model parity, migration/seed, real provider/scanner/browser/lab,
security scans and independent QA/CI checks are **DEFERRED / NOT RUN**. No passed,
failed, skipped, discovered or duration results are claimed.

Remaining blockers: predecessor source admission/current-obligation/authoritative
evaluation integration; qualified freshness/source-specific acknowledgment and
external signature verification; genuine human appointments; final decision
adapter; broader source/screen contract gaps; actual generated model/consumer
reconciliation and later full bundle execution. These remain software integrations
and external authority gates, explicitly distinguished in the task. Current
approving/final decision commands fail409. Trace completeness stays false with
REVIEW_REQUIRED and actual gaps, even for synthetic verified test branches.

Next: coordinator reviews this local UNVERIFIED commit, continues missing admitted
source/evaluation/authority adapters, then runs one REGULATORY-CHAIN-BUNDLE-01 and
independent exact-target QA/required CI. No publication, auth/account/private config
change, extra agent, self-approval or canonical SP inflation occurred.

Source-only observations: `git diff --cached --check` exit0 with no whitespace
errors (line-ending normalization warnings only). Final `graphify update .` exit0:
7334 nodes,15784 edges,511 communities;50 zero-node source warnings; oversized
HTML omitted. This was AST maintenance only, no model/source approval or passing
product evidence. Source inventory captures staged bytes; the evidence manifest
itself is excluded from its own recursive hash list. Product checks remain NOT RUN.
