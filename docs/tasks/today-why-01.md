# TODAY-WHY-01 task contract

Status: authored product checkpoint — UNVERIFIED; not accepted. Tests/build/contract generation deferred by direct user instruction. Local commits only; coordinator owns any later publication.

Prerequisites read: docs/reviews/dev-oidc-lab-01-93de7b6b376f.json approves exact 93de7b6b376fb4592150c5c7b15a8c52e39bd4b3; NAV proof approves ff1e95ebf5f43ba5883f69e5e52b0cb2f205b775. Both are local engineering approvals only. Prior worker began from b67dd63. This continuation found 11 modified tracked files and 9 untracked Today files; all inherited product/test/contract work was preserved and resumed. The existing AGENTS.md cadence edit is preserved separately.

Active baseline: docs/sources/active.json, poseidon-v46.3-20261030. Bounded retrieval: py -3.13 scripts/query_sources.py with DEV023 (no match), why-engine, poseidon-today, POSEIDON Today, input freshness (limit4). Exact surrounding source chunks read through local SQLite, never entire corpus in context.

Source locators (all under analiz/POSEIDON GUNCEL 30.10.2026):
- 06_DEVELOPMENT/18.01 development backlog, sheet:Backlog/row:24 DEV-023/T-S2-004 dependencies DEV-021/022; row:25 DEV-024/T-S2-005 dependencies DEV-016/023.
- 03_REQUIREMENTS_REGULATORY/11.01 traceability, sheet:196 Ekran v46.3/row:56 (current screen inventory).
- 05_TECHNICAL_ARCHITECTURE/15.01 product API specification paragraph:327-333 Why screen/API service contract.
- 04_UX_PRODUCT_DESIGN/13.02 screen catalog paragraph:26-37 Why; paragraph:350-355 Today. 15.01 paragraph:698-704 separately confirms the Today endpoint.
- 04_UX_PRODUCT_DESIGN/13.04 design system paragraph:18-24 role/mode, exception, source/control/evidence/owner/next action and progressive disclosure.

Source decision: choose /api/v1/poseidon-today from current traceability and /api/v1/why-engine from API specification over backlog shorthand /today and /why. No duplicate aliases. UI /app/today/{vesselId}, current vessel confirmed by server scoped read, selected from existing authorized fleet. DirectoryRead grants access only to advisory metadata from the same supported regulatory draft. Not a regulatory decision engine. No operational adapter currently exists for permits, safety decisions, crew or technical tasks: typed Unavailable coverage, never sample cards.

Acceptance IDs recorded before code:
- TW-01: scoped persisted draft adapter; fixed advisory rule (absent draft or unverified draft), honest missing/unknown/declared input counts; no mandatory-field policy, critical risk or Ready state.
- TW-02: paginated stable card list, typed available/missing/unavailable coverage, query validation; safe fixed read/denial audit, fail closed.
- TW-03: Why recomputes current scoped source after authorization, fixed card identifiers only; no client reasons/paths, no unauthorized counts/details/destinations; revocation tests.
- TW-04: usable TR/EN route and navigation, trusted vessel context, accessible expandable Why with retry and focus, source observations and explicit unknown freshness, no cached green states.
- TW-05: loading/empty/error/401/403/404/offline/refresh states; abort superseded requests; existing draft navigation guard preserved.
- TW-06: domain, real PostgreSQL API, strict generated-consumer validation, React and desktop/mobile Playwright coverage; checks reported distinctly, no acceptance claim.

No new persisted entity or migration needed: projection reads existing vessel/regulatory drafts. No fake lifecycle, owner assignment, sign-off, task completion, safety or permit activation.


## Continuation delivery / deferred QA handoff — 2026-11-01

See [phase report](../phases/TODAY-WHY-01.md) for behavior, source decisions, test mapping and the deferred command inventory. The task remains a substantial supported advisory subpackage, not canonical DEV-023/024 acceptance or a complete operational Today engine. TW-DRAFT-01 is a local projection rule: stored declaration never implies verification or readiness.

Source review confirmed the locators above with Python 3.13 bounded retrieval and scoped SQLite reads. Added strict Today/Why/selector JSON checks; vessel-context response invalidation; abort/late-response handling; honest offline snapshots and reconnect reads; removal of the prior card/context after a Why authorization denial; focus on errors; and metadata-based fail-closed Today denial auditing for route casing/trailing-slash variants. No IdP, session, account, persistence schema, seed, decimal storage or command lifecycle behavior was changed.

Authored domain, actual-host/PostgreSQL API and contract, mocked React and mocked desktop/mobile Playwright regression coverage. All are DEFERRED/NOT RUN in this continuation. The previous worker/test tree was deliberately INTERRUPTED; it is not a product failure or test result. No test/build/lint/typecheck/restore/browser/lab/Python suite or host/contract generation was started. Existing OpenAPI and generated TypeScript additions are retained from the prior worker unchanged; generation provenance/parity is unverified and must be regenerated from the actual host at the combined gate. Generated artifacts were not hand-edited here.

Next: local UNVERIFIED checkpoint, then coordinator-directed source-ready regulatory draft foundation work before one combined build/contract/regression gate and independent QA of the eventual exact target commit. No push, PR, merge or self-approval.
