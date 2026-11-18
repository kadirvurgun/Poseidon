# S5 daily human review recording — UI source delta

Status: **AUTHORED / UNVERIFIED**. Canonical S5 remains **8 tasks /69 SP /0 accepted**. Tests, build, typecheck, lint, browser and generation are **DEFERRED until actual PR preparation**. This local source checkpoint is not independent QA or operational sign-off. No push, PR, provider, runtime or preview change.

## Source and trace

[Active baseline](../sources/active.json): `poseidon-v46.3-20261030`. Original18.01 SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`, DEV-097/T-OPS2-001 row98 (Noon/Master review), DEV-098/T-OPS2-002 row99 (Deck/officer/Master review), DEV-099/T-OPS2-003 row100 (Engine/C/E review). This is a continuation of the [ordinary daily UI package](./s5-daily-operations-experience-01.md), not additional story points or acceptance.

Controlled14.02 SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`, Data Dictionary2316 rows2146/2164/2183, and11.01 SHA256 `bc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9`, matching2316-field rows: review states, role/context visibility, immutable audit and scoped authority. Source field states are not authority to manufacture Master safety approval, submission or Chief Engineer sign-off. Existing13.04 context/form/action/focus/language standards remain controlling. Scoped local Graphify discovery preceded source reads; MCP graph tools were unavailable. Root owns the shared AST refresh after integration.

Actual backend contract was read from `DailyReviewEndpoints` and `DailyReviewPolicy`, checkpoint `59b7d44e9837301815bbceadaac85a7d9543aa24`. Default absence of a trusted exact-record human appointment remains unavailable. The backend checks actual Human membership, role, kind/vessel/record, current source pin and validity window; no administrator fallback or client-provided role can replace it.

## Delivered consumer behavior

[Strict review consumer](../../frontend/src/product/daily-review-api.ts):

- `GET /api/v1/{noon-reports|deck-daily-logs|engine-daily-logs}/{id}/review-capabilities?vesselId=…` verifies record/vessel/kind, current record revision/version, review-head version, exact sourceVersion and actual human appointment choices. Only Master for Noon; Master/ChiefOfficer for Deck; ChiefEngineer for Engine. No default selection or expanded client authority.
- `GET …/{id}/reviews?vesselId=…&offset=N` consumes25-row immutable review pages, preserving record revision, actor/role/appointment, rationale, controlled reason, sequence, source pin and integrity references. Old revision entries are labeled historical rather than current.
- `POST …/{id}/reviews?vesselId=…` sends the exact captured record/review versions, reviewed revision, source-granted authorityId, decision, paired reason and rationale with current CSRF and retained idempotency key. Client actor, source qualification and authority hashes are not mutation inputs. Confirmed receipt must match the frozen actor, role, appointment, duty, decision, rationale, reviewed revision and immutable record hash.
- Only actual per-kind REVIEWED/RETURNED metadata choices are admitted. RECORDED_UNASSESSED remains unassessed; source-owned Master/C/E DRAFT/PENDING fields are untouched. Submission/Ready/verification/legal/safety approval are not derived from recording.

[Review panel](../../frontend/src/product/DailyReviewPanel.tsx) presents “İnceleme kaydı” / “Human review recording”, actual appointment choices, displayed saved revision/time and exact immutable reference, controlled reason/rationale and readable history. Raw appointment configuration is not exposed by the API and is not fabricated. SourceVersion/role/appointment/hash references remain under details. HUMAN_REVIEW_NOT_RECORDED and REVIEW_RECORDING_IS_NOT_VERIFICATION have TR/EN labels.

Recording requires the exact displayed SAVED record revision/version to match current capability. Unsaved ordinary input, ordinary frozen/inflight writes or pending offline work disable new review recording with a save/discard hint. Forms are separate, never nested. Refresh/history do not present edited local fields as reviewed. Known source failure does not render an empty successful history.

Review drafts and unknown commands remain bound to the original tenant/actor. Fresh session identity is rechecked before reading capabilities/history and immediately before POST. Identity transitions invalidate cached projections; private rationale is hidden under another identity, offline or hidden window. A late response after the committed identity fence changes stays uncertain rather than clearing/transferring private input.

Unknown outcome freezes the original key/body/identity. A later401/403/409 cannot prove the earlier operation absent; unknown remains retained until a currently authorized matching receipt confirms it. No hidden POST retry or replacement key/body. Definite initial rejection retains local input for explicit correction/reload/discard. Current appointment withdrawal cannot be replaced by choosing a new role for an old unknown command.

[Workspace integration](../../frontend/src/product/DailyReportWorkspace.tsx) aggregates ordinary/review/offline dirty and saving channels through separate synchronous refs. A panel completing cannot clear another form's unsaved inputs or unknown outcome. beforeunload reads those same live refs, including changes before a React render commits. Same-revision metadata can refresh without changing local fields/base version; dirty fields preserve the original optimistic concurrency base. Root's Chrome/browser-Back source scenarios remain separate and unexecuted.

[Audit command vocabulary](../../frontend/src/product/audit-command-vocabulary.json) adds the actual six ordinary DailyCreate/Update operations as PREPARATION_COMMAND and three DailyReviewRecord operations as HUMAN_REVIEW_WORKFLOW. The separate offline author owns its seven DailyOffline operations; no public17-event audit taxonomy expansion or GDPR-completeness claim is made.

## Meaningful test sources — execution deferred

- [daily-review-api.test.ts](../../frontend/src/product/daily-review-api.test.ts): default unavailable authority, exact duty/choice/source scope, no admin/approval expansion, paired reasons, exact versions/body, no client actor injection, immutable history and receipt lineage validation.
- [DailyReviewPanel.test.tsx](../../frontend/src/product/DailyReviewPanel.test.tsx): explicit appointed recording, no automatic write, unknown503→403→receipt recovery, exact key/body, cross-identity private rationale withdrawal/resumption, late identity-crossing response, historical revision labels, ordinary-form lock and offline/unavailable history.
- [DailyReportWorkspace.test.tsx](../../frontend/src/product/DailyReportWorkspace.test.tsx): existing readonly/history regression now targets actual unconfigured review capability instead of a placeholder Submit button; original no-write/dirty/history/unknown assertions remain.

No suite or per-module check was run. Whitespace review alone is not runtime evidence. Backend author independently authors actual signed-host journey SOURCES; mocked transport tests prove no external IdP, appointment, signature or real human approval.

## Follow-up and open gate

Dedicated secure daily offline panel wiring follows as a separate normal checkpoint after merging the isolated adapter. New draft restart must retain only a noncredential strict resource UUID in the URL; no grants, decrypted fields, credentials or ownership are inferred from it. Live source/permission/actor/appointment revocation and actual browser/history/native behavior remain to be validated at the PR gate. Source lookup/date/unit and operational authority gaps remain open. Canonical S5 status and phase acceptance are owned by root and remain0/69 accepted.
