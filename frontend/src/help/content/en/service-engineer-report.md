---
key: service-engineer-report
title: Service and engineer reports
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## What this page is for
This page records and lists the **reports of service and engineer visits** on the selected vessel. It answers: "Who did what work on which equipment and for which work order, which parts and measurements were recorded, and is the report verified?" A report is linked to an equipment ID, a work order and defects, so it is tied to the work order and equipment history (13.03 §5). A verified report is a human record; it does not close a defect or a work order.

## Who uses it
- **Chief Engineer / Technical Superintendent:** to link reports to equipment and work order history, follow what is missing and verify reports they did not author (13.03 §5).
- **Engineer:** to enter a report as a draft after a service and submit it for verification (13.03 §6).

## Before you start
- If no vessel is selected, the page says **Choose a vessel.** and lists the vessels in your scope. Select the right vessel.
- You need service report and equipment read access to see reports. The **New report** button is shown only with write access and while online.
- Have the equipment, work order, defect and evidence file IDs ready. The work order, defect and evidence fields are shown only if you can read those records.

## Step by step
1. Open a report from the **Reports** list, or start a new draft with **New report**.
2. Fill in **Report no.**, **Service provider**, **Engineer**, **Service date**, **Reason and scope** and **Work performed**.
3. If needed, use **Add part** and **Add measurement** to enter the parts used and the measurements. If you do not know a measurement result, leave it as **Unknown**.
4. Under **Links**, enter the **Equipment ID**, **Work order ID** and the linked defect and evidence file IDs (one per line).
5. Save with **Save draft**. If the report shows a **Missing before submission** list, complete those items first.
6. Send the report with **Submit for verification**.
7. On a submitted report you did not author, write a **Reason** of at least 10 characters and use **Record verification** or **Return to author**.
8. To change a verified report, write a reason and use **Amend (new revision)**. Every revision stays in **Immutable history**.

## What the states mean
- **Draft / Submitted / Returned:** the report's lifecycle. Submitting is not verification.
- **Verified (recorded human decision) / Verified by a non-author:** an authorized person who did not author or submit the report recorded a verification. Completed and Verified are not the same.
- **Awaiting verification:** the report was submitted and is not yet verified.
- **An earlier revision was verified; this one is not:** after an amendment the new revision needs verification again.
- **current revision / changed since linked / unavailable:** the state of a linked equipment, work order or defect compared with the revision that was linked.
- **Accepted transport, not verified evidence:** an attached file is only an accepted transport.
- An **Unknown** measurement result does not mean "Within limits".

## What you cannot do / caution
- You cannot verify a report you authored or submitted; the server rejects it.
- A verified report does not close a defect or a work order, and it does not show that the equipment is safe.
- Evidence originals are not changed; an amendment opens a new revision and the old revision stays in history (13.03 §1).
- Nothing is recorded while offline. When the connection returns, **Retry the same command** sends the same command again.

## If something goes wrong
- **Required data or records are missing:** complete the missing list and save again.
- **Someone else changed this record:** use **Reload** to open the latest version, then save again.
- **You do not have the server permission for this action or record:** check your role and vessel context; contact your tenant administrator if needed (13.03 §10).
- **A report cannot be verified:** check the linked records and evidence files; talk to an authorized verifier (13.03 §10).
- **The record could not be loaded:** send the screen, time and steps to support (13.03 §10).

## Related pages
