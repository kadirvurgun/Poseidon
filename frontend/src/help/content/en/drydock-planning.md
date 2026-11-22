---
key: drydock-planning
title: Drydock planning
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## What this page is for
Drydock planning holds the selected vessel's **drydock packages** (project, shipyard, planned dates and scope items). It answers: "Which work order, defect, certificate or survey checklist is each drydock scope item linked to, and are those links current?" The scope is linked to these records (13.03 §5). **Linkage readiness** is linkage completeness only; it is not a class or flag survey result and not a seaworthiness statement.

## Who uses it
- **Technical Superintendent:** to prepare a drydock package, link the scope to records, plan it and complete it (13.03 §5).
- **Chief Engineer:** to follow how the scope links to defect and work order records and, with permission, verify a package someone else completed (13.03 §5).

## Before you start
- Select the right vessel. If no vessel is selected, the page says **Select a vessel from the workspace to continue.**
- You need drydock read access to see packages. Creating, editing and changing status need write access; verifying needs verify access.
- Have the identifiers and revisions of the work order, defect, certificate and survey checklist records you will link.

## Step by step
1. Open a package from the list, or create an empty draft with **Create drydock package**.
2. Fill in **Drydock project**, **Shipyard**, **Planned start** and **Planned finish**.
3. Add items with **Add scope item**; enter the **Scope item** type, **Title**, **Work package**, **Class requirement**, **Statutory requirement** and **Priority**.
4. On each item, use **Add link** and enter the **Record type**, **Record identifier** and **Pinned revision**.
5. For completion, add an evidence reference under **Completion evidence** with **Add**. Then press **Save**.
6. Check the **Planning blockers** or **Completion blockers** list. Under **Change status**, choose an **Action** (**Plan**, **Back to draft**, **Start drydock**, **Complete drydock**, **Cancel package**) and confirm it. Cancelling needs a note.
7. On a completed package that you did not complete, write a **Reason** of at least 10 characters and use **Verify record** or **Reject verification**.
8. Follow the planned and actual dates and every revision in the **Timeline**.

## What the states mean
- **Draft / Planned / In progress / Completed / Verified / Cancelled:** the package lifecycle. Completed and Verified are not the same.
- **Links current / Attention / Review required / Incomplete:** linkage readiness. Even with links current, this is not a survey result.
- **Linked / Unlinked / Overdue:** the link state of a scope item.
- **Current / Stale / Unavailable:** a linked record compared with its pinned revision. Review a stale link and use **Re-pin to** to pin it to the new revision.
- **Unknown / not set:** no priority was entered. Unknown does not mean "Normal" or "Low".

## What you cannot do / caution
- The page does not decide that a drydock is safe or that the vessel is seaworthy.
- You cannot verify a package you completed. Rejecting verification puts the package back to **In progress**.
- Cost, currency, vendor and budget are external references only; nothing is totalled, approved or ordered here.
- A completed, verified or cancelled package cannot be edited.
- Commands are not sent while offline.

## If something goes wrong
- **The server listed missing or blocking items:** resolve the blocker list and try again.
- **The record changed or the command conflicts:** use **Reload latest** and review.
- **The outcome is unknown:** **Retry** sends the identical command again.
- **You do not have access to this record:** check your role and vessel context; contact your tenant administrator if needed (13.03 §10).
- **You cannot read these linked record types:** check the permission with the record owner or your tenant administrator.

## Related pages
