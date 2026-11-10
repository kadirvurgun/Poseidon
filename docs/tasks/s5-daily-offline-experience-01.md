# S5 daily secure offline panel — workspace integration source

Status: **AUTHORED / UNVERIFIED**. Canonical S5 stays **8 tasks /69 SP /0 accepted**. This is source authoring; tests/build/typecheck/lint/browser/contract generation remain **DEFERRED until actual PR preparation**. No runtime, preview, provider, push or PR change. Whitespace review is not runtime acceptance.

## Source and ownership

Continuation of [ordinary daily UI](./s5-daily-operations-experience-01.md), [human review recording](./s5-daily-review-experience-01.md), and the separate [dedicated adapter](./S5-DAILY-OFFLINE-01.md). Active baseline/source pins remain unchanged:18.01 SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`, DEV-097/T-OPS2-001 row98, DEV-098/T-OPS2-002 row99, DEV-099/T-OPS2-003 row100; prerequisite DEV-033 durable queue/UUID/restart, with original14.02 daily fields rows2128–2185. No additional story points or acceptance.

The isolated offline backend/client/panel is owned by the offline author. Its primary checkpoint `4cecf78` was merged normally after the review UI checkpoint; one additive audit vocabulary conflict was resolved by preserving all nine ordinary/review and all seven offline operations. No adapter internals, grant classifications, provider configuration or source schema were invented by this integration.

## Delivered integration

[Workspace](../../frontend/src/product/DailyReportWorkspace.tsx) mounts the actual `DailyOfflinePanel` outside the current online/capability projection conditional. Its key is the retained editor's exact owner/kind/vessel/resource UUID. It stays mounted through offline/hidden/source refresh so encrypted state is not lost because a parent loading branch disappears.

The panel's `enabled` flag is only an interface lock. Offline capture still requires its own server-issued exact actor/tenant/vessel/kind/resource classification, proof, key and bounded lease. A mere offline event preserves only the last confirmed same-owner write eligibility for an already preauthorized handle; known denial, identity mismatch, hidden window, ordinary unknown/conflict/inflight write and pending review lock it. No crew/tug grant is reused, and no plaintext fallback or automatic authorization/write is introduced.

Ordinary/review/offline dirty and saving channels remain separate. Completion of an offline action cannot clear an unrelated daily edit or unknown human command. The offline commit callback only refreshes projections; it retains dirty ordinary fields and their ORIGINAL concurrency base0/N. It does not combine old local input with a new base version. An explicit, guarded “Read current saved draft” route allows reload/resolution; it is offered for a resolved retained command without claiming that a keep-server outcome modified the daily report.

[Strict routing](../../frontend/src/product/daily-location.ts) adds only `?draft=UUID` for a new resource without an existing saved row. It rejects duplicate draft, id+draft, draft without selected vessel, invalid IDs, unknown parameters and paginated draft context. The URL contains no credential, actor ownership, plaintext field, grant, key or policy. Restoring it initializes an EMPTY ordinary editor only after current authenticated session/capability admission; the panel must explicitly inventory/renew its own current context before decrypting eligible retained ciphertext. No saved-record GET is issued for an absent new draft.

New draft replaces the current vessel collection history entry, retaining its native history index. Browser Back therefore still targets the preceding vessel selection; Keep restores the exact protected draft URL. Cancel clears draft context through the existing guard. A confirmed ordinary save canonicalizes to the saved `?id=UUID` URL without creating a second hidden new resource or losing the success message. The later draft UUID is not an authorization input.

The [numeric field hint](../../frontend/src/product/DailyReportFields.tsx) explicitly states the `.` decimal separator in TR/EN. Numeric values remain lexical strings; unresolved units/ranges are not invented. Human review select validation also references its accessible error summary.

## Authored regression sources — not executed

- [DailyOfflineWiring.test.tsx](../../frontend/src/product/DailyOfflineWiring.test.tsx): a deliberately mocked panel tests parent lifetime/resource identity across offline, current identity lock, aggregate guard behavior, unrelated ordinary precision/base-version retention after offline callback, empty-only URL restart, no absent saved-record read/no automatic command and current create-permission denial. These orchestration tests do not prove cryptography, IndexedDB persistence, human authority or actual server behavior.
- [daily-operations-api.test.ts](../../frontend/src/product/daily-operations-api.test.ts): draft route roundtrip and malformed/duplicate/id+draft/no-vessel/paged draft rejection added to the existing exact source contracts.
- [DailyReportWorkspace.test.tsx](../../frontend/src/product/DailyReportWorkspace.test.tsx): synthetic confirmed writes now persist transport rows across canonical saved-record navigation; original dirty/value/history/exact retry/privacy assertions remain. The default offline boundary targets the real mounted classified panel, not a placeholder button.

The separate adapter owner authors actual crypto/IndexedDB/loss/conflict and signed host scenario SOURCES. Root owns ordinary Chrome/back navigation browser sources and later whole-bundle independent QA. No test result, count, visual success, supported production offline classification or acceptance is claimed here.

## Open acceptance

The operator must provision genuine classification/purpose/storage/key/retention and current authority before offline operation. Hidden/disabled/reconnect withdrawal must preserve truthful pending/loss guards; the adapter author's latest retained-memory/ServerRetained follow-up is a coordinated dependency, not a validation result. Never-captured classified edits are not described as encrypted recovery. Cross-context actor/role/file/policy revocation, exact replay/material conflict, key loss and restart need actual PR-gate execution. Current operational, Master/C/E approval, external pilot evidence and unresolved source/date/unit/lookup decisions remain separate and open. Root owns final status/phase report; canonical S5 remains0/69 accepted.
