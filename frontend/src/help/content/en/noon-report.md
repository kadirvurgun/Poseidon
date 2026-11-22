---
key: noon-report
title: Noon report
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## What this page is for
The noon report page keeps the selected ship's **daily noon declarations** as drafts with their recorded source version. It answers: "What did we declare for report time, voyage, position, distance, weather, cargo, machinery hours, fuel and ROB, and has the Master recorded a review of this exact revision?" A saved draft is an unverified declaration. Saving it does not establish Master or Chief Engineer approval, and the page draws no emissions, fuel assurance or readiness conclusion from it.

## Who uses it
- **Officer preparing the report (Crew / OOW):** to fill in and save the noon draft for the right ship and date (13.03 §6).
- **Master:** to review the saved report and record that review as a human act (13.03 §2). Only an appointment with the Master duty can record a noon review.

## Before you start
- In the top context bar, select the **right company and ship**. If no ship is selected, the page shows "Select a ship".
- Reading needs `daily.noon.read`. Creating and saving drafts needs `daily.noon.write` and edit rights on this ship; without them you see "Read-only access. Editing and saving are not granted."
- Recording a review needs `daily.noon.write` with edit rights on this ship, `daily.noon.review`, plus a current Master appointment for this exact record.

## Step by step
1. Pick the ship. The **Recorded drafts** list shows each report by date and revision with the badge "Draft · review required". Use **Previous drafts** / **More drafts** to page.
2. Press **New draft** to open **New daily draft**, or open a listed report to **Edit recorded draft**.
3. Fill in the groups (**Report and voyage**, **Position and activity**, **Weather and cargo**, **Machinery, fuel and ROB**). For each field choose a **Data state**: **Known**, **Unknown** or **Missing**. Incomplete drafts can be saved.
4. Press **Save draft**. The page confirms "Draft saved. Human review and operational approval remain separate." Each save adds a revision under **Recorded revision history**.
5. On a saved report, the Master opens **Human review recording**, chooses an **Authorized human appointment**, a **Review entry** ("Record reviewed declaration (unassessed)" or "Return for correction") and writes a **Review rationale**, then presses **Record review entry**.
6. If fuel or a port event must be recorded, use **Use saved operational source** and **Open fuel record from saved source** or **Open port event from saved source**. Quantities, units and approvals are not copied.

## What the states mean
- **Draft · declarations unverified / Unverified declaration:** the values are declared by a person; nobody has verified them.
- **Known / Unknown / Missing:** the data state of a field. Unknown does not mean "No", "Ready" or "Not applicable", and Missing is not zero.
- **Pending · review not recorded:** no human review has been recorded for this revision.
- **Human metadata recorded · unassessed / Recorded · unassessed:** a Master review entry exists for the shown revision. Review recording does not establish verification, submission or Master safety approval.
- **Historical revision:** the review entry belongs to an older revision and does not review the current saved draft.

## What you cannot do / caution
- The system never reviews or finalizes the report. Recording a review is a human act by the appointed Master (13.03 §2).
- Whoever authored any revision of the log cannot review it. No appointment is offered to an author and the page says "You authored this log revision; an independent reviewer must record the review."; the server also refuses such a review for separation of duties. If the Master wrote the report, an independent reviewer with review authority must still record the review.
- A new save creates a new revision; the earlier review stays on its own revision. Review again after changes.
- A typed free-text reference cannot replace an authorized lookup; keep the field Missing or Unknown.
- Offline, only explicitly classified fields can go to the encrypted queue. Review, approval and verification fields are excluded.

## If something goes wrong
- **"The record changed. Your inputs are retained.":** someone saved a newer version. Explicitly discard and reload before a new attempt.
- **"Save outcome unknown":** fields are frozen; use **Retry exact saved command**, do not retype.
- **"No current source-authorized human appointment is available for this record.":** check your role and ship context with your tenant administrator (13.03 §10).
- **"Human conflict resolution required":** an offline capture conflicts with the server. Open **Review conflict** or the Conflict Center and choose explicitly; nothing is merged automatically (13.03 §10).
- **Daily sources unavailable or access denied:** use **Retry reading sources**; if it persists, report the time and your steps to support.

## Related pages
