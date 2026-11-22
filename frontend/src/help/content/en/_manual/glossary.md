---
key: glossary
title: Glossary of states
reviewed: false
---
## Readiness and review states
- **READY:** every required input was read and the rule found no blocker. It is the result of a rule, not a safety decision; the authorized person still decides.
- **CONDITIONAL:** the result depends on a condition that is shown in the Why view.
- **NOT_READY:** at least one blocker exists. Open Why to see which one.
- **REVIEW_REQUIRED:** the system cannot decide, for example because a critical input is unknown. An authorized person must look at it.
- **UNKNOWN:** the value is missing from the source or could not be determined. Unknown is never the same as "No", "Ready" or "Not applicable". When a critical value is unknown, the result stays REVIEW_REQUIRED.

## Record lifecycle
- **Draft:** saved but not submitted. A draft may be incomplete; submission is blocked while required data is missing.
- **Completed:** the person doing the work has finished it.
- **Verified:** an authorized verifier, who is a different person from the one who recorded the work, has confirmed it.
- **Effective:** a later check has shown that the action really worked. Completed ≠ Verified ≠ Effective.
- **Superseded:** a newer record replaces this one; the original is kept unchanged and linked.
- **Separation of duties:** some steps must be taken by a different person from the one who recorded or verified an earlier step. The server refuses the step otherwise.

## Sources and data freshness
- **Available (unverified):** the source was read; its content has not been verified.
- **Unavailable in this context:** the source could not be read for this company, vessel or role. An alert you cannot see does not mean everything is fine.
- **Partial view:** some sources could not be read. A count of zero does not mean "no problem".
- **Stale:** the data shown is an older copy. Refresh to read it again.
- **Read time / saved time:** when the data was read or saved. It does not prove that the source is correct.

## Offline work
- **Offline:** the connection is down. Data on screen is the last copy.
- **Queued:** a change waits in the durable queue and is sent when the connection returns.
- **Conflict:** the record changed on the server while you were offline. It is never merged automatically; resolve it in the Material conflict center.
