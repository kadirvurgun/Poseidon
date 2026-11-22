---
key: tug-assignment
title: Assignment and conflicts
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [tug-job-board, tug-readiness, tug-crew-readiness, tug-equipment-readiness, towage-plan]
screenshots: []
reviewed: false
---
## What this page is for
This page shows the **tug assignment proposals** for a submitted tug job and their **conflicts** with other jobs. It answers: "Which tug was proposed for this job, with which role and time window, does it conflict with other work, and has the Tug Master accepted the proposal?" Shore proposes, the Tug Master acknowledges; the page makes no dispatch or release decision. The notice at the top says so: "The system never issues GO."

## Who uses it
- **Tug operations coordinator / Shore operations:** to make an assignment proposal after reviewing conflicts and readiness on the job board (13.03 §7).
- **Tug Master:** to confirm a proposal for their own tug in context and accept or reject it (13.03 §3).

## Before you start
- Open the tug from **Tug operations**; if no tug is selected the page points you there.
- Proposing needs tug write and plan write access; the job must be **Submitted**.
- Responding needs a server-recorded **Appointment** (Tug Master or Master) for that tug. The person who made a proposal cannot respond to it.

## Step by step
1. Open the job number from **Select a submitted tug job**. The **Assignments and readiness** link in the job detail opens this page too.
2. For existing proposals, read **State**, **Tug Master acknowledgement**, **Role**, **Planned start** and **Planned end**.
3. In **Conflicts**, see time and resource conflicts with other jobs, their result badge and reason code.
4. For a new proposal, choose **Tug**, **Planned start**, **Planned end** and **Role** (Lead, Support, Escort) in the **Propose assignment** form and press **Propose assignment**.
5. As Tug Master, choose your **Appointment**, add a **Reason** if needed and press **Accept** or **Reject**. A reason is required to reject.
6. To remove a proposal, enter a reason and press **Withdraw proposal**.
7. Use the **Operation readiness gate** and **Crew and towage competence** links under each proposal to move to the readiness screens.

## What the states mean
- **Proposed / Withdrawn:** the proposal's lifecycle. Proposed does not mean the tug has been dispatched.
- **Pending / Accepted / Rejected:** the Tug Master's response to the proposal. Accepted confirms the proposal scope; it is not a GO decision.
- **Pass / Fail / Unknown:** the result of a conflict check. Unknown shows there was not enough source data for the check; it never means "no conflict".
- **No conflict observed; inputs may still be incomplete.:** no conflict was observed, but this is not a full eligibility result.
- **The request changed after this proposal; a new proposal is required.:** the job request changed after the revision the proposal was based on.

## What you cannot do / caution
- A shore assignment proposal never overrides the Tug Master's onboard safety decision (13.03 §3, §7).
- The proposer cannot accept their own proposal; the response belongs only to the person appointed for that tug.
- No proposal can be made for a draft or cancelled job.
- Commands are disabled while offline.

## If something goes wrong
- **"The outcome is uncertain" message:** **Retry** sends the same command; do not create a new proposal.
- **"The record changed" message:** use **Reload** to review the current record, then act again.
- **"You are not authorized for this action." / "You do not have access to this record.":** check your role, tug and appointment context; contact your tenant administrator if needed (13.03 §10).
- **Unexpected conflict:** note the reason code and review it with the owner of the other job.

## Related pages
