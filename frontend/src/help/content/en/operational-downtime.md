---
key: operational-downtime
title: Operational downtime
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## What this page is for
This page records a ship's **declared operational interruptions** (downtime) together with their source lineage. It answers: "Which interruption was declared on this ship, when, for what cause and linked to which defect or work order, and where are its review and closure?" The page does not decide loss of hire entitlement, safe service or any safety release.

## Who uses it
- **Chief Engineer / Technical Superintendent:** to record downtime caused by a technical failure or maintenance and link it to the defect or work order history (13.03 §5).
- **Master:** to follow the operational impact of downtime and review it when appointed (13.03 §2).
- **Appointed reviewer (Master, Chief Officer, Chief Engineer, Fleet Manager, DPA):** to verify, return for correction or close a downtime record.

## Before you start
- In the top context bar, check that the **right company** is selected; the page lists only ships in your scope.
- Reading needs downtime read access; opening a draft needs write access. Review actions also need review access and a current duty appointment for this ship.
- To open linked downtime, you can use **Open linked downtime draft** on a defect or work order page.

## Step by step
1. Pick the ship from the "Select a ship" list. The **Recorded operational interruptions** list opens.
2. Use **Search causes on this page** to narrow the records on this page.
3. For a new record, press **New downtime draft**.
4. For each field, first choose its value state: **Missing**, **Unknown** or **Declared value**. You enter a value only while "Declared value" is selected.
5. In "Interruption start" and "Interruption end", type a full timestamp with seconds and an explicit UTC offset.
6. Press **Save unassessed draft**. An incomplete draft can be saved.
7. In **Downtime review and closure**, link claim evidence with **Select evidence** if needed, then press **Submit for review**.
8. If you are an appointed reviewer, choose your "Appointment" and use **Verify**, **Return for correction** or **Close**.
9. Review earlier revisions under **Immutable draft history**.

## What the states mean
- **Draft / Review required:** the recorded content is unassessed. The reasons are under "Why this draft remains unassessed".
- **Not submitted / Open / In review:** the lifecycle state of the record.
- **Verified (human):** an appointed person other than the recorder verified it. Completed is not Verified (13.03 §1).
- **Closed:** the downtime was closed. Verified closure is not a safety release.
- **Unknown:** the value was not declared or could not be determined. Unknown does not mean "No" or "Not applicable". For this reason loss of hire and closure stay "Unknown".
- **Loss of Hire candidate:** a candidate only; it states no entitlement or amount.
- **Recorded interval:** an unassessed engineering observation; it does not replace the declared minutes.

## What you cannot do / caution
- Whoever recorded the downtime cannot verify or close it; another appointed person must.
- Submit, verify and close are not possible while required facts are missing.
- Linking claim evidence is not verification.
- No description, time or closure decision is copied from the owning source (defect, work order); only the exact revision link is kept.
- Offline, current sources cannot be read and there is no offline storage; your entries stay only in the open page.

## If something goes wrong
- **The save outcome is unknown:** your original command is protected. With the same account, use "Retry exact downtime command".
- **The saved draft changed:** someone else changed the record. Your entries are kept; use "Read current draft" to reload, or discard.
- **Downtime records or current access are unavailable:** check your role and vessel context; contact your tenant administrator if needed (13.03 §10).
- **The source changed:** use the current revision of the owning source for a new draft.
- **Command not accepted:** report it to support with a screenshot, the time and your steps.

## Related pages
