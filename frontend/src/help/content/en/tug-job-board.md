---
key: tug-job-board
title: Job board
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [canRead, canWrite]
related: [tug-command, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: []
reviewed: false
---
## What this page is for
The job board lists the **draft tug job requests** of the selected tug context for planning. It answers: "Which requests exist, which time window do they ask for, how many tugs and how much bollard pull (BP) do they need, and what is still missing for planning?" The board shows draft declarations only; it is not an operational assignment, eligibility, permit or safety approval.

## Who uses it
- **Tug operations coordinator / Shore operations:** to see requests, follow conflict and readiness information and prepare an assignment proposal (13.03 §7).
- **Tug Master:** to see upcoming work for their tug in advance (13.03 §3).

## Before you start
- In the top context bar, select the **right company and tug**. The board shows requests of this context only.
- You need read access to see the board. Creating a request needs write access; without it the "New request" button is not shown.

## Step by step
1. In the summary at the top, see the number of requests on this page, jobs with a recorded blocker, those awaiting readiness review and those with unknown readiness. **Counts cover this page only.**
2. If needed, type a job number or assisted vessel name in **Search requests**, choose the **Priority**, **State** and **Time window** filters and press **Apply filters**.
3. On each row, read the requested window (UTC), draft assignments, required tug count, required BP against planning capacity, readiness and operational conflict.
4. Open **Why / next action** on a row to see the assigned tug references, the comparison scope and the recorded reasons.
5. Use **Open job** to go to the request's details.
6. For a new request, press **New request** to open **Job intake** (needs write access).
7. Use **Refresh board** to read the data again and **Previous** / **Next** for other pages.

## What the states mean
- **Draft / Submitted / Cancelled:** the request's lifecycle state. Submitting records the request for planning; it is not an approval, assignment or readiness decision.
- **Blocked:** the request has a recorded blocker. The reason is under "Why / next action".
- **Review required:** the readiness information waits for review by an authorized person.
- **Unknown:** there is not enough source data for readiness or conflict. Unknown does not mean "Ready" or "no conflict", and the board draws no readiness conclusion from it.
- **Authorized human review required / Boundary touch review required:** two requests may overlap in time or touch end to end. A person decides.
- **Profile BP (unverified):** the bollard pull shown is a declared value, not a measured or verified one.

## What you cannot do / caution
- The board never declares a request "ready" or "safe". A shore assignment proposal does not remove the Tug Master's onboard safety authority (13.03 §7).
- Fields of a submitted or cancelled request are not changed from the board; a cancelled request cannot be restarted, create a new one instead.
- The time window filter uses server time. Requests without a start stay in the list as "Unknown".
- Data shown while offline may be old and the board cannot be refreshed.

## If something goes wrong
- **Current board unavailable:** check your connection and use "Refresh board".
- **No draft requests:** if you applied filters, try a different search or priority; new requests appear in this context.
- **Access unavailable:** check your role and tug context; contact your tenant administrator if needed (13.03 §10).
- **Unexpected blocker or review:** open "Why / next action" and review the recorded reasons with the request owner.

## Related pages
