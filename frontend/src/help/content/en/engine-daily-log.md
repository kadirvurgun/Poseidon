---
key: engine-daily-log
title: Engine daily log
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## What this page is for
The engine daily log keeps the selected ship's **engine watch records** as drafts with their recorded source version. It answers: "What did the engine watch declare for engine status and hours, fuel and lube oil consumption, fresh water, critical measurements, alarms, defects, bunkering, bilge and environmental controls, and has the Chief Engineer recorded a review of this exact revision?" A saved entry is an unverified declaration. Draft data is not turned into emissions, fuel assurance or readiness conclusions.

## Who uses it
- **Watch engineer (Crew / OOW / Engineer):** to keep the engine watch record in real time and in chronological order (13.03 §6).
- **Chief Engineer:** to review the saved log, link it to work orders and defects and record the review as a human act (13.03 §5). Only an appointment with the Chief Engineer duty can record an engine log review.

## Before you start
- In the top context bar, select the **right company and ship**.
- Reading needs `daily.engine.read`; saving needs `daily.engine.write` and edit rights on this ship. Recording a review needs `daily.engine.write` with edit rights on this ship, `daily.engine.review`, plus a current Chief Engineer appointment for this exact record.
- Linking work orders or defects also needs read access to those records.

## Step by step
1. Pick the ship. **Recorded drafts** lists each log by date and revision with "Draft · review required".
2. Press **New draft**, or open a listed log to **Edit recorded draft**.
3. Fill in **Engine watch context**, **Engine status and hours**, **Consumption and production**, **Measurements, alarms and defects** and **Work and environmental context**. For each field choose **Known**, **Unknown** or **Missing**.
4. Press **Save draft**. Each save adds a revision to **Recorded revision history**.
5. On a saved log, use **Work orders and defects**: choose a **Record type** and **Record**, then **Link**. A link pins one exact revision; **Remove link** removes it.
6. If needed, open **Open fuel record from saved source** or **Open downtime record from saved source**. Quantities and units are not copied.
7. The Chief Engineer opens **Human review recording**, chooses an **Authorized human appointment** and a **Review entry**, writes a **Review rationale** and presses **Record review entry**.

## What the states mean
- **Unverified declaration / Known / Unknown / Missing:** declared values and their data state. Unknown does not mean "No", "Ready" or "Not applicable".
- **Pending · review not recorded:** no human review exists for this revision.
- **Human metadata recorded · unassessed:** a Chief Engineer review entry exists for this revision. It does not establish verification or Chief Engineer sign-off.
- **Current revision / Newer revision exists:** whether a linked work order or defect has changed since it was linked. A link is lineage only; it does not verify, close or approve that record.
- **lookup unavailable — UNKNOWN:** the linked record type cannot be read with your access.

## What you cannot do / caution
- The system never reviews or finalizes the log. Review is a human act by the appointed Chief Engineer (13.03 §5).
- Whoever authored any revision of the log cannot review it. No appointment is offered to an author and the page says "You authored this log revision; an independent reviewer must record the review."; the server also refuses such a review for separation of duties.
- Keep entries real time and chronological; do not change someone else's reviewed record (13.03 §6).
- Linking a defect here does not record a restriction or close it; do that on the defect or work order page (13.03 §5).
- Links need a current online connection; nothing is linked offline. Offline conflicts are never merged automatically.

## If something goes wrong
- **"The log entry changed. Reload it before changing links.":** reload, then link again.
- **"Save outcome unknown":** use **Retry exact saved command**; do not retype.
- **"Human conflict resolution required":** open **Review conflict** or the Conflict Center and choose explicitly (13.03 §10).
- **No appointment available or access denied:** check your role and ship context with your tenant administrator (13.03 §10).

## Related pages
