# S4 retained offline inventory

Status: AUTHORED / TEST EXECUTION DEFERRED UNTIL ACTUAL PR GATE / 0 ACCEPTED.

Source: active `poseidon-v46.3-20261030`, original `18.01` development backlog
SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`,
Backlog row 34, DEV-033 / T-S4-001 (durable encrypted queue, restart retention and
visible pending state), and row 39, DEV-038 / T-S4-006 (offline chaos regression).
Dependencies remain DEV-021/029 and DEV-033:035; authoring does not accept them.

The IndexedDB store already preserved records when quotas shrank, but the crew
controller read only one page sized by the newly reduced quota. Retained captures
beyond that page became invisible to the queue and exact replay lookup.

[Storage](../../frontend/src/product/offline-record-store.ts) now offers a bounded
retained inventory that traverses all namespace-local pages. The
[controller](../../frontend/src/product/offline-application.ts) uses its existing
128-record protocol ceiling. Overflow fails explicitly instead of returning a
partial list. Read access still requires the original current authorization and
authenticated decryption. Quotas on writes, keys, ciphertext, namespaces, exact
revisions, explicit discard and no automatic deletion remain unchanged.

One authored real IndexedDB browser regression lowers the quota after reopening
five stored records, observes all five in order, checks the overflow boundary,
preserved write rejection and foreign namespace isolation. The fulfilled technical
HTML harness is storage-driver evidence only; it is not a signed product journey.
An additional real IndexedDB case closes storage after the first inventory page:
the read must fail rather than return a partial queue, and reopening must retain
every original record. No test, browser, build, typecheck, lint or generation was executed. Actual results
and independent acceptance remain deferred to the larger PR gate.
