---
key: deck-daily-log
title: Deck daily log
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## What this page is for
The deck daily log keeps the selected ship's **deck watch records** as drafts with their recorded source version. It answers: "What did the watch declare for this date and period about position, heading, speed, weather, visibility, navigation, port and cargo work, ballast, deck work, safety observations and handover, and has a review been recorded for this exact revision?" A saved entry is an unverified declaration, not an approved log.

## Who uses it
- **Officer of the watch (Crew / OOW):** to keep the watch record in real time and in chronological order (13.03 §6).
- **Master or Chief Officer:** to review the saved log and record that review as a human act (13.03 §2). Only an appointment with the Master or Chief Officer duty can record a deck log review.

## Before you start
- In the top context bar, select the **right company and ship**. If no ship is selected, the page shows "Select a ship".
- Reading needs `daily.deck.read`. Creating and saving needs `daily.deck.write` and edit rights on this ship; otherwise you see "Read-only access. Editing and saving are not granted."
- Recording a review needs `daily.deck.review` plus a current Master or Chief Officer appointment for this exact record.

## Step by step
1. Pick the ship. **Recorded drafts** lists each log by date and revision with "Draft · review required".
2. Press **New draft** for a **New daily draft**, or open a listed log to **Edit recorded draft**.
3. Fill in **Watch context** first (**Log date**, **Watch / period**, **Officer of watch reference**). The officer list only offers people available for the declared log date.
4. Fill in **Navigation and weather**, **Port and cargo activities** and **Deck work and handover**. For each field choose **Known**, **Unknown** or **Missing**.
5. Press **Save draft** during or right after the watch. Each save adds a revision to **Recorded revision history**; earlier revisions stay unchanged.
6. The Master or Chief Officer opens **Human review recording**, chooses an **Authorized human appointment** and a **Review entry**, writes a **Review rationale** and presses **Record review entry**.

## What the states mean
- **Draft · declarations unverified / Unverified declaration:** declared by a person; not verified.
- **Known / Unknown / Missing:** the field's data state. Unknown does not mean "No", "Ready" or "Not applicable".
- **Pending · review not recorded:** no human review exists for this revision.
- **Human metadata recorded · unassessed:** a review entry exists for this revision. It does not establish verification, submission or Master safety approval.
- **Record reviewed declaration (unassessed) / Return for correction:** the two review entry types a reviewer can record.
- **Historical revision:** the review belongs to an older revision, not the current draft.

## What you cannot do / caution
- The system never reviews or finalizes the log. Review is a human act by the appointed Master or Chief Officer (13.03 §2).
- Keep entries real time and chronological; do not rewrite an old watch to look like a new one (13.03 §6).
- Do not change someone else's reviewed record; a later save is a new revision and needs a new review (13.03 §6).
- Offline capture only covers explicitly classified fields. An offline conflict is never merged automatically; do not assume your record wins (13.03 §6).

## If something goes wrong
- **"The record changed. Your inputs are retained.":** explicitly discard and reload before a new attempt.
- **"Save outcome unknown":** use **Retry exact saved command**; do not retype the fields.
- **Officer reference unavailable:** keep the field Missing or Unknown; a typed name cannot replace the lookup.
- **"Human conflict resolution required":** open **Review conflict** or the Conflict Center and choose explicitly (13.03 §10).
- **No appointment available:** check your role and ship context with your tenant administrator (13.03 §10).

## Related pages
