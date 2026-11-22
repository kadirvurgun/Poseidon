---
key: critical-spares
title: Critical spares and requisitions
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## What this page is for
The critical spares and requisitions page shows the selected vessel's **declared stock, minimum and maximum, low-stock alerts and technical requisition proposals**. It answers: "Which critical spare fell below its minimum, how much do open work orders need, and has a technical requisition been proposed and decided?" Counts are human declarations and are not verified against a store.

## Who uses it
- **Chief Engineer / Technical Superintendent:** to follow critical spare minimum thresholds and start a technical requisition (13.03 §5). The person who proposes a requisition and the person who approves it are different people.

## Before you start
- Viewing needs `spare.read`. Defining spares and recording counts needs `spare.write`, proposing a requisition `requisition.propose` and deciding `requisition.approve`.
- Proposals and decisions require a signed-in human user.
- Choose the right vessel from the **Vessel** list.

## Step by step
1. Choose the vessel. The **Critical spares** view opens. Read the **Low stock** and **Review required** counts at the top.
2. On each table row, read **Spare criticality**, **Equipment criticality**, **Stock state**, **Projected**, **Min / max** and **Open work orders**.
3. Press **New critical spare** for a new spare or **Edit** for an existing one. Enter **Part number**, **Name**, **Unit**, **Minimum stock** and **Maximum stock**. Leave thresholds empty when not decided.
4. To enter a count, tick **Record a new stock count**, fill in **Counted quantity** and press **Save**.
5. On a **Low stock** row with no open requisition, press **Propose requisition**. The server computes the proposed quantity.
6. In the **Technical requisitions** view, an authorised person who did not make the proposal checks **Approved quantity** and presses **Approve for ERP export**, or chooses a **Rejection reason** and presses **Reject**.
7. Open **ERP export reference** on an approved requisition to see the reference.

## What the states mean
- **Low stock:** the declared stock is below the minimum. The requisition proposal is offered on this row.
- **Above minimum (declared):** the declared stock is above the minimum. It does not mean the count was verified.
- **Minimum not configured:** no threshold was entered. An empty threshold is reported as review required, never as healthy.
- **Stock unknown / Consumption unresolved:** stock could not be calculated. Unknown does not mean "enough stock".
- **Proposed / Approved for ERP export / Rejected:** the state of the technical requisition.
- **Critical (declared) / Non-critical (declared) / Unknown:** spare criticality is a declaration.

## What you cannot do / caution
- You cannot decide a requisition you proposed. The page says "You proposed this; another person must decide."
- Proposal and approval are separate human acts. Approval only creates an ERP export reference; no purchase order, vendor or price is managed here.
- The page has no separate reorder threshold field; low stock is assessed against the minimum stock.
- While offline, data cannot be refreshed and no action can be taken.

## If something goes wrong
- **The record changed:** use **Retry** to refresh the page and try again.
- **Outcome unknown:** **Retry same command** sends the same command.
- **The request was rejected by a rule:** check the stock state and open requisition information.
- **You do not have permission:** check your role and vessel context; contact your tenant administrator if needed (13.03 §10).

## Related pages
