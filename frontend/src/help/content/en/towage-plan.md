---
key: towage-plan
title: Towage plan
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## What this page is for
The towage plan records, for a submitted tug job, the **tug roles and configuration, information exchange, operating limits, communication, emergency release, contingency and snap-back zone controls**. It answers: "How will this job be carried out, within which limits and with which emergency arrangements?" Plan approval is a human record of the appointed Master or Tug Master; it is not a GO decision or a permit. Limits are plan inputs stated with their source; the system supplies no default values.

## Who uses it
- **Tug operations coordinator / planner:** to prepare the plan and submit it for approval (13.03 §7).
- **Tug Master / Master:** to confirm configuration, line, BP/capability, communication, limits, contingency and stop-work conditions and approve or return the plan (13.03 §3).

## Before you start
- Open the tug from **Tug operations** and choose the job from **Select a submitted tug job**.
- Editing the plan needs tug write and plan write access. Tugs in the plan are chosen from tugs with a current assignment proposal for this job.
- Approving or returning needs a server-recorded **Appointment** (Tug Master or Master) for the coordinating tug.

## Step by step
1. Read **State** and **Revision**, then the **Missing before submission** list.
2. In **Tug roles and configuration**, use **Add** for each tug; enter **Position**, **Towing method**, **Coordinating (lead) tug**, **Towline source**, **Connection point**, **Towline equipment id** and **Emergency release equipment id**.
3. Write the **Master / pilot / Tug Master information exchange** text.
4. In **Operating limits (plan inputs)**, enter a **Value** and **Source of the limit** for each **Parameter**. A new limit starts empty.
5. Fill in **Communication plan** (primary and standby channel, working language, loss of communication procedure), **Emergency release**, **Contingency** and **Snap-back zone controls**.
6. Save with **Create draft** or **Save draft**; once nothing is missing, press **Submit for approval**.
7. The approver chooses an **Appointment**, writes a **Reason** of at least 10 characters and presses **Approve** or **Return**.
8. If an approved plan must change, write a reason and use **Amend**. The **Approval records** and **Revisions** lists show the history.

## What the states mean
- **Draft:** the plan can be edited.
- **In review:** the plan was submitted for approval; a decision is awaited.
- **Returned:** the approver sent the plan back with a reason; correct and resubmit it.
- **Approved:** the Master or Tug Master approved this revision of the plan. This is not a GO decision; the readiness gate and briefing are still required.
- **Unknown:** no value was chosen for the field. "Unknown" for snap-back zones never stands in for "No".
- **The tug request changed after this plan was saved.:** the plan is based on an older request revision and must be reviewed.

## What you cannot do / caution
- The person who authored or submitted the plan cannot approve it.
- A plan with missing required sections cannot be submitted; an incomplete draft can be saved.
- The system proposes no limit, channel or threshold value; every value comes from the planner with its source.
- An approved plan is not permission to start; starting needs current briefing acknowledgements and the Tug Master's GO record (13.03 §3).
- Commands are disabled while offline.

## If something goes wrong
- **"Required sections are missing." message:** complete the codes in **Missing before submission**.
- **"The record changed" message:** use **Reload** to review the current plan.
- **"The outcome is unknown" message:** **Retry** sends the same request again.
- **No authorization:** check your role, tug and appointment context; contact your tenant administrator if needed (13.03 §10).

## Related pages
