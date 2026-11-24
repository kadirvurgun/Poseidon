---
key: departure-signoff
title: Departure readiness and Master sign-off
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: [1, 2, 3, 4, 5, 6, 7, 9]
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
6. If the assessment is not current and Ready, read **Why Release is unavailable**. It says when the window is a forecast, lists the categories that do not pass yet with their results, and explains which record would close the Emergency (no 61.02 drill source connected yet), Statutory (no required-certificate set defined) and Safety events (no approved severity policy) source gaps. It only explains the server's categories; it does not change the gate.
7. Under **Master decisions**, see earlier recorded decisions and their validity.
8. If you are the Master, in **Record Master decision** choose Release, Hold or Deny, pick your appointment in "Appointment", write a "Reason" (10–2000 characters) and press **Record**.
9. Use **Refresh** to read the assessment again.

## What the states mean
- **Ready:** no recorded source has an observation that did not pass. This is information only; the departure decision is still the Master's.
- **Conditional:** at least one source reports a condition. Release is not offered for this assessment.
- **Not ready:** at least one source returns "Fail". Other categories passing does not offset it.
- **Review required:** at least one input is "Unknown". An authorised person needs to look at it.
- **Pass / Condition / Fail / Unknown:** the category result in the category table. **Unknown** means the source could not be read or has no data; it does not mean "Pass" or "Ready". No emergency preparedness source is connected for the Emergency category in this version, so that row shows Unknown. Statutory can stay Unknown even when every recorded certificate is valid, because no source defines the required-certificate set for the ship. Statutory also includes data trust signals (DATA_TRUST_*); without data trust read access Statutory shows Unknown. Safety events shows Pass only when the safety event source is fully covered and reports no open high-severity event. In this version no approved severity policy defines which open events block departure, so Safety events shows Unknown with SAFETY_EVENT_SEVERITY_POLICY_UNCONFIGURED even when no open high-severity event is recorded; review the open events on the Safety events page.
- **Forecast:** the window does not include today. It is a planning forecast only and cannot be released.
- **Current / Stale — inputs changed:** the validity of a recorded decision. When inputs change, the earlier decision becomes stale and is not deleted.

## What you cannot do / caution
- The system never releases a departure and shows no state as a departure clearance. A recorded decision is the appointed Master's own human record.
- Release can be recorded only for a current READY assessment; every category must pass. Because the Emergency category stays Unknown in this version, the assessment cannot reach Ready and Release is not offered; **Why Release is unavailable** shows why. Even on Ready the release decision is the Master's; the system approves nothing. "Hold and Deny can always be recorded with a reason."
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
