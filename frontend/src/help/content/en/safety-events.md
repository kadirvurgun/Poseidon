---
key: safety-events
title: Safety events & near misses
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: []
reviewed: false
---
## What this page is for
The Safety events & near misses page lets you report a safety event on a vessel quickly and follow the human steps that come after it: triage, investigation, independent review, corrective and preventive actions, verification, effectiveness and closure. It answers: "What happened, who is handling it, and did the action actually work?" Nothing here decides severity, root cause or closure automatically; each is a separate human record. The page follows the Finding → Action → Verification chain, and **closed is not the same as effective** (13.03 §4).

## Who uses it
- **Crew / OOW / Engineer and Master:** report near misses, incidents, accidents, fatigue, environmental or cyber events (13.03 §2, §6).
- **DPA / HSEQ:** as the safety bridge, watch critical events and overdue actions and manage investigation and action effectiveness (13.03 §4).
- Triage, investigation, review, identity reading, action writing, verification and effectiveness review are separate permissions; each user sees only the parts they hold.

## Before you start
- Choose the right vessel (**Choose a vessel**). Without access you see "You do not have access to safety events for this vessel."
- Reporting needs the report permission; reading the list needs the read permission.
- A **Confidential** report and **Submit report** need a connection. Offline you can only save a named draft, and only on a device prepared beforehand.
- Time is entered in UTC.

## Step by step
1. See the vessel's events in the **Events** list. Use **Only my reports** to see just your own.
2. For a new record press **Report an event**. Choose **Named** or **Confidential** as the **Reporting mode**. For a confidential report that needs follow-up, choose **Give me a private follow-up code**.
3. Fill in **Event type**, **When did it happen?**, **Operation**, **Location on board**, **Summary**, **What happened**, **Was immediate action taken?** and optionally **Your severity estimate**. If the time is not known choose **Unknown** and fill in **Why is the time unknown?**.
4. You can **Save draft** while it is incomplete. When **Still needed before submission** is empty press **Submit report**. An unsubmitted draft can be withdrawn with **Withdraw draft** and a reason.
5. If a follow-up code was issued, store the code in **Your confidential follow-up code** privately right away and press **I have stored it**. It is shown only once. Later, enter it as **Follow-up code** under **Confidential follow-up** and press **Open** to read team replies.
6. A triager records severity and **Investigation required** with a reason in **Triage** and presses **Record triage**, or sends it back with **Return to reporter**.
7. An investigator presses **Open investigation**, fills in **Contributing factors**, **Root causes**, **Barriers** and **Uncertainties**, then presses **Save investigation** and **Submit for review**. In **Independent review** another person chooses **Approve**, **Return** or **Reject**.
8. In **Corrective and preventive actions** use **Add action** with an owner, due date and the finding it addresses. The owner records **Report completion**, another person records **Verify** and a third records **Review effectiveness**.
9. When **Closure blockers** is empty, a reviewer (review permission) who is neither the reporter nor an investigation author presses **Close event**. In **Messages** you can send messages with **Team only** or **Shared with reporter** visibility.

## What the states mean
- **Draft / Submitted / Returned / Triaged / Investigating / Action tracking / Closed / Withdrawn:** the event lifecycle.
- **Severity — Not triaged / Unassessed:** no human classification yet. The **Reporter estimate** is only a suggestion. **Severity policy: not configured – human classification only** shows that the system derives no severity.
- **Investigation need is UNKNOWN:** no decision has been made; it does not mean "no" and it blocks closure.
- **Action — Open / Completed / Verified:** Completed is not Verified, and Verified is not Effective.
- **Effectiveness — Pending / Effective / Not effective / Inconclusive / No actions / Not visible:** effectiveness is a human review per action and is separate from closure. **No actions** does not mean effective.
- **Open safety events** summary: **High-severity events open**, **Review required** or **No open high-severity event**. It is a warning only; it gives no readiness decision.
- **Confidential (protected):** the reporter identity is in a restricted vault and is not shown to readers.
- **Captured offline / Waiting to sync / Saved on this device. Not yet sent.** Queued is not sent.

## What you cannot do / caution
- A reporter cannot triage their own report. An investigation author, the submitter or the reporter cannot give the independent review.
- Only the action owner reports completion. Neither the owner nor the completer can verify; none of the owner, completer or verifier can review effectiveness. These are separate records by different people.
- Root causes are human statements; the system never proposes one.
- A submitted report can never be withdrawn or deleted by its author.
- In a confidential report the identity is opened only on a need-to-know basis, by someone holding the identity read permission and choosing a **Purpose**; each reading is recorded with its purpose. Anonymous reporting needs the public intake channel, which is not configured.
- An Inconclusive review can be repeated; Effective and Not effective are final; an action that did not work needs a new follow-up action.

## If something goes wrong
- **The server refused the command:** complete the missing or blocking items listed below it.
- **The record changed meanwhile. Reload and review before trying again.** Press **Reload**.
- **The outcome is unknown (connection lost).** Use **Retry the same command**; it will not be applied twice.
- **This action is not permitted for you.** Check your role, vessel and object context; if needed contact the tenant administrator (13.03 §10).
- **The device key is unavailable** or **Online re-authorisation is needed:** captured data is retained; re-authorise when online. For an application error, contact support with the correlation ID, screen, time and steps (13.03 §10).

## Related pages
