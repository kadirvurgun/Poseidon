---
key: tug-job-intake
title: Job intake
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## What this page is for
Job intake structures a new **tug job request as a draft** in the selected tug context. It answers: "For this job, what is the assisted vessel, job type, time window, location and required resources (tug count, bollard pull)?" The saved request is a draft declaration; it is not an operational assignment, eligibility, permit or safety approval.

## Who uses it
- **Tug operations coordinator / Shore operations:** to structure the request and enter the assisted vessel, job type, time window and required capability (13.03 §7).

## Before you start
- In the top context bar, select the **right company and tug**.
- You need tug write access. Without it the form does not open and "Access is not available." is shown.
- Incomplete drafts can be saved. Required operational data and authorized review must be resolved before submission.

## Step by step
1. In **Request and assisted vessel**, fill in **Request source**, **Customer reference**, **Assisted vessel**, **Assisted vessel IMO**, **Job type**, **Priority** and **Request owner**. Lists without a choice stay "Unknown".
2. In **Requested time and location**, enter **Requested start (UTC)**, **Requested end (UTC, conditional)**, **Operation area**, **Pickup point** and **Destination point**. Times are UTC.
3. In **Resource needs and notes**, enter **Required BP (tonnes, exact decimal)**, **Required tug count**, **Special requirements** and **Notes**.
4. Press **Save draft**. On success the job detail opens with "Draft saved. Operational approval remains unavailable."
5. To abandon your edits, use **Discard changes**.
6. In the job detail, use **Submit request** to record the request for planning. If required data is missing, submission is blocked and the missing fields are listed.

## What the states mean
- **Draft:** the request is saved and editable; not submitted yet.
- **Submitted:** the request is recorded for planning. It is not an approval, assignment or readiness decision.
- **Cancelled:** the request is closed; it cannot be restarted.
- **Unknown:** no value was chosen for the field. Unknown is not a default and never stands in for "No" or "Not applicable".
- **Check this value.:** the server rejected this field; correct it and save again.

## What you cannot do / caution
- This page assigns no tug and never declares the job ready. The assignment proposal is a separate step and never overrides the Tug Master's onboard safety decision (13.03 §7).
- A request cannot be submitted while required data is missing; an incomplete draft can still be saved.
- Fields of a submitted request cannot be changed from this form; a cancelled request cannot be restarted, create a new one instead.
- While offline the draft is kept on this page only and cannot be saved.

## If something goes wrong
- **"Save outcome uncertain" message:** the exact attempt is retained; after reconnecting use **Retry** to send the same command. Discarding local inputs does not cancel a command already sent.
- **"Conflict" message:** your draft is retained; discard and reload before a new attempt.
- **Check fields:** correct the marked fields.
- **Access is not available:** check your role and tug context; contact your tenant administrator if needed (13.03 §10).

## Related pages
