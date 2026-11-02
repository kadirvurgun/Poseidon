# PR12 semantic source evidence — 2026-11-02

Task: SOURCE-CLASSIFICATION-REVIEW-01 / FSC-SEC-01..02 / DEV-017, 18.01 backlog row18. Source baseline: active 30.10.2026 controlled set. This addresses the actual independent source-review rejection on9fb074e; it is not source approval or canonical product acceptance.

The old structural renderer hid the language needed to distinguish documented examples, fixture responses and formal references. The new authoring renderer supplies bounded readable source windows and complete enclosing Python scopes where they fit. It withholds all original matched spans before cropping, recognized email/userinfo/private-key material, and newly visible encoded matches after HTML decoding. Source identities, nested archive/member locators, original byte revisions and surrounding context remain exact and are revalidated. The older structural artifact and previous reviews are preserved.

Assignment RHS AST shapes distinguish calls/references from literals without releasing their values; long scopes are explicitly incomplete. Every proposed exception is still pending. A changed source, tampered proposal, duplicate identity or unsafe rendered value fails closed. This code creates no approvals, exceptions or external trust pins.

## Verification

- First focused run:41 tests,1 failure in expected stale-source error classification. Fixed by checking exact identity before rendering changed context.
- Final focused run:42/42 PASS, `python -m unittest test_source_review_semantics test_source_review_package test_source_review_evidence test_secret_scan test_reviewed_secret_scan` from the isolated worktree scripts directory. No skips reported.
- A full regenerated164-case semantic package and its genuine fresh read-only independent review remain pending at this authoring checkpoint.
- Actual repository/client scans, full backend product suites, exact-target CI and independent product QA are separate required gates. No new passing product test or release claim is made.

Canonical8/970 SP (%0.82),2/121 tasks,0/196 screens remains unchanged. PR12 remains unmerged. Next: complete regenerated evidence, include classifier/extractor/scanner tests in the independent review, and only after complete genuine approval install context-bound receipts through the separate coordinator step and renew actual CI.
