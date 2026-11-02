# OFFLINE-CONFLICT-REVIEW-UI-01

AUTHORED / UNVERIFIED shared DOMAIN-COMPLETION-01 offline reconciliation support.
Active15.01 Product_Wide_API_Integration_Specification_TR.docx paragraphs228–245,
especially244–245 local/server/resolved values and decision actor/reason;
SHA25668955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837.

TR/EN typed presentation shows authorized captured/server values and a separately
chosen resolved value. There is no default radio selection. Each field requires
an explicit policy-permitted keep-server/use-captured choice plus a nonblank
reason. Unknown/missing/withheld/known-empty values are distinct, never boolean
No by default. Withheld or unsupported fields cannot be resolved by stale UI
capability flags. Text is inert, responsive and keyboard labeled. Field choices
select the exact conflict snapshot values; they do not permit arbitrary raw
client values or imply signatures, safety release or a current READY result.

Actor/tenant and domain values are not supplied in the resolution request. The
workspace sends conflict ID, expected revision, explicit choices and reason to
the actual trusted server handler. It must keep a stable idempotency UUID for
each semantic resolution attempt. An uncertain outcome locks editing/resubmit
and exposes a receipt lookup action; a known rejection retains the draft. Changed
conflict revision resets choices/reason. Actor/session/tenant/vessel/device/policy
context mismatch, denied access and loading/stale/error states hide old data.
The owning workspace must bind those keys, cancel obsolete requests, clear private
parent state and fetch fresh scoped authorized conflict DTOs. Keying alone is not
a server authorization boundary or privacy-retention policy.

Only current authenticated online mutation is offered. Reason input length comes
from configured server transport policy, not an invented canonical business limit.
No browser persistence or private draft telemetry is introduced here. Manual
arbitrary-value/controlled-lookup resolution, when required by the resolved domain
contract, remains with the actual domain-specific editor and current server field
policy; this two-choice supporting projection cannot silently narrow that contract.

DOMAIN-COMPLETION-01 must connect real conflict endpoints/receipt recovery and
history, authorized actor attribution, revision/base-version/domain validation,
atomic audited SaveChanges plus receipt and actual durable offline cache/queue.
Write actual signed-host browser and PostgreSQL/API races/privacy/revocation cases.
The component tests are authored only: **DEFERRED / NOT RUN**, as are build,
typecheck, browser, independent QA and CI. This is no complete offline system,
human decision evidence, production policy approval, accepted task or canonical SP.


## Application consumer — AUTHORED_UNVERIFIED

The former handoff is now composed in the actual Crew workspace by OFFLINE-APPLICATION-01: explicitly configured Draft watch_group, enrolled device/encryption/signing keys, persistent encrypted snapshot/queue, ordinary-handler atomic replay/receipts/audit/conflict, explicit lifecycle/recovery/history/cleanup and registered real signed-host scenarios. Primitive coverage remains retained. All execution is DEFERRED. Other fields/binary evidence remain denied, and production policy is not approved. [Exact source and boundaries](offline-application-01.md).
