---
key: pms-command-center
title: PMS command center
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## What this page is for
The PMS command center collects the maintenance **exceptions** of the selected vessel on one screen. It answers: "Does this vessel have critical overdue jobs, open work orders, defect alerts or critical spare gaps, and why?" The page is an advisory exception view; it is not a fitness, readiness or safety decision. Healthy items are not listed, only counted.

## Who uses it
- **Chief Engineer / Technical Superintendent:** to follow overdue jobs, open work orders, defect alerts and critical spare gaps (13.03 §5).
- **Master:** to see the operational restriction impact of PMS and defect records (13.03 §2).

## Before you start
- You need equipment read permission (`equipment.read`) to see the page.
- Each section depends on its own module's read permission. For example, without defect read permission the **Condition alerts** section stays "Access required: not shown".
- Choose a vessel first. Without a vessel the page only shows the **Choose a vessel.** list.

## Step by step
1. Choose the vessel from the list. If none is in your scope, the page says "No vessel is in your scope."
2. Read the **Exception summary** badge and the **Last refresh** time at the top.
3. Review the sections: **Critical overdue jobs**, **Due, overdue and review jobs**, **Open work orders**, **Condition alerts** and **Critical spare gaps**.
4. Open **Why** on an item to see the recorded reason codes and, if present, the **Observed** time.
5. Use **Open source record** to go to the source. A defect item opens [Defects and temporary measures](/app/defects), a work order item opens [Work order center](/app/work-order-center).
6. Press **Refresh** to read the data again.

## What the states mean
- **Blocked / Review required / Attention:** the state of the exception summary. Details are in the section items.
- **No open exceptions (advisory):** the sources that were read returned no open exception. This is not a readiness or fitness result.
- **Critical / Attention / Review required:** the severity of an item.
- **Source unavailable: unknown, not zero:** the section source could not be read. Unknown does not mean "zero", "no problem" or "Ready".
- **Access required: not shown:** you have no permission for this section; neither content nor count is shown.
- **No exceptions from this source:** the section was read and returned no exception. The number of healthy items is shown separately.
- **Stale data / Freshness unknown:** the reading behind the item is not current, or its freshness is unknown.

## What you cannot do / caution
- The page changes no record; it only reads. Work orders, defects and spares are handled on their own pages.
- The page never declares a vessel "ready" or "safe". The departure decision belongs to the Master (13.03 §2).
- A defect counts as closed only through a human closure record, and only for the declaration revision that was closed. If the declaration changes later, the item returns to review.
- Data shown while offline may be old, and **Refresh** is not available.

## If something goes wrong
- **The record could not be loaded:** check your connection and use **Reload**.
- **You do not have the server permission for this action or record:** check your role and vessel context; contact your tenant administrator if needed (13.03 §10).
- **Unexpected exception:** open **Why** and review the source record with the responsible owner or SME (13.03 §10).
- **Application error:** report the Correlation ID, screen, time and steps to support (13.03 §10).

## Related pages
