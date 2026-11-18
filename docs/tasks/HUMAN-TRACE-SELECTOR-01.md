# HUMAN-TRACE-SELECTOR-01 — unambiguous unavailable-evaluation observation

Status: **AUTHORED / execution DEFERRED / acceptance false**. No product behavior,
server permission, fixture appointment or canonical acceptance changes.

Source: active `poseidon-v46.3-20261030`,
[HUMAN-DECISION-TRACE-01](human-decision-trace-01.md), 15.01 paragraph99,
18.01 Backlog32 and 13.04 paragraphs19–25. Missing authoritative evaluation
must remain visible and cannot imply human verification or operational approval.

The `faf7f91` genuine diagnostic recorded a `toBeVisible` failure at
[human-review-cases.ts](../../tests/Poseidon.Browser.Tests/register-specs/human-review-cases.ts):35.
The closed telemetry did not retain the exception text. Source inspection
independently proves that the broad exact-text selector is ambiguous whenever
the evaluation is absent: the server supplies the reason as both a chain
blocker and an Applicability segment; the UI displays both explanations.

Both pre-offline and post-reconnect assertions now require exactly one visible
complete blocker list item. The lineage explanation remains present. No `.first()`
fallback, alternate state, hidden reason, skipped case, timeout extension or
weakened API/keyboard/offline/decision assertion is introduced.

The new [component regression](../../frontend/src/product/HumanDecisionTrace.test.tsx)
requires both legitimate text matches, the unique complete blocker and the
Applicability lineage explanation. It uses mocked transport and cannot prove
the genuine signed-host journey. Test/build/browser execution remains deferred
to the next larger frozen bundle gate and independent target review.
