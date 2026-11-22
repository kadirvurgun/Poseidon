---
key: today
title: Today
roles: [Master, Tug Master, Chief Engineer, Crew / OOW]
permissions: [canRead]
related: [fleet, tug-job-board, offline-conflict-center, sources]
screenshots: [1, 2, 3, 4, 5, 6]
reviewed: false
---
## What this page is for
"Today" gathers, for the ship or tug you select, the **alerts that need your attention now** in one list. It answers: "Which record on this vessel is waiting for a decision or a check today, and which source says so?" The page does not decide anything; it shows the alert, its source and the next step.

## Who uses it
- **Master** and **Tug Master:** to see the day's exceptions, their owner and the next step (13.03 §2, §3).
- **Chief Engineer:** to follow maintenance plan alerts (13.03 §5).
- **Crew / OOW:** to see work that falls to them (13.03 §6).

## Before you start
- In the top context bar, confirm that the **right company and vessel** are selected (13.03 §1). An alert you see belongs to the selected vessel.
- You need read access to the workspace to open this page.
- If no vessel is selected, the page first shows the "Select a vessel" list.

## Step by step
1. Select the ship or tug you are working on. The selected vessel is shown as the "Current vessel"; use "Switch vessel" to change it.
2. Look at the **Source coverage** section at the top. It shows which sources (regulatory, planned maintenance, tug operations) could be read.
3. Read the alert cards from top to bottom. Each card comes from one source and suggests a next step (for example "Review draft", "Review maintenance plan", "View tug job").
4. Press **Why?** to see the reason behind a card. The section shows the rule, the source record, its revision, the read time and any unknown inputs.
5. Follow the card's link to the record's own page and act there, within your permissions.
6. Use **Refresh** to read the data again.

## What the states mean
- **Available (unverified):** the source was read. This does not mean its content was verified.
- **Missing / Unavailable in this context:** the source could not be read for this vessel. An alert you cannot see does not mean everything is fine.
- **Partial view:** some sources could not be read. A count of zero does not mean "no problem".
- **Every listed source was read and returned no items:** no alert came back. This is not a safety clearance.
- **Unknown:** the value is missing from the source or could not be determined. Unknown does not mean "No", "Ready" or "Not applicable".
- **Review required / Planning attention / Advisory:** the card's priority. A review-required card waits for an authorized person to look at it.
- **Completed ≠ Verified:** completing a record does not mean an authorized verifier has confirmed it (13.03 §1).

## What you cannot do / caution
- This page does not make a departure, operation or safety decision. That decision belongs to the authorized Master or Tug Master.
- No command runs from this page; actions happen on the record's own page.
- Data shown while offline is old and unverified. The "Why?" details do not open while offline.
- A read or save time does not prove that the source is correct.

## If something goes wrong
- **Offline:** continue your critical work and watch the sync state. If a conflict appears, go to the Conflict Center (13.03 §10).
- **Access denied / Vessel unavailable in your scope:** check your role and vessel context; contact your tenant administrator if needed.
- **Unexpected alert:** open "Why?"; if the rule or source looks wrong, pass it to the control owner or subject-matter expert.
- **Source unavailable / Too many requests:** wait a moment and use "Retry".
- **The server response failed validation:** nothing from it is shown; report it to support with a screenshot, the time and your steps.

## Related pages
