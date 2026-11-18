# S4 offline chaos regression source pack

Status: SOURCE CROSSWALK / EXECUTION DEFERRED TO ACTUAL PR GATE / NOT ACCEPTED.

Authority: active `poseidon-v46.3-20261030`, original `18.01` backlog row 39,
DEV-038 / T-S4-006 / 8 SP, SHA256
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Literal criterion: “Network drop/reconnect, duplicate request, stale base version,
attachment retry, conflict paths otomasyonda PASS.” Dependencies: DEV-033:035.
The word PASS is the required later gate, not this source checkpoint's result.

| Required failure path | Meaningful owning regression source |
|---|---|
| Network loss, restart, reconnect and lost acknowledgement | [Signed offline cases](../../tests/Poseidon.Browser.Tests/register-specs/offline-application-cases.ts), `restartAndDuplicate` and `offline-application-restart-reconnect-response-loss-idempotent-replay`; [workspace consumer cases](../../frontend/src/product/OfflineApplicationPanel.test.tsx) also cover eventless reconnect and explicit reauthorization. |
| Duplicate delivery and exact original result | [Owned PostgreSQL/API cases](../../tests/Poseidon.Api.Tests/OfflineApplicationTests.cs), `Concurrent_duplicate_delivery_never_creates_two_domain_mutations_and_retry_recovers_original` and `Offline_real_command_and_both_receipts_audit_commit_once_and_recover_exact_original_outcome`. |
| Stale base version and explicit material conflict | The same API class's `Material_conflict_exposes_authorized_base_local_current_and_requires_new_reasoned_resolution_lineage` and `Resolution_rejects_a_server_change_after_review_and_requires_explicit_current_version_choice`; the signed `materialConflict` journey uses the actual consumer. |
| Attachment retry with uncertain result | [Attachment consumer](../../frontend/src/product/VesselFiles.test.tsx), `freezes ambiguous upload payload and key; retry reconciles without a second byte submission`, plus selected-file retention during offline/reconnect. This mocked transport verifies UI behavior only; real file-provider/scanner gate remains separate. |
| Clock drift and exclusive authorization expiry | API `Unrecognized_sensitive_fields_duplicate_json_hash_and_capture_time_are_rejected_without_writes` rejects future/old capture time and invalid offsets. `Expired_server_lease_blocks_new_replay_until_explicit_authenticated_renewal_without_losing_capture` uses a deterministic server clock and retains original capture time. |
| Retention after lower quota, bounded inventory and namespace isolation | Newly authored T-S4-006 case in [real IndexedDB driver cases](../../tests/Poseidon.Browser.Tests/specs/offline-record-store.spec.ts), described in [retained inventory correction](s4-retained-inventory-01.md). Technical HTML/storage-driver evidence is separate from genuine signed journeys. |

These references identify actual test sources, not newly executed counts. Existing
earlier S1/S2 receipts retain their original commits; they are not evidence of the
changed S4 implementation. All new execution, including full browser/provider,
PostgreSQL/API, client, contract and security coverage, remains **DEFERRED** per the
user's latest instruction. No retries, builds, typechecks or browser loops ran to
produce this crosswalk. Independent final review and actual gate closure remain
necessary before any S4 acceptance or merge.
