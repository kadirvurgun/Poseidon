---
key: pilot-program
title: Pilot program weeks and value readout
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## What this page is for
The Pilot program page keeps the pilot weeks for a ship (and optionally a tug), each week's checklist items, pilot issues, daily feedback and the value readout in one place. It answers: "Has every pilot week's items been met by real records, and how did the measured values change against the baseline?" The notice at the top applies: "Software cannot complete a pilot. A checklist item is satisfied only by a link you make to a real record that the server verifies; a missing baseline stays Unknown; only the appointed Product Owner closes a week or records the program decision."

## Who uses it
- **Product Owner:** closes weeks and records the program decision (Continue, Fix, Stop, Expand). This needs a current Product Owner appointment.
- **Pilot team:** links records to checklist items, reports issues, records feedback and measurements.
- **Triage owner:** triages and resolves issues.
- 13.03 defines no separate role for this page; the tenant administration principles apply (13.03 §9).

## Before you start
- Viewing needs tenant-wide read and pilot program read. Creating programs, recording, triage and deciding each need their own permission; week closes and decisions also need a Product Owner appointment.
- The records you link (for example a certificate, crew or work order record) must already exist on their own pages.
- In feedback never enter names, e-mail addresses, phone or ID numbers; use a pseudonym and role only.
- Recording needs a live connection.

## Step by step
1. Open a program from **Pilot programs**. For a new program fill in **Program name**, **Ship**, **Tug (optional)**, **Week 1 start date** and **Calendar time zone (IANA)** in **Create pilot program** and press **Record**.
2. In each **Week** section review the **Checklist item**, **Criterion (source)**, **Area**, **State** and **Linked records** columns.
3. To meet an item, enter the **Checklist item**, **Record type** and **Record identifier** in **Link a record** and press **Link a record**. Remove a wrong link with **Withdraw link** after writing a **Withdrawal reason**.
4. In **Pilot issues** use **Report issue** to enter the area, **Category**, severity and **Issue summary**. The triage owner uses **Triage** with a **Triage note**, then resolves the issue.
5. In **Daily feedback** use **Record feedback** to enter a **Summary** and **Sentiment**.
6. In **Value readout** use **Record measurement** to enter the **Metric**, **Phase** (Baseline or Pilot period), **Value**, **Sample size**, **Period from**, **Period to**, **Evidence reference** and **Method and sample**.
7. If you are the Product Owner, record the outcome and rationale in **Close week**. At the end of the program fill in the decision, **Appointment** and **Rationale** in **Record program decision** and press **Record**.

## What the states mean
- **Linked and verified:** a real record verified by the server is linked to the item. **Link no longer valid** and **Missing** items are not met.
- **Completion required:** the linked record must be in its completed state. "Audit trace proves that a person wrote the record; it does not prove the record or flow is completed."
- **Ready for human close / Not ready:** a recommendation for the week; only the Product Owner closes it as **Accepted** or **Not accepted**.
- **Triaged same day / Triaged late / Triage due today / Overdue:** triage timing of P0 issues against the program's calendar day. For issues with **No same-day target in the source** the system invents no deadline.
- **Measured / Unknown:** when a baseline or pilot value is missing, the readout stays **Unknown**; it is never counted as zero or as an improvement. **Not comparable** values are not compared.
- **Telemetry (declared, unverified):** a declared telemetry value.
- **Expand eligible; human decision pending / Fix recommended:** a program recommendation, not a decision.

## What you cannot do / caution
- "Accepted is possible only when every item is verified and no P0 triage is open, pending or late."
- "Expand is possible only when every week is accepted and values, UAT, defects and P0 are clear."
- If you authored evidence a week or decision rests on (links, feedback, measurements), you cannot close that week or record that decision.
- No week close or decision can be recorded for a fictional fixture program.
- The system never completes a pilot, never closes a week and never fabricates decisions or sign-offs.

## If something goes wrong
- **You have no current Product Owner appointment; week closes and decisions are read-only.** Contact the Tenant Admin for the appointment (13.03 §10).
- **Feedback capture is unavailable until an operator configures the retention policy.** Ask the system administrator to configure the retention policy.
- **The record changed or was already used. Reload.** Open the current state with **Reload**.
- **The outcome is uncertain. Retry sends the identical command.** Use **Retry** to resend the same command safely.
- **Refused by the rules.** For example the linked record may not exist, be out of scope or not be completed.
- **Not permitted.** You authored links, feedback or measurements that this week close or decision rests on; another person must record it.
- A week that has not started cannot be closed (shown as "The record changed or was already used. Reload.").
- **You do not have access to these records.** Check your role and tenant scope (13.03 §10).

## Related pages
