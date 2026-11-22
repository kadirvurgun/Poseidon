---
key: risk-assessment
title: Risk assessment
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## What this page is for
The Risk assessment page records the hazards, potential consequences, control measures, initial and residual risk and stop-work conditions for a job or operation on a vessel. It answers: "Have the hazards and controls for this job been considered, reviewed and acknowledged by the participants?" The page is a **development target only**: an approval here is a recorded human review, not a permit, an expert risk acceptance or an HSE gate pass (13.03 §2, §4).

## Who uses it
- **Master and Tug Master:** have the assessment prepared for work on the ship or tug and make the stop-work decision. Decision authority stays with them (13.03 §2, §3).
- **DPA / HSEQ:** oversee risk governance and record the review decision (13.03 §4).
- **Crew / OOW / Engineer:** as participants, acknowledge or dispute the approved assessment for themselves (13.03 §6).

## Before you start
- Choose a vessel to open its risk assessments. Reading needs the risk read permission; writing a draft needs the risk write permission and edit scope on that vessel. The review decision needs the risk approve permission; acknowledgement needs the risk acknowledge permission.
- Ratings need a published risk method (matrix) in the tenant. Without one, ratings stay **Not assessed**. A tenant HSEQ owner configures the matrix and another person publishes it (Risk template library page).
- Risk commands need a current online connection; nothing is queued or approved offline.

## Step by step
1. Choose the vessel and open an assessment from the list, or press **New assessment**.
2. Enter an **Assessment number**. Optionally enter a **Work type** and use **Start from a published template (copy only)** to pick a matching template; the template is only copied into the new draft.
3. Fill in **Title**, **Operation / work context**, **Work type**, **Work scope** and **Risk method**. For **Stop-work required** choose **Yes**, **No** or **Unknown** and, if needed, write the **Stop-work conditions**.
4. With **Add hazard**, enter for each hazard the **Job step**, **Potential consequences**, **Initial risk** and **Residual risk** (Likelihood, Severity, Rationale). With **Add control**, add **Control measures**, the **Control owner** and **Barrier references**.
5. In **Participants** write one actor ID and role per line. Link related records with **Add link** if needed.
6. Press **Save draft**, then **Submit for review**. **Blocking items** lists what is missing.
7. A reviewer writes a **Reason** of at least 10 characters and presses **Approve (human review record)** or **Return**.
8. After approval each participant presses **Acknowledge** or, with a reason, **Dispute** for themselves. If a change is needed use **Amend (opens a new draft)**.

## What the states mean
- **Draft / In review / Returned / Approved (human record):** the assessment lifecycle. Approved is a human review record, not a permit.
- **Not assessed / Method not configured:** there is no rating. The system uses no default matrix, multiplication or low/medium/high threshold.
- **Stop-work required — Unknown:** no decision has been made yet; it is never read as "No".
- **Acknowledgement not applicable before approval / Acknowledgements pending / All participants acknowledged / Disputed – unresolved:** the participant acknowledgement state. A dispute is never closed by someone else's acknowledgement.
- **Changed since pinned / Unavailable:** a linked record changed after the pinned revision or can no longer be read; review it again.

## What you cannot do / caution
- An author or the submitter of the review cycle cannot approve the same assessment (separation of duties).
- Everyone acknowledges only for themselves and only the exact approved revision.
- An approval here is not a permit, an expert risk acceptance or an HSE gate. The decision to start or stop work stays with the Master or Tug Master.
- A draft started from a template carries no rating, signature, participant confirmation or approval.
- Offline an assessment cannot be submitted, approved or acknowledged.

## If something goes wrong
- **Blocked until the listed items are completed.** Complete the blocking items and submit again.
- **This record changed elsewhere.** Press **Reload** to see the current revision, then repeat the action.
- **You are not permitted to take this action (separation of duties or scope).** Ask another authorised person to review, or check your role and vessel context; if needed contact the tenant administrator (13.03 §10).
- **The risk workspace is unavailable for your current access.** Check your permissions.
- If a rating looks unexpected, check the risk method and its revision; ask the control owner or the HSEQ owner (13.03 §10).

## Related pages
