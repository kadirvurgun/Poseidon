---
key: renewal-workflow
title: Certificate renewal
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## What this page is for
The Certificate renewal page records a certificate's renewal preparation and internal workflow step by step: preparation, application, awaiting the authority, the issued replacement, verification and completion. It answers: "Where is this certificate's renewal, and who takes the next step?" It is an internal workflow record only; it does not establish legal validity or authority approval (13.03 §1, §8).

## Who uses it
- **Master, Chief Engineer / Technical Superintendent:** prepare the renewal draft and record requirements and the application.
- **Assigned renewal owner:** moves the workflow forward. It is a role held by members with the renewal-owner permission; the workflow cannot open or be validated without one.
- **Verifier and completer:** compare the issued replacement with its source and verify it, and, as a different person, complete the renewal (13.03 §1, §8).

## Before you start
- Viewing needs certificate read. Preparing a draft needs certificate write and renewal write on that vessel; verifying needs renewal verify; completing needs renewal complete.
- First choose the right vessel in **Select a vessel** (13.03 §1).
- The certificate to renew must have a current saved revision. A superseded certificate cannot be renewed.
- Workflow steps need a live connection. Offline you see "You are offline. Workflow steps are unavailable until the connection returns."

## Step by step
1. Open a **Renewal record** from **Recorded renewal drafts**. For a new renewal use **Choose certificate for renewal** to go to the [Certificate command center](/app/certificate-command-center) and press **Prepare renewal** on the record.
2. Fill the **Certificate renewal form**: **Declared renewal due date**, the survey window, **Planned location**, **Assigned renewal owner role** and **Survey required declaration**, **Documents required declaration**, **Actions required declaration**. Leave a value you do not know as **Unknown**.
3. Save the draft with **Save unverified draft**.
4. In **Renewal workflow**, choose the step open to your role from **Available steps**, for example **Confirm requirements complete**, **Record application** or **Await authority decision**. Fill the required fields and press **Record step**.
5. When the authority issues the new certificate, use **Record issued replacement** and select it. Its own document is used as evidence.
6. The verifier uses **Verify replacement** and sets **Issuer matches**, **Certificate number matches**, **Subject matches**, **Dates match** and **Scope matches** by comparing with the source.
7. A different person completes the renewal with **Complete and supersede**. If the authority refused, use **Record authority rejection**; if preparation is abandoned, use **Cancel preparation**.

## What the states mean
- **Draft / Preparing:** the renewal is being prepared; the form can be edited.
- **Ready to apply / Application recorded / Awaiting authority:** the application stage. The authority decision happens outside the system.
- **Issued, unverified:** the replacement is recorded but not yet compared with its source by a person.
- **Verified by reviewer:** the verifier recorded the comparison; the renewal is not yet complete.
- **Completed:** the internal workflow is closed and the replacement supersedes the original. It is not a decision on the certificate's legal validity.
- **Cancelled / Rejected:** the flow stopped.
- A requirement can be **Complete / Not required / Unknown**. **Unknown** does not mean "Not required"; the server does not advance the flow on an unknown requirement.

## What you cannot do / caution
- The person who recorded the issued replacement, or who entered that replacement certificate, cannot verify it. The completer must differ from the verifier, from the person who recorded the issued replacement and from the replacement certificate's author.
- Verification and completion are human decisions only; Completed ≠ Verified.
- Planning declarations do not establish completed surveys, approved applications, voyage fitness or verification.
- Draft replacement links do not supersede the original. The old certificate closes only through the completion step with a traceable supersede link (13.03 §1).
- Each step is recorded as an immutable revision; history stays in **Recorded revision history**.
- Without an explicitly assigned owner the system never defaults to an administrator.

## If something goes wrong
- **No step is available for your role in this state.** The next step belongs to another role; check your permission and appointment and contact the Tenant Admin if needed (13.03 §10).
- **The renewal changed after it was loaded.** or **This is not the current revision.** Press **Reload** and review the current state.
- **The outcome is unknown. Retry sends the same request again.** Use **Retry same request**.
- **Refused for separation of duties: a different person must take this step.** The server refuses a verification by the person who recorded the issued certificate or authored the new certificate record, and a completion by any of them or by the verifier. Completion is also refused while no verification by another person is recorded. The record did not change; reloading does not help. Leave the step to another authorised person, or have the verification recorded first.
- **The step was not recorded. Check the details and try again.** Check the required fields and match selections.
- If the replacement cannot be verified, check the document and source lineage and refer it to the verifier (13.03 §10).

## Related pages
