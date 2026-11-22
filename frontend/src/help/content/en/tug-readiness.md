---
key: tug-readiness
title: Tug readiness gate
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## What this page is for
The readiness gate assesses a tug assignment's operational readiness from source checks as **advice only**, and lets the appointed Tug Master or Master record a reasoned **GO/NO-GO decision**. It answers: "What do the crew, equipment, PMS, certificate, communication, weather, bollard pull margin, assignment and briefing checks say, and which blockers exist?" The assessment is not a decision: "The system never issues GO; only the appointed Master or Tug Master records a GO/NO-GO decision with a reason."

## Who uses it
- **Tug Master / Master:** to review the assessment and blockers and record a GO or NO-GO decision (13.03 §3).
- **Tug operations coordinator / Shore operations:** to follow readiness and blockers; they do not decide (13.03 §7).

## Before you start
- Open the page from an assignment: on **Assignment and conflicts**, use the **Operation readiness gate** link under a proposal. Without a selected assignment the page shows "Select an assignment from the job's assignment list."
- Recording a decision needs tug write access and a server-recorded **Appointment** (Tug Master or Master) for that tug, and the assignment must still be Proposed.
- There is no READY without the briefing: **Towage risk and briefing** must be issued and acknowledged by every participant (13.03 §3).

## Step by step
1. Read the state badge on the **Assessment** line. The note next to it says "Advisory, not a GO decision."
2. Check **Required bollard pull (t)**, **Verified available bollard pull (t)** and **Bollard pull margin (t)**. A missing value shows "Unknown".
3. Review the codes under **Critical blockers**, **Review reasons** and **Conditions**.
4. In the category table, see the result of each category (Crew, Towage competence, Equipment, Tow equipment, PMS and defects, Certificates, Communication, Weather, Bollard pull margin, Assignment, Towage risk and briefing). For detail, read the reason codes in the **Source checks** table.
5. If you hold decision authority, choose **NO-GO** or **GO** in the **Record Master decision** form, enter your **Appointment** and a **Reason**, and press **Record**.
6. See recorded decisions in the **Master decisions** list.

## What the states mean
- **Ready (READY):** every source check passes (no blocker, unknown or condition). This is information, not a decision; the GO decision is still recorded by the Tug Master or Master.
- **Conditional (CONDITIONAL):** the conditions in "Conditions" apply; GO is not accepted on a CONDITIONAL assessment in this version.
- **Not ready (NOT_READY):** at least one critical blocker exists.
- **Review required (REVIEW_REQUIRED):** some sources are unknown or await review. An unknown check is never counted as "Ready".
- **Pass / Fail / Unknown:** the result of a single category or check.
- **GO / NO-GO:** the human decision and reason recorded by the Tug Master or Master.

## What you cannot do / caution
- GO can only be recorded against a READY assessment; the server refuses GO on CONDITIONAL, NOT_READY or REVIEW_REQUIRED. NO-GO can always be recorded with a reason. The page may still offer GO on a CONDITIONAL assessment; the server refuses it.
- There is no READY while the risk and briefing are incomplete or participant acknowledgements are missing; the GO option is then disabled (13.03 §3).
- A shore user's assignment proposal never overrides the Tug Master's onboard safety decision (13.03 §7).
- If the assessment changes before your decision, the decision is refused; review the current assessment again.
- Commands are disabled while offline.

## If something goes wrong
- **"The assessment changed" message:** use **Reload**, review the current assessment and decide again.
- **"The outcome is uncertain" message:** **Retry** sends the same command.
- **Unexpected readiness:** open the category table and source check reasons; review them with the control owner or subject-matter expert (13.03 §10).
- **No authorization:** check your role, tug and appointment context; contact your tenant administrator if needed (13.03 §10).

## Related pages
