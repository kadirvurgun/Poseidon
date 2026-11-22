---
key: certificate-command-center
title: Certificate center
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## What this page is for
The Certificate command center lists the certificate **declarations** of the selected vessel with their declared calendar states. It answers: "Which certificates on this vessel are close to expiry or a survey window, and which records still wait for human review?" The dates shown are declarations. The page never decides that a certificate is valid or verified (13.03 §1, §8).

## Who uses it
- **Master, Chief Engineer / Technical Superintendent:** watch the vessel's certificate calendar, enter new certificate drafts and start renewal preparation.
- **Regulatory / Assurance user:** check that declarations rest on an official, versioned source and record an assigned Auditor's assertion (13.03 §8).
- Users with read access only see the list and cannot change it.

## Before you start
- Viewing needs certificate read. Entering drafts needs certificate write and edit scope on that vessel; extracting document suggestions needs file read and extraction; recording a human assertion needs directory write, the verification-record permission and a current **Auditor** appointment for this vessel and certificate. Protected crew declarations also need crew read, crew certificate read, crew identity read and file read.
- Choose the right vessel first: press it in the **Select a vessel** list (13.03 §1).
- The page needs a live online read. Offline you see "Current certificate sources unavailable offline or hidden." and no offline grant is inferred.

## Step by step
1. Open the vessel from the **Select a vessel** list.
2. Use **Certificate scope** to choose **Vessel certificates** or, if you hold the permission, **Protected crew declarations**. Narrow the list with **Declared calendar filter**.
3. Open a record by pressing its row. Rows carry the label **Unverified certificate declaration**.
4. For a new record press **New certificate draft**. Fill **Document name**, **Declared regulatory basis**, **Declared flag**, the survey window and **Declared availability** from the source document, then press **Save unverified draft**.
5. To get suggestions from the linked document press **Extract from linked document** in **Review document facts**. Compare each suggestion with the source, tick **I checked this fact against the source** and **I checked that this document belongs to this vessel and certificate**, then press **Confirm selected facts and save draft**.
6. As an assigned Auditor, in **Human assertion record** fill **Select actual assignment** and **Select recorded claim** and enter the reason, then press **Record unassessed assertion**.
7. To start a renewal use **Prepare renewal** under **Certificate related work** on the record. This takes you to the [Certificate renewal](/app/renewal-workflow) flow.

## What the states mean
- **Declared expiry <90 days / <60 days / <30 days / Declared expiry passed:** a calendar observation from the declared expiry date. It is not a decision on validity.
- **Declared survey window open / Beyond due window:** position relative to the declared survey window.
- **Calendar review required:** a date is missing, ambiguous or conflicting. The record is not ready until a person checks it against the source document.
- **Unknown:** the value was not declared. An unknown value never means "not missing" or "compliant"; it needs review.
- **Low confidence · review / Ambiguous · review / Review required:** a document suggestion not yet confirmed by a person. A confidence score is not verification.
- "This page only. Calendar observations are separate from qualification; these are not fleet-wide readiness totals."

## What you cannot do / caution
- Saving a draft does not verify the certificate. Writing the record, confirming document suggestions and the Auditor's assertion are separate steps; Completed ≠ Verified.
- Automatic extraction only suggests. Missing values stay unknown and only the facts you select and correct update the draft.
- An administrator role alone does not permit recording a human assertion; a current appointment and the source file are required.
- A saved certificate is never silently changed. Every save creates a new revision and older revisions stay in **Recorded revision history** (13.03 §1).
- The page grants no class, flag or authority approval and does not decide voyage fitness. That decision belongs to authorised people.

## If something goes wrong
- **Certificate source or access unavailable. No valid, empty or verified inventory is inferred.** Press **Retry source read**. If it persists, check your role, vessel and object scope; contact the Tenant Admin if needed (13.03 §10).
- **Outcome unknown.** Keep the original command and resend the same request with **Retry exact certificate command**.
- **Current source changed.** Your local fields are kept; use **Back to records**, read the current record again and redo your change.
- **Automatic reading is not available for this document:** enter the values manually from the source document.
- If a record cannot be verified, check its evidence and source lineage and refer it to the verifier (13.03 §10).

## Related pages
