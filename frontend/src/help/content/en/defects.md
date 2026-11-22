---
key: defects
title: Defects and safeguards
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## What this page is for
The defects and temporary measures page holds the **recorded defects, restrictions and declared temporary measures** of the selected vessel. It answers: "How critical is this defect, which safeguard or operational restriction is recorded, was the corrective action verified, and what still blocks closure?" Every step is a human record; the page grants no safety or operating authority.

## Who uses it
- **Chief Engineer / Technical Superintendent:** to record the safeguard or restriction and the readiness impact of a critical defect, and to follow corrective action (13.03 §5).
- **Master:** to see the operational restriction impact of a defect (13.03 §2).
- **Engineer / crew:** to declare a defect they found (13.03 §6).

## Before you start
- Reading needs `defect.read` and `equipment.read`. Declarations and lifecycle records need `defect.write`.
- Verification needs `defect.verify`, effectiveness `defect.effectiveness` and closure `defect.close`, each with a signed-in human user.
- Choose the right vessel from **Select a vessel or tug**.

## Step by step
1. Choose the vessel. If needed, filter **Recorded declarations** with **Filter returned expiry observations** and open a declaration.
2. For a new defect, press **New defect declaration**, fill in the fields and press **Save unassessed declaration**. Incomplete drafts can be saved.
3. On a saved declaration, press **Start lifecycle** in the **Defect lifecycle** section.
4. Use **Record assessment** to choose the criticality and write the statement (at least 10 characters).
5. Use **Record safeguard or restriction** and choose **Safeguard** or **Operational restriction** as **Measure type**. For a temporary measure, enter the expiry (UTC).
6. Use **Record corrective action** to describe the work done.
7. An authorised person who recorded no corrective action records **Record verification** and then **Record effectiveness**.
8. When no **Closure blockers** remain, an authorised person who also recorded no corrective action records **Close and return to service**. Records appear under **Lifecycle records**.

## What the states mean
- **Detect / Assess / Safeguard / Action / Verify / Effectiveness:** the lifecycle stage.
- **Closed (recorded human decision):** closure is a human decision; it is not a green "fixed" result.
- **Critical rule applies:** a critical defect cannot close without a safeguard.
- **Active / Expired — blocking / Lifted / Ended at closure:** the state of a safeguard or restriction. An expired temporary measure must be renewed or lifted.
- **Critical defect without an active safeguard / Criticality unknown and no safeguard:** alerts. These defects also appear under **Condition alerts** on the [PMS command center](/app/pms-command-center).
- **Unknown:** criticality has not been assessed. Unknown does not mean "Low" or "Non-critical".

## What you cannot do / caution
- If you recorded the corrective action, you cannot record verification, effectiveness or return to service; they need their own authority and a different person.
- Verified, Effective and Closed are separate human records. A declared stage or measure does not authorize service, verify effectiveness or permit closure.
- If the declaration changes after verification, closure is blocked and the record returns to review.
- Asset criticality is not copied as this defect's assessment.
- While offline, current defect sources are unavailable; there is no offline storage.

## If something goes wrong
- **Someone else changed this record:** use **Reload** to read the current record and record again.
- **The save outcome is unknown:** **Retry the same command** sends the same command.
- **You do not have the server permission for this action or record:** check your role and vessel context; contact your tenant administrator if needed (13.03 §10).
- **Closure blockers:** complete the missing record in the list. If a record cannot be verified, check the evidence with the verifier (13.03 §10).

## Related pages
