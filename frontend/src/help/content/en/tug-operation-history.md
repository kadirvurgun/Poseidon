---
key: tug-operation-history
title: Tug operation history
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## What this page is for
Operation history shows the **read-only history** of tug jobs, executions, daily logs and evidence. It answers: "Which jobs ended with which outcome, were the executions verified, is there an evidence record and which daily logs belong to these jobs?" Cancelled and aborted records stay visible; nothing is deleted or relabelled.

## Who uses it
- **Tug operations coordinator / Shore operations:** to follow evidence completeness, event and history records of completed jobs (13.03 §7).
- **Tug Master:** to see past jobs and execution timelines of their own tug (13.03 §3).

## Before you start
- Open the tug from **Tug operations**; without a selected tug the page shows "Select a tug vessel to continue."
- Tug read access is enough; the page changes no data.
- History is read from the server; it cannot be refreshed while offline.

## Step by step
1. In **Filters**, optionally set **Tug ID**, **From (UTC date)**, **To (UTC date)** and **Outcome**, then press **Apply filters**. The date range is at most 400 days.
2. On each table row, read **Job** (job number, assisted vessel, job type, plan and briefing state), **Activity time**, **Outcome** and **Evidence**.
3. In **Executions**, see each execution's state, verification state, duration, Master record and number of evidence entries; use **Open execution timeline** to go to the execution console.
4. Use the links in **Daily logs** to open the related log. The note "not linked by the log" shows the log does not link this execution.
5. Use **Load more** for older records. **Read at** shows when the data was read.

## What the states mean
- **Not started / In progress / Completed (recorded) / Aborted / Cancelled / Mixed:** the overall job outcome. Completed (recorded) does not mean the job was verified.
- **Verified, evidence recorded / Verified, no evidence entry / Verification rejected / Evidence unverified / Unknown:** the evidence and verification state.
- **verification pending / verified / verification rejected:** the verification state of a single execution.
- **None:** no linked record exists for this job. This does not mean no record was needed.

## What you cannot do / caution
- No record is edited, deleted or verified on this page.
- No silent deletion: cancelled jobs and aborted or rejected executions remain in the history.
- Completed ≠ Verified: verification is a separate person's record.
- The data shown is the state at **Read at**; it is not refreshed while offline.

## If something goes wrong
- **"The filter is not valid" message:** check that the tug ID is a UUID and the date range does not exceed 400 days.
- **"The history could not be loaded." message:** check your connection and press **Retry**.
- **"You do not have access to this tug scope.":** check your role and tug context; contact your tenant administrator if needed (13.03 §10).
- **Record cannot be verified:** check the evidence reference and its source in the execution timeline; contact the verifier (13.03 §10).

## Related pages
