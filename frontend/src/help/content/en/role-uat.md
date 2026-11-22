---
key: role-uat
title: Role UAT records
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## What this page is for
The Role UAT records page records user acceptance test (UAT) runs with ship and tug roles, their scenario outcomes and findings as **human evidence**, and compares them with the agreed threshold. It answers: "Does the attested UAT evidence for every role and scenario meet the threshold the humans agreed?" The notice at the top applies: "Records human UAT evidence only. The system never accepts UAT and never decides a pilot GO; automated test results are attachments, not a PASS."

## Who uses it
- **UAT facilitator:** opens a run, records the participant's observed scenario outcomes and findings, and submits the run for attestation. Ship participant roles include Master, DPA, Chief Engineer and Crew (13.03 §2, §4, §5, §6).
- **Attester:** a person other than the facilitator attests the run.
- **UAT owners / Product Owner:** record the agreed threshold and use the result on the **Pilot go/no-go review** page.

## Before you start
- Viewing needs tenant-wide read and UAT read. Recording runs and outcomes, recording the threshold and attesting each need their own permission.
- The UAT owners must have agreed the threshold beforehand; the system never sets one.
- Choose a pseudonym for the participant. Never use a name, e-mail or crew number.
- Recording needs a live connection; it is disabled offline.

## Step by step
1. Choose **Ship** or **Tug** with the **Operation mode** buttons.
2. In **UAT evidence against the agreed threshold** review the state, the **Open blockers**, **Open major findings**, **Attested runs** and **Awaiting attestation** counts and the scenario table.
3. If no threshold exists, fill in **Minimum scenario pass rate**, **Maximum open major findings**, **Design conformance**, **Agreement reference** and **Rationale** in **Record agreed threshold** and press **Record**.
4. In **UAT runs** use the **New UAT run** form to enter **Role**, **Environment**, **Build commit** and **Participant pseudonym**, then press **Record**.
5. Open the run. With **Record outcome** choose the **Scenario** and **Outcome**; add **Assistance given** and an **Automated evidence reference** if relevant. For a problem use **Record finding** with **Severity** and **Finding summary**.
6. When all scenarios are recorded press **Submit run for attestation**. A person other than the facilitator writes an **Attestation statement** in **Attest run** and presses **Record**.
7. In **Open findings** close a finding with a **Resolution** and **Resolution note**, then press **Resolve**.

## What the states mean
- **Threshold not set:** "No human-agreed threshold is recorded. UAT cannot pass until one is recorded."
- **Criteria not met / Evidence incomplete:** attested evidence does not meet the threshold, or not every role and scenario is covered.
- **Criteria met; human decision pending:** the best possible state. It is not a UAT acceptance or a pilot GO; the decision is human.
- **Not executed:** no result exists for the scenario. A scenario that was not run never counts as passed.
- **Open / Submitted / Attested:** the run state. Only attested runs count toward the threshold.
- **Passed / Failed / Blocked / Not run:** the participant's observed scenario outcome.
- **Blocker / Major / Minor:** finding severity. An open blocker fails the gate.
- **Fictional fixture:** a synthetic sample record; it is not real UAT evidence.

## What you cannot do / caution
- You cannot attest a run you facilitated: "Attestation must be recorded by a different human than the facilitator."
- You cannot resolve a finding you reported. A blocker closes only as **Resolved (fixed and retested)**; **Risk accepted** is not offered.
- An automated (Playwright) result is attached evidence only; the outcome is the participant's observed result.
- The system never accepts UAT, never sets a threshold and never fabricates attestations or sign-offs.

## If something goes wrong
- **You do not have access to these records.** Check your role and tenant scope and contact the Tenant Admin (13.03 §10).
- **The record was not found in your tenant.** Check the run link and the selected mode.
- **The record changed or was already used. Reload.** Open the current state with **Reload**.
- **The outcome is uncertain. Retry sends the identical command.** Use **Retry** to resend the same command safely.
- **Refused by the rules.** For example the attester may be the same person as the facilitator.
- **You are offline. Recording is disabled until the connection returns.** When it returns press **Refresh**.

## Related pages
