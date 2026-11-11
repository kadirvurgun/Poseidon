# MAINTENANCE-OBSERVATION-OWNER-RECOVERY-01

State: AUTHORING; acceptance=false. New technical bundle, all execution DEFERRED.
Baseline: poseidon-v46.3-20261030. Starting checkpoint:8955d5c.
Coordinator/UI owner: root, foundation/enterprise-localization-20261109 in s8-pms-command-center.
API-test author: separate observation-recovery-tests-20261112 branch/worktree.
Independent source review: separate reviewer after authoring; no execution acceptance.

## Source and dependency boundary

DEV-053/T-S8-002,18.01 Backlog row54; existing equipment-maintenance-01 and DOMAIN-COMPLETION-01 observation integration. Current11.01 field contracts meter_source/last_done_meter/current reading, FR46-maintenance-plan-meter_source / TC46-maintenance-plan-meter_source; traceability row1413 and dictionary1411–1415.15.01 paragraphs67/71/73 tenant authority/idempotent retries/version;13.04 paragraphs10–24 enterprise explicit blockers and authority. DEV-014/015/019 accepted cross-cutting protections. Existing APIs authorize, persist/audit immutable observation UUID/body and separate revocation rows. No new policy, approved source catalog or safety sign-off.

The original TOCR-OBS-EXISTING-DENIAL issue is open at MaintenanceObservations401/403 handling. Frontend owner binding is not a server actor-scoped receipt contract: capture uses observation UUID/body equality, revocation uses observation UUID/current authorization. Do not invent generic CommandReceipts or actor-specific replay semantics.

## Acceptance cases

1. Bind draft and frozen capture/revoke to original tenant+actor and vessel/asset. Replacement identity, invalid identity, withdrawn/offline/hidden view exposes no retained private DOM and cannot issue its command.
2. Refresh real session before/after current owning observation access and mutation. Use fresh same-owner CSRF; CSRF, callback identity, source reload and focus do not reset dirty fields or unknown command.
3. Preserve prior unknown across401/403/404/CSRF failure/current policy or permission denial and interrupted reads. Only explicit original-owner retry sends exact same path/key/body/UUID. Access refresh never resends.
4. Validate actual capture/revoke response target and shape. Keep confirmed outcome read-only through optional failed readback; no recapture/revoke or new UUID. Ignore stale scoped and unmounted completions.
5. Block parent navigation/discard while a write is running or unknown. Explicit access/source retry remains available. Definitively rejected fresh command may retain editable draft after current access is re-established; unknown commands never unlock/discard silently.
6. Existing17-language source messages, shared Button/EnterpriseActions, no plaintext persistence, no manufactured approved/fresh policy. Scope or owner props changing never adopt retained fields.

## Ownership and verification

Root owns MaintenanceObservations.tsx/test, small observation API adapter/test and TechnicalWorkspace integration/test. API-test author owns only new MaintenanceObservationRecoveryTests.cs, no backend changes presumed. Author actual-host API/PG regressions for permission/policy denial then original-byte reauthorization replay, fresh CSRF, foreign tenant/asset denial, idempotent revocation and single business audit. Existing genuine-host sources may be extended only with real configured fixture support; unavailable source fixture remains explicitly open.

All new test/build/typecheck/lint/browser execution DEFERRED until larger technical bundle gate. Static source review and Git/AST metadata are not PASS. Later independent final-target QA, actual PostgreSQL/UI/browser and required CI precede publication/merge. Canonical tasks/SP/screens unchanged.
