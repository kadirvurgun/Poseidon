---
key: departure-signoff
title: Departure readiness and Master sign-off
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: []
reviewed: false
---
## What this page is for
This page shows an **advisory readiness assessment** for the selected ship and planned departure window, and records the appointed Master's decision. It answers: "What do the crew, technical, statutory, emergency and safety event sources say for this window, and why?" The decision to release, hold or stop the departure belongs to the **Master** (13.03 §2). The system never releases a departure; a recorded sign-off is not a departure clearance.

## Who uses it
- **Master:** to review the assessment and the "Why" list, then record Release, Hold or Deny with a reason (13.03 §2).
- **Shore Operations / Technical Superintendent:** to follow the assessment and recorded Master decisions read-only. Shore staff cannot record a decision in the Master's place.

## Before you start
- In the top context bar, check that the **right company** is selected. The page lists only ships in your scope (13.03 §1).
- You need departure readiness read access to see the page.
- The decide permission alone is not enough to record a decision: you also need a **current Master appointment** for this ship. Without it the page says "You hold no current Master appointment for this ship. Decisions are read-only."

## Step by step
1. Pick the ship from the "Choose a ship" list.
2. In **Planned departure window**, fill in "Departure date" and "Voyage end date" and press **Assess readiness**.
3. In the **Assessment** line, read the state badge, the "Current" or "Forecast" mode, "Assessed at" and "Valid until".
4. In the category table, check the Crew, Technical, Statutory, Emergency and Safety events results.
5. Read the **Why** list. Each row shows an observation that did not pass, its reason code and its source. Review an unexpected row on the source's own page.
6. Under **Master decisions**, see earlier recorded decisions and their validity.
7. If you are the Master, in **Record Master decision** choose Release, Hold or Deny, pick your appointment in "Appointment", write a "Reason" (10–2000 characters) and press **Record**.
8. Use **Refresh** to read the assessment again.

## What the states mean
- **Ready:** no recorded source has an observation that did not pass. This is information only; the departure decision is still the Master's.
- **Conditional:** at least one source reports a condition. Release is not offered for this assessment.
- **Not ready:** at least one source returns "Fail". Other categories passing does not offset it.
- **Review required:** at least one input is "Unknown". An authorised person needs to look at it.
- **Unknown:** the source could not be read or has no data. Unknown does not mean "Pass" or "Ready". No source module is connected for the Emergency category in this version, so that row shows Unknown.
- **Forecast:** the window does not include today. It is a planning forecast only and cannot be released.
- **Current / Stale — inputs changed:** the validity of a recorded decision. When inputs change, the earlier decision becomes stale and is not deleted.

## What you cannot do / caution
- The system never releases a departure and shows no state as a departure clearance. A recorded decision is the appointed Master's own human record.
- Release is possible only for a current READY assessment. Hold and Deny can always be recorded with a reason.
- A reason is required for every decision. Recorded decisions are not changed; a new decision is a new record.
- You cannot record a decision while the assessment is stale or you are offline; refresh first.

## If something goes wrong
- **You are offline:** the assessment may be out of date and sign-off is disabled. Refresh once you are back online (13.03 §10).
- **You do not have access to departure readiness for this vessel:** check your role and vessel context; contact your tenant administrator if needed.
- **Unexpected readiness:** open the "Why" list; if a source looks wrong, raise it with the control owner or subject matter expert.
- **The outcome is uncertain:** "Retry" sends the same command. Do not write a new decision.
- **The decision history changed / The assessment changed:** use "Reload", review the current assessment, then decide.
- **The command was not accepted:** report it to support with a screenshot, the time and your steps.

## Related pages
