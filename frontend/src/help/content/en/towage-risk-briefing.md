---
key: towage-risk-briefing
title: Towage risk and briefing
roles: [Tug Master, Master, Crew, Tug Operations Coordinator]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-plan, tug-readiness, towage-execution, tug-operations-offline]
screenshots: []
reviewed: false
---
## What this page is for
This page prepares and issues the **towage risk and briefing record** for a submitted tug job and collects participant **acknowledgements**. It answers: "Which conditions, hazards, preventive and mitigating barriers, stop-work authority and emergency arrangements were briefed, what is the state of the linked risk assessment, and has every participant acknowledged the issued revision?" Issuing the briefing is a human record of the appointed Master or Tug Master; it is not a GO decision or a permit. An incomplete briefing blocks READY.

## Who uses it
- **Tug Master / Master:** to prepare and issue the briefing and make sure no READY decision is made before risk and briefing are complete (13.03 §3).
- **Crew and other participants (pilot, mate, deck crew, engineer):** to acknowledge or dispute the issued briefing in the right role context (13.03 §6).
- **Tug operations coordinator:** to follow the briefing and acknowledgement state (13.03 §7).

## Before you start
- Open the tug from **Tug operations** and choose the job from **Select a submitted tug job**.
- The job's **Towage plan** must be approved and the linked risk assessment approved; otherwise the briefing cannot be issued.
- Issuing needs a server-recorded **Appointment** (Tug Master or Master) for the coordinating tug. Acknowledgement is only for people in the participant list.

## Step by step
1. On the top line, read **State**, **Revision**, **Acknowledgement**, **Risk state** and **Plan state**.
2. In **Readiness input (an incomplete briefing blocks READY)**, see the results and reason codes sent to the readiness gate.
3. Fill in **Conditions confirmed at the briefing** (weather, current, visibility), **Hazards**, **Preventive barriers**, **Mitigating barriers**, **Stop-work authority** and **Emergency arrangements**.
4. Enter the **Linked risk assessment id**; in **Participants**, use **Add** to enter each participant's **Person id** and **Role**.
5. Save with **Create draft** or **Save draft**; when **Missing before submission** is empty, choose an **Appointment** and press **Issue briefing**.
6. As a participant, read the issued revision and press **Acknowledge**. If you disagree, write a **Reason** of at least 10 characters and press **Dispute**.
7. If the briefing must change, write a reason and use **Amend**. The **Acknowledgements** and **Revisions** lists show the history.

## What the states mean
- **Draft / Issued:** the briefing state. Issued means acknowledgements can start; on its own it is not a readiness result.
- **Not issued / Pending / Complete / Disputed:** the participant acknowledgement state. It becomes **Complete** only when every participant has acknowledged the issued revision.
- **Approved and acknowledged / Attention / Blocked / Review required:** the state of the linked risk assessment.
- **Pass / Fail / Unknown / Conditional:** the check result sent to the readiness gate. A missing or unissued briefing and pending acknowledgements are a **Fail** that blocks READY.

## What you cannot do / caution
- No READY decision is made before risk and briefing are complete and participant acknowledgements are recorded (13.03 §3).
- A dispute blocks the start of the operation; its reason appears in the **Acknowledgements** list and must be assessed by the person who issued the briefing.
- If the plan or risk assessment changes after the briefing, the briefing must be reviewed again; earlier acknowledgements do not count for a new revision.
- Nobody acknowledges on someone else's behalf; each participant records their own acknowledgement (13.03 §6).
- No command is sent from this page while offline. An acknowledgement can be queued on **Tug offline operations**.

## If something goes wrong
- **"Required sections are missing." message:** complete the missing codes; check that the plan and risk assessment are approved.
- **"The record changed" message:** use **Reload** to review the current briefing.
- **"The outcome is unknown" message:** **Retry** sends the same request again.
- **No authorization:** check your role, tug and appointment context; contact your tenant administrator if needed (13.03 §10).

## Related pages
