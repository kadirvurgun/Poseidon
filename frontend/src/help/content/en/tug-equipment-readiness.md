---
key: tug-equipment-readiness
title: Tow equipment readiness
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## What this page is for
This page shows the selected tug's **tow equipment declarations** (towing winch, tow hook, towline, emergency release, fire-fighting FiFi) and their **Chief Engineer verifications**. It answers: "What are the SWL, test dates, condition and criticality of each item, who verified it and what does the readiness check say?" The assessment is advice only and feeds the readiness gate; it is not a GO decision.

## Who uses it
- **Tug Master:** to check the winch, towline, tow hook / chain stopper, emergency release, FiFi and critical defect evidence (13.03 §3).
- **Chief Engineer:** to verify or not verify declarations and record the readiness effect of a critical defect (13.03 §5).

## Before you start
- Open the tug from **Tug operations**; this screen needs no assignment.
- Adding or editing a declaration needs tug write and equipment write access.
- Recording a verification needs a server-recorded **Chief Engineer** appointment for that tug.

## Step by step
1. Read the overall badge on the **Assessment** line.
2. On each equipment card, check the result badge, reason code, **SWL (t)**, **Next test due**, **Condition** and **Critical**.
3. For a new declaration, press **Add equipment**; fill in **Equipment type**, **Label**, **SWL (t)**, **Last test**, **Next test due**, **Condition**, **Critical**, **PMS asset ID**, **Certificate ID** and **Notes**, then press **Save declaration**.
4. To change an existing declaration, use **Edit** on its card.
5. As Chief Engineer, choose your **Appointment**, write a reason and use **Verified** or **Not verified** to **Record verification**. A reason is required for "Not verified".
6. See the verification history with revision and time under **Chief Engineer verifications**, and the **Source checks** table at the bottom.

## What the states mean
- **Ready / Conditional / Not ready / Review required:** the advisory assessment. Ready is not a GO decision.
- **Pass / Fail / Unknown:** the check result for one item.
- **Serviceable / Degraded / Failed / Unknown:** the declared condition. If no condition is chosen it stays "Unknown"; it is not counted as serviceable.
- **Not assessed:** criticality has not been assessed. This does not mean "No".
- **Verified / Not verified:** the Chief Engineer's human record.
- **superseded by a later revision:** the verification belonged to an older revision of the declaration; the current revision needs a new verification.

## What you cannot do / caution
- Saving records an unverified declaration. Any change needs a new Chief Engineer verification.
- Completing a record and verifying it are not the same action; verification waits for the authorized person (13.03 §1).
- The equipment type cannot be changed after saving; add a new declaration for another type.
- The page never declares tow equipment safe; the decision stays with the Tug Master (13.03 §3).
- Commands are disabled while offline.

## If something goes wrong
- **"The record changed" message:** use **Reload** to review the current declaration.
- **"The outcome is uncertain" message:** **Retry** sends the same command.
- **Record cannot be verified:** check the evidence and the PMS/certificate source; contact the verifier (13.03 §10).
- **No authorization:** check your role, tug and appointment context; contact your tenant administrator if needed (13.03 §10).

## Related pages
