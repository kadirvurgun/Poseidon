---
name: poseidon-coordinate
description: Plan and coordinate POSEIDON UI, backend and independent QA work from controlled requirements with task dependencies and phase gates.
---

Use [automation decisions](../../../docs/automation.md) and [architecture](../../../docs/architecture.md). Start from the controlled register, traceability matrix and development backlog. Research/archive documents are not requirement authorities. Resolve source conflicts through a recorded decision rather than inventing requirements.

Give each task a stable ID, source version/locator, acceptance cases, dependencies, owning role, target branch/worktree and exact required checks. Select only dependency-ready tasks. Keep each worker's branch/worktree separate and assign QA independently of the author. Require backend unit/integration and UI component coverage alongside implementation; run push/PR checks and the larger phase-end suite.

Persist leases and progress before dispatch. On restart reconcile remote commits/PRs/CI before repeating side effects. Enforce the configured usage and retry limits; if no operational limit or host is configured, record that setup is incomplete. Rate limits pause eligible dispatch rather than rotate accounts to bypass them. Merge only after acceptance and required checks pass on the target commit. Real pilot and SME approvals remain external evidence.
