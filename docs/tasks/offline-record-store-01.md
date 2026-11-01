# OFFLINE-RECORD-STORE-01

AUTHORED / UNVERIFIED shared DOMAIN-COMPLETION-01 durable offline support.
Source15.01 API Integration paragraphs228–245 and security baseline;
SHA25668955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837.

Actual IndexedDB driver stores only opaque authenticated-encryption envelopes
and fixed-format command/revision/namespace metadata. It stores no key, token,
raw actor/tenant identity, declared fields or plaintext payload. Namespace is a
server-context-derived opaque SHA256, not authorization; real privacy depends
on current scope/key lifecycle and the cipher's authenticated binding. Input
buffers are copied before asynchronous writes. Native ArrayBuffer brand checks
support real browser/worker structured-clone realms.

Expected revision, per-namespace record/cipher-byte quota inventory and write
run in one readwrite transaction, serializing actual two-tab create/update and
quota races. No first/last-writer overwrite. The Promise acknowledges a write
only after transaction completion. Browser quota errors become fixed codes.
Close/versionchange disables stale handles; incompatible storage versions fail
without reset. Explicit revision-matched removal is provided for a confirmed
server receipt or deliberate authorized discard. There is no automatic expiry,
key-loss, logout, quota-pressure eviction or database deletion API.

Reduced quota blocks excess new writes but still permits paginated encrypted
record inventory/read, enabling explicit retained/recover/discard UI. Quotas
count ciphertext/tag, nonce and context digest; metadata is separately bounded
by record count. This is not an exact physical disk-usage measurement. The
actual vault must additionally enforce device-wide/all-namespace budgets,
bounded policy/session renewal, current capture permissions, key custody and
loss recovery, expiry/reauthentication, schema validation, idempotent reconnect,
atomic audited server mutation/receipt and real history/conflict journeys.
An open storage handle does not grant policy permission or offline identity.

Seven authored Playwright technical cases execute the actual source modules in
an isolated fulfilled fixture origin with real browser IndexedDB and WebCrypto:
close/reopen exact-byte persistence, wrong-key retention, two-connection CAS,
record and byte quota races, namespace separation, reduced-quota retention and
revision-matched discard, pagination after quota reduction, and version-upgrade
preservation/stale handle closure (some scenarios share a case). The fixture
uses synthetic data only; no exported product debug API or modified CSP.
These driver tests are explicitly not ordinary signed-host/SSO/application
offline evidence. DOMAIN-COMPLETION-01 must add the real composed user journeys.

No test/browser/build/typecheck/provider lab/independent QA/CI was run.
Execution is **DEFERRED / NOT RUN**, as requested. No operational entity/seed,
production encryption/key policy approval, GDPR certification or canonical SP.
