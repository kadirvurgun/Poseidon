---
name: poseidon-ui
description: Implement POSEIDON React screens from controlled screen contracts, the prototype and design system, including component tests.
---

Read the assigned screen/field contract, 13.04 design system and relevant prototype journey. Follow [architecture](../../../docs/architecture.md). Screen count is not a database design.

Use React/TypeScript with API contract types. Show why readiness is conditional/blocked/review-required; unknown or unverified evidence cannot appear as an approved green state. Preserve distinct ship and tug contexts and server authority. Implement loading, validation, empty, error, stale/offline and conflict states where the journey requires them. Do not silently resolve safety/legal conflicts in the browser.

Write component tests for meaningful user behavior and coordinate Playwright journey selectors/data with QA. A mocked API verifies UI behavior only; report separately whether a real backend journey passed. Preserve TR/EN content and keyboard access according to the design contract.

## Learnings

After an owning mutation receipt is confirmed and the original actor/tenant session fence passes, persist its canonical resource identity in the URL before optional current-record reads. For example, receipt success followed by a GET 503 must reopen the same resource, never a fresh draft with a new UUID. Keep the receipt immutable and read-only until a current owning GET establishes editability; retain an unresolved command's original key and body until its outcome is known.
