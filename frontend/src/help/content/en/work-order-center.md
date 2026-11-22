---
key: work-order-center
title: Work orders
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: []
reviewed: false
---
## What this page is for
The work order center holds the **draft work orders and declared execution records** of the selected vessel. It answers: "Which work orders exist on this vessel, and which have been released, executed, had evidence submitted and been verified by a person?" Every saved revision is kept in immutable history. Verification and closure are not return to service, permit or risk approval.

## Who uses it
- **Chief Engineer / Technical Superintendent:** to open, release, verify and close planned and unplanned work orders (13.03 §5).
- **Engineer / crew:** to start assigned work and record and submit readings, spares, photo and document evidence (13.03 §6).

## Before you start
- Reading needs `workorder.read`, `equipment.read` and `maintenance.read`. Drafts need `workorder.write`, release `workorder.release`, execution `workorder.execute`, verification `workorder.verify` and closure `workorder.close`.
- Release, verification and closure require a signed-in human user.
- Choose the right vessel from **Select a vessel or tug**.

## Step by step
1. Choose the vessel. In **Recorded work orders**, filter this page with **Search returned work order numbers** and open a record.
2. For new work, press **New work order draft**, fill in the fields and press **Save unassessed draft**. Incomplete drafts can be saved.
3. In **Execution and evidence**, if you have release permission, choose **Declare required evidence** under **Required evidence policy**, tick the evidence kinds and press **Release work order**. If you release without declaring, verification stays blocked until a releaser uses **Change required evidence** → **Save required evidence**.
4. The executor presses **Start work**, fills in **Readings**, **Spares consumed**, **Photo and document evidence**, **Work done** and **Findings**, presses **Save evidence** and then **Submit evidence**.
5. An authorised verifier who did not execute the work reviews the evidence. They press **Record verification**, or choose a **Return reason** and press **Return to executor**.
6. For a verified work order, an authorised person presses **Record closure**.
7. See earlier revisions in **Immutable work order history**. If a downtime record is needed, use **Open linked downtime draft**.

## What the states mean
- **Not released for execution. / Released / In progress:** the work is not yet released, is released, or is being executed.
- **Evidence submitted:** the executor declared the work and the evidence. It does not mean verified.
- **Verified (human record):** recorded by an authorised verifier who did not execute the work. **Closed (human record)** is a separate step.
- **Required evidence missing / Present, pending human verification / Verified by a human:** the state of the required-evidence gate.
- **Review required: policy not configured:** no required evidence was declared. Not configured never means nothing is required; verification stays blocked.
- **Returned by verifier:** the work went back to the executor with the reason shown.

## What you cannot do / caution
- If you executed the work, you cannot verify or close it. The page says "You executed this work, so you cannot verify or close it."
- Evidence submitted, Verified and Closed are separate human records. The page never declares equipment ready for service.
- The "Saved work order source" summary describes the saved draft fields only. It always shows Draft / Review required and may say "Release, evidence acceptance, verification and closure remain unavailable." The actual state and the available actions are in **Execution and evidence**.
- Readings are kept as declared; no unit conversion or threshold is applied.
- The offline evidence queue works only when an approved offline policy exists and while work is in progress. Conflicts are never merged automatically; use **Open conflict center** and a person decides.

## If something goes wrong
- **The record changed or this action is not allowed in its current state:** use **Reload** to read the current record and try again.
- **Outcome unknown:** **Retry same command** sends the same command; it does not create a new record.
- **Permission or human authority is missing:** check your role and vessel context; contact your tenant administrator if needed (13.03 §10).
- **The required-evidence gate rejected this action:** complete the missing evidence kinds. If a record cannot be verified, check the evidence and its provenance with the verifier (13.03 §10).

## Related pages
