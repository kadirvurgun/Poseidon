# TECHNICAL-COMMAND-OWNER-RECOVERY-01

State: AUTHORING, acceptance false; new technical package, separate from localization gate.
Baseline: poseidon-v46.3-20261030. Parent source: 23b87d47de56ed5015032dd6a21a00ee92a1a1e6.
Branch: foundation/technical-owner-recovery-20261111.
Worktree: .runtime/worktrees/technical-owner-recovery-20261111 (relative to main checkout).
Owner: UI author; coordinator and independent QA separate.

## Controlled authority and dependency boundary

18.01 Development Backlog Backlog!A53:Q54 (DEV-052/T-S8-001 and DEV-053/T-S8-002); cross-cutting accepted DEV-014/015/019 Backlog rows15/16/20. API15.01 paragraphs67/71/73; security baseline; existing equipment-maintenance-01 command recovery requirements. Sources are active20261030, not prior archives. This is an existing engineering prerequisite, not acceptance of full DEV-020/052/053/055/056 or PMS Command Center.

## Acceptance cases

1. Bind equipment/plan/job drafts and retained uncertain attempts to original authenticated actor+tenant, vessel and resource. Detected replacement identity sees no former private fields and cannot adopt/edit/retry them.
2. Fresh actual session and current scoped access precede mutation; same-owner refreshed CSRF allowed. Recheck identity after async reads/writes and fence obsolete/unmounted scope completions. Frontend is not server authorization or atomic session-race prevention.
3. Prior unknown outcome survives later401/403/revalidation errors. Explicit retry by reauthorized original owner uses exact original key/body/UUID/base. No silent resend or rebasing.
4. Confirmed original-owner identity is persisted in URL before optional readback. Failed readback keeps that canonical identity and read-only outcome; no fresh UUID.
5. Job creation retains actual UUID/body replay protocol; no invented Idempotency-Key/CommandReceipt contract. Keep dirty/navigation protections.
6. Use shared enterprise controls and existing17-language messages; no English fallback or new plaintext storage. No new backend authority, catalog or approval.

## Owning files and checks

Author owns TechnicalWorkspace.tsx, TechnicalWorkspace.test.tsx and a small technical ownership helper if needed. Coordinator separately owns API/backend/browser tests and project status. Read WorkOrderWorkspace/DowntimeWorkspace recovery patterns and real technical API contracts before implementation.

Author meaningful controlled identity/tenant/CSRF changes, uncertain retry, failed readback and stale completion tests alongside code. All NEW package test/build/lint/typecheck/browser execution DEFERRED until larger technical bundle gate. Later acceptance needs actual UI/backend/PG/browser, independent final-target QA and required CI. Do not push or merge now.