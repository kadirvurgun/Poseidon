# OFFLINE-CIPHER-01

AUTHORED / UNVERIFIED shared DOMAIN-COMPLETION-01 encrypted offline capture support.
Active15.01 API Integration paragraphs228–245 and project security baseline apply;
source SHA25668955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837.
No deployed client storage/key lifecycle or privacy policy is approved by this code.

The WebCrypto primitive encrypts bounded bytes with a nonextractable AES-256-GCM
key, a new random96-bit nonce and128-bit authentication tag. Ordered authenticated
additional data binds tenant, actor, vessel, device, noncredential session context,
policy version and command ID under an explicit format/purpose version. Only a
SHA256 context fingerprint, nonce and ciphertext are returned; no raw context,
payload or authentication token is included in the stored envelope projection.
Changed binding, ciphertext, nonce or substituted context hash cannot decrypt.
Configured byte/context quotas are mandatory; data is never truncated or normalized.
Errors contain fixed codes, no context or plaintext. The primitive never deletes data.

The caller must validate supported schema/fields/command, authorize actual current
policy, supply enrolled device key, manage invocation limits/rotation, atomically
persist within bounded IndexedDB quota, and retain/recover pending commands on
expiry/key loss/policy or session renewal. A changed limit can block reading an old
record; that must be an explicit retained/renew/recover path, never silent deletion.
sessionBinding is an opaque noncredential context reference, NOT a session cookie,
access/refresh/ID token or offline authentication. The owning lifecycle must support
explicit authorized recovery of old pending context without cross-identity access.

Nonextractable browser keys do not provide XSS immunity or prove protection against
a compromised browser/OS. This helper does not select device key custody, establish
immediate offline revocation/time authority, enroll a device, choose browser storage,
claim GDPR compliance, synchronize records or authorize operational decisions.
DOMAIN-COMPLETION-01 must implement the actual durable vault/queue, logout/restart/
key-loss/expiry/device/policy lifecycle, current trusted server reconciliation and
real signed-host browser evidence. Fields are opaque bytes only after source-schema
validation, not a permissive arbitrary JSON mutation contract.

Unit scenarios use real Node built-in WebCrypto (no provider API or mocked crypto):
exact Unicode/decimal-preserving roundtrip, nonce uniqueness, seven changed scope
bindings, delimiter ambiguity, tampering, wrong/substituted keys/context, extractable
and smaller keys, byte/context quotas, safe errors and retained encrypted records.
They are **AUTHORED ONLY / NOT RUN**. No suites/build/typecheck/IndexedDB/browser/QA/
CI executed. No seed entity is introduced and canonical progress is unchanged.


## Application consumer — AUTHORED_UNVERIFIED

The former handoff is now composed in the actual Crew workspace by OFFLINE-APPLICATION-01: explicitly configured Draft watch_group, enrolled device/encryption/signing keys, persistent encrypted snapshot/queue, ordinary-handler atomic replay/receipts/audit/conflict, explicit lifecycle/recovery/history/cleanup and registered real signed-host scenarios. Primitive coverage remains retained. All execution is DEFERRED. Other fields/binary evidence remain denied, and production policy is not approved. [Exact source and boundaries](offline-application-01.md).
