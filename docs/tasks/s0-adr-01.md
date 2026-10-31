# S0-ADR-01

Source: 18.01 Backlog row5 DEV-004 / T-S0-004; docs/architecture.md

Prepare the engineering ADR set required by DEV-004. Read docs/architecture.md, security-baseline.md and the current source pointer. Use scripts/query_sources.py for small targeted extracts of 14.01, 15.01 and 23.03; do not load the corpus. Write docs/decisions/s0-module-boundaries.md, s0-events-storage.md, s0-offline-ai.md. Include exact source file/section, alternatives, selected engineering direction, consequences, implementation evidence and open approval fields. Distinguish implementation from proposals. Domain -> application -> infrastructure/API dependency directions must be unambiguous; no cross-module DbContext leaking into domain. Transactional outbox is proposed until implemented. Offline replay must not invent active permit authority. AI is advisory, tenant-authorized retrieval and evidence citations required. Leave expert/legal/operational sign-offs open. Add acceptance evidence to this task document without claiming DEV-004 accepted. Do not modify project-status or phase files. Documentation delivery only; all repository checks remain required.

Acceptance:
- Module boundaries, dependency direction, outbox, storage, offline and AI decisions are explicit with source locators
- Unresolved D-STATE-01, IdP, data location and expert decisions stay open; no invented approvals
- Relative document links resolve and canonical accepted SP unchanged
