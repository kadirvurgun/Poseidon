---
key: pilot-gate
title: Pilot go/no-go review
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## What this page is for
The Pilot go/no-go review page shows the state of the gates required for a pilot, the open P0 count and support readiness in one place, and records the appointed Product Owner's go/no-go decision. It answers: "Has every pilot gate been met with evidence, and has a human decision been recorded?" The notice at the top applies: "Advisory review only. Unknown or deferred gates and an unknown or open P0 recommend NO-GO. Only the appointed Product Owner records a decision."

## Who uses it
- **Product Owner:** reads the review result and records the go/no-go decision with a rationale. Deciding needs a current Product Owner appointment.
- **Support / Tenant Admin:** marks support and on-call readiness items with an evidence reference.
- 13.03 defines no separate role for this page; the tenant administration principles (least privilege, controlled publication) apply (13.03 §9).

## Before you start
- Viewing needs tenant-wide read and pilot gate read. Marking a support item needs support record permission; deciding needs decide permission plus a current Product Owner appointment provisioned on the server. The permission alone is not enough.
- Gate inputs come from other pages; for example the Ship UAT and Tug UAT gates are computed from attested runs on the **Role UAT records** page.
- Recording needs a live connection; it is disabled offline.

## Step by step
1. In **Pilot gate review** read the recommendation (**Advisory recommendation**), the **Open P0** count and the gate table: **Gate**, **State**, **Source**, **Reasons**.
2. In **Support and on-call readiness** choose a **State** for each item, enter an **Evidence reference** for **Ready**, write a **Note** and press **Mark**.
3. If you are the Product Owner, choose the decision in **Record go/no-go decision** (**NO-GO**, **Conditional GO**, **GO**), choose your Product Owner appointment in **Appointment** and write the **Rationale**. For **Conditional GO** fill in **Conditions (one per line)**.
4. Press **Record**. The decision appears in **Product Owner decisions** with its sequence number.
5. Press **Refresh** to reload the data.

## What the states mean
- **Evidence threshold met (PASS):** the gate's source meets the evidence threshold. It is not an approval; a human decides.
- **Fail (FAIL)**, **Unknown (UNKNOWN)**, **Deferred (DEFERRED):** the gate is not met. An unknown or deferred gate never counts as passed and recommends NO-GO.
- **NO-GO recommended:** at least one gate is not PASS, or open P0 is unknown or above zero.
- **GO eligible; human decision pending:** every gate is PASS and open P0 is zero. The system never decides GO; only the Product Owner records it.
- **Not marked / Ready / Not ready:** the latest marked state of a support item.
- **Current / Stale: review changed:** a decision is bound to the review at the time it was made. If the review changed later, the decision shows as stale and needs to be reconsidered.

## What you cannot do / caution
- "GO is possible only when every gate is PASS and open P0 is zero." Otherwise the GO option is disabled.
- Conditional GO cannot be recorded while a gate is failed or unknown, or while P0 is open.
- The system, AI or automation never makes the pilot decision and never fabricates gates or sign-offs. Pilot gates and decisions are human records.
- You cannot change a recorded decision; a new decision is added with a new sequence number.

## If something goes wrong
- **You hold no current Product Owner appointment. Decisions are read-only.** Appointments are provisioned on the server; contact the Tenant Admin (13.03 §10).
- **You do not have access to these records.** Check your role and tenant scope.
- **The record changed or was already used. Reload.** The review changed while you were deciding; open the current review with **Reload**.
- **The outcome is uncertain. Retry sends the identical command.** Use **Retry** to resend the same command safely.
- **Refused by the rules.** The chosen decision does not fit the current gate states.
- **The service is unavailable. Try again.** If it persists, report the correlation ID, time and steps to support (13.03 §10).

## Related pages
