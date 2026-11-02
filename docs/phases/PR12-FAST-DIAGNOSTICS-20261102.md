# PR12 Fast routing and bounded API diagnostics — 2026-11-02

Source: user request to enable Fast and fix PR12; OPS-MODEL-01; PR12 source checkpoint `9fb074e`.

Automation previously forced `service_tier="default"` despite the local Codex preference being priority. The explicit policy now selects Fast, verifies its advertisement for the selected account/model, and records the requested tier in each dispatch receipt. Existing model roles and security reasoning effort remain unchanged. Unsupported or unknown tiers fail closed; actual server speed is not inferred from a requested preference.

API timeout receipts now retain elapsed time, discovery/execution stage, partial numeric summaries, an output digest and at most eight static identifiers already seen in discovery. These identify diagnostic occurrences only: they do not prove which case was running or completed. Detailed output remains in memory. Deadlines remain nonpassing even when partial output contains a perfect test summary. Filters, owned-provider requirements and the 1800-second deadline remain unchanged.

## Verification

- Python focused CI/helper suites: **22/22 PASS**, `python -m unittest test_api_test_gate test_model_policy`, root projected source and again on the isolated publication worktree (22/22 each). A separate root-directory invocation found neither test module (2 loader errors, zero test cases); corrected working-directory invocation passed. These are helper tests, not the backend/API product suite.
- Installed .NET test runner help: `--output <Detailed|Normal>` confirmed; no product test executed by help.
- One actual fresh read-only Esra native security review completed on `9fb074e`, GPT-6.1 Sol/medium, requested Fast. Actual usage: 247943 input tokens, 1343 output tokens; approximately60 seconds in the native model call. Neither the root chat billing profile nor the server's applied speed is inferred from this.
- Source review: **1 approved / 163 blocked**, no classification gate released, no exception installed, no external trust pin changed. [Actual independent review](../security/pr12-source-classification-review-20261102.json).
- Main missing evidence: structural rendering hides semantic identifiers, literal roles and document text; missing classifier/context-extractor and scanner regression inputs. Author provenance alone is insufficient. Next source package must preserve safe semantic context and include the actual classifier/scanner tests.
- Earlier CI on `9fb074e`: frontend/browser, Windows automation and installer passed;48 default browser cases passed. Persistence/security stopped at repository scan1417 inputs/164 findings before backend product tests. Those results do not validate a later commit.

PR12 remains unmerged. Full exact-target CI and independent product QA remain required. Canonical acceptance remains8/970 story points (%0.82); no sprint/release gate is completed by this package.
