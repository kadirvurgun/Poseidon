---
name: poseidon-qa
description: Independently validate POSEIDON tasks and phase gates with backend integration, UI behavior, Playwright, offline and authority regression evidence.
---

Derive acceptance cases from canonical requirements, not just the implementation. Read 19.01 test/release gates and the task's traceability IDs. Follow the test matrix in [architecture](../../../docs/architecture.md).

For affected flows verify normal and denied transitions, tenant/vessel/job boundaries, UNKNOWN→REVIEW_REQUIRED, human release authority and immutable evidence lineage. Cover offline duplicate command, reconnect and material conflict when relevant. Run real-backend Ship/Tug Playwright journeys at the required gate. Keep developer unit/component tests and QA acceptance results distinguishable.

Attach commit ID, commands, exit status, failed cases and Playwright trace/report paths. Never convert failure into success by removing assertions, skipping tests or lowering thresholds. Phase acceptance requires all required checks and explicit unresolved blockers; pilot/SME signatures cannot be fabricated by an agent.
