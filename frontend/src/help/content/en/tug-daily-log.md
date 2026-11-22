---
key: tug-daily-log
title: Tug daily log
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: []
reviewed: false
---
## What this page is for
The tug daily log records, for one date and watch, **jobs, engine runtime meters, fuel and defects**, and compares them with the day's towage executions for **reconciliation**. It answers: "How many jobs were completed in this day/watch, do the meter hours match the execution times, how much fuel was used and which defects were reported?" Reconciliation is advisory; the Master review is a separate human record.

## Who uses it
- **Crew / watchkeeper:** to keep the watch record in real time and in order (13.03 §6).
- **Tug Master / Master:** to review a submitted log and verify or return it (13.03 §3).

## Before you start
- Open the tug from **Tug operations**.
- Creating and editing a log needs tug write access.
- Review needs a server-recorded **Appointment** (Tug Master or Master) for that tug. Whoever wrote or submitted the log cannot review it.

## Step by step
1. Open an existing log from the list, or choose a **Log date** and **Watch** and press **Create daily log**.
2. Enter the **Time zone (IANA)**.
3. In **Engine runtime meters**, enter the **Opening reading**, **Closing reading** and, if needed, **Reset or rollover evidence** for each **Meter**.
4. In **Fuel used**, enter the value, unit and **Fuel source reference**.
5. Tick the executions that belong to this log in **Towage executions of the day**; fill in **Linked defect identifiers** and **Daily summary** (summary at least 20 characters).
6. Press **Save**. On the **Reconciliation** line, check jobs completed, execution hours, meter hours and defects reported.
7. When the **Blockers** list is empty, press **Submit for Master review**.
8. As Master, choose your **Appointment**, write a reason and press **Master: verify log** or **Master: return log**.

## What the states mean
- **Draft / Submitted / Returned / Verified:** the log state.
- **Master review — Pending / Attention / Reviewed:** Pending = awaiting review; Attention = returned, or submitted while reconciliation is not Reconciled; Reviewed = verified by the Master. A submitted log is not a verified log.
- **Reconciled / Review required / Incomplete:** the advisory comparison result. Reason codes are listed below it.
- **open interval:** an execution is still running; execution hours are not final yet.
- **Safety events — Unknown:** this page does not count safety events; no "no events" conclusion is drawn.
- **Synced / Waiting to sync / Conflict / Failed:** the sync state. Waiting to sync shows the change has not reached the server yet.

## What you cannot do / caution
- Whoever wrote the log or submitted it for Master review cannot verify it.
- The reconciliation result is not an approval; the Master does the review.
- Completing a record and verifying it are not the same action (13.03 §1).
- While offline, one unsaved change is kept in this page's memory and sent with its original key when the connection returns; keep the page open. Submitting and reviewing happen online.
- If a conflict appears, do not assume your own record wins automatically (13.03 §6).

## If something goes wrong
- **"The server listed missing or blocking items." message:** clear the codes in **Blockers**.
- **"The record changed or the command conflicts." message:** use **Reload latest** to review the current log; the retained local change is dropped.
- **"The outcome is unknown." message:** **Retry** sends the identical command again.
- **Conflict:** a change kept on this page is never merged; use **Reload latest**. Daily entries captured on **Tug offline operations** that conflict are resolved there; they are also listed in the **Material conflict center** (13.03 §10).

## Related pages
