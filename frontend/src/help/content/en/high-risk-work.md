---
key: high-risk-work
title: High-risk work
roles: [DPA / HSEQ, Master, Tug Master]
permissions: [hse.risk.read, hse.high_risk_work.read]
related: [risk-assessment, risk-template-library, safety-events]
screenshots: []
reviewed: false
---
## What this page is for
The High-risk work page shows exceptions for fleet work such as hot work, enclosed space, working aloft, electrical isolation, critical maintenance, towage and bunkering in one **read-only** list. It answers: "Which high-risk job has an open issue on risk, permit, critical barrier, crew or equipment, and who holds the next action?" Every dimension is shown separately; there is no aggregate score. Actions open the owning record; this page changes no record and makes no start-of-work decision (13.03 §4).

## Who uses it
- **DPA / HSEQ:** as part of risk governance, watch high-risk work exceptions across the fleet (13.03 §4).
- **Master and Tug Master:** see the blockers for high-risk work on their own vessel and go to the owning record. The decision to start or stop work stays with them (13.03 §2, §3).

## Before you start
- Viewing needs the risk read and high-risk work read permissions. Without them you see "You do not have access to the high-risk work view."
- The list covers only the fleets and vessels in your scope.
- While offline the list shown may be out of date.

## Step by step
1. Choose the **Fleet**, **Vessel** and **Work type** filters. **All** removes a filter.
2. In the table read each job's **Risk**, **Permit**, **Critical barrier**, **Crew** and **Equipment** states separately. The **Critical blockers** and **Review items** badges show counts.
3. Use the **Next action** and **Owner / due** columns to see who needs to do what.
4. To see why a row is in its state press **Why**. A **Dimension**, **State** and **Reason** table opens; close it with **Hide**.
5. To change anything follow the job link or **Open record**; the action is taken on the owning record's own page.
6. Page with **Previous** and **Next**. **Evaluated at** and **Source coverage** show when and from which sources the list was computed.

## What the states mean
- **Ready / Conditional / Attention / Not ready / Blocked:** the dimension's state in its source. A Ready dimension does not mean the job may start; the decision stays with the authorised person.
- **Review required:** a person needs to review it.
- **Unknown:** the source is unavailable; it is never ready. POSEIDON has no permit-to-work source yet, so the **Permit** column always shows **Unknown**.
- **Healthy / Degraded / Failed:** the state of the critical barrier.
- **Unclassified:** the work type is not known or its criticality is not determined; it is not left out of the list.
- **Waiting for source / Resolve stop or dispute / Renew expired item / Complete required item / Reassess / Open owner record:** the next action, shown in a fixed priority order.
- **Due unresolved / Not planned:** the due date or planned start is not known.

## What you cannot do / caution
- This page is read-only; it changes, approves or permits nothing.
- There is no aggregate score; a job with a critical blocker always sorts to the top, and other dimensions being fine never hides it.
- Do not read **Unknown** as ready. A historic active permit is never a current valid permit.
- The decision to start or stop work stays with the Master or Tug Master.

## If something goes wrong
- **The service is unavailable. Try again.** Press **Retry**.
- **You are offline. The list shown may be out of date.** When online press **Reload**.
- If a state looks unexpected, open the **Why** table and ask the owner of the record or the control owner (13.03 §10).
- Without access, check your role and vessel context; if needed contact the tenant administrator (13.03 §10).

## Related pages
