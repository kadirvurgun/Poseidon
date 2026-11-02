# OFFLINE-REPLAY-POLICY-01

AUTHORED / UNVERIFIED support for DOMAIN-COMPLETION-01 shared offline READ/CAPTURE
and reconciliation, including DEV-047/T-S7-001. Active15.01 Product_Wide_API_
Integration_Specification_TR.docx paragraphs228–245,
SHA25668955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837.
Source requires UUID idempotency, last-known material aggregate version, explicit
material conflict, server receipt clock and later evidence verification. No source
field whitelist, automatic operational transition or production catalog is invented.

The pure .NET preflight compares captured actor/tenant/vessel/device/aggregate
binding with independently resolved current server context. Read/device access
and binding are checked before receipt recovery. A matching scoped historical
receipt can be recovered under current read access after write revocation or
policy expiry, without executing another write or asserting current READY. Receipt
mutation/operation/base-version/hash mismatch is an explicit command-reuse conflict.

For new commands, current write permission and configured versioned operation/field
policy are required. Policy validity uses explicit server receipt time, never client
capture time; exact expiry, policy change and not-yet-active policy are distinct.
Unknown fields are denied with ordinal matching; material commands need a base
version and stale versions require conflict resolution. Nonmaterial naming alone
does not permit stale merge; explicit approved additive-merge policy and actual
domain validation are still needed. A policy marking material data auto-mergeable
fails closed; authority-producing offline operations are refused.

The result is only EligibleForTransactionalRevalidation. API must authenticate and
scope receipt DB queries before calling, build server policy/binding/current versions
from trusted state, recompute SHA256 from a canonical full semantic command envelope
and payload (never trust a supplied hash), and reauthorize/revalidate actual domain
state in the transaction. Atomically persist mutation+unique scoped receipt through
attributed audited SaveChanges. Preserve local/server/resolved values and authorized
actor/reason in real conflict workflow. Receipt recovery must filter historical
response fields by current privacy access and present historic outcomes separately
from current status. No unscoped receipt/existence queries or body-created actor.

Operation handlers must be an actual server allowlist of supported source commands;
the generic policy flags cannot create a handler or legal permission. Default deny
unconfigured/sensitive fields, no enabled offline Tug dispatch or safety signature.
The explicit synthetic policy in tests is test input, not approved deployment policy.
This policy creates no operational entity or seed.

Actual bounded encrypted durable cache/queue, device/session key lifecycle,
HTTP endpoints and atomic receipt persistence, ordinary domain mutations, scoped
conflict/history/reconnect UI, logout/expiry/revocation handling and signed-host
browser/PostgreSQL/API tests remain in DOMAIN-COMPLETION-01. This helper is not a
complete offline system, proof of DB race safety, immediate disconnected revocation
or XSS immunity. Unit cases are authored; execution/build/typecheck/browser/QA/CI
are **DEFERRED / NOT RUN**; canonical SP unchanged.


## Application consumer — AUTHORED_UNVERIFIED

The former handoff is now composed in the actual Crew workspace by OFFLINE-APPLICATION-01: explicitly configured Draft watch_group, enrolled device/encryption/signing keys, persistent encrypted snapshot/queue, ordinary-handler atomic replay/receipts/audit/conflict, explicit lifecycle/recovery/history/cleanup and registered real signed-host scenarios. Primitive coverage remains retained. All execution is DEFERRED. Other fields/binary evidence remain denied, and production policy is not approved. [Exact source and boundaries](offline-application-01.md).
