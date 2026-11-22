---
key: pre-survey-checklist
title: Pre-survey checklist
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.survey.plan, certificate.survey.review]
related: [survey-planner, certificate-command-center, renewal-workflow]
screenshots: []
reviewed: false
---
## What this page is for
The Pre-survey checklist tracks the preparation tasks for a certificate's survey with owner, due date, evidence and independent human verification. It answers: "Which preparation items for this survey are open, which have evidence, and which were verified by another person?" Readiness is advisory and never survey approval (13.03 §1, §8).

## Who uses it
- **Master, Chief Engineer / Technical Superintendent:** start a checklist, answer items and attach evidence.
- **Verifier:** compare items answered by someone else with their evidence and verify or reject them (13.03 §1).
- Users with read access only see "Read-only for your role."

## Before you start
- Viewing needs certificate read. Starting and answering a checklist needs survey planning on that vessel; verifying needs survey review.
- Choose the right vessel with **Choose a vessel** (13.03 §1).
- The certificate's survey window should be declared on the [Certificate command center](/app/certificate-command-center).
- The page needs a live read. Offline you see "Offline: a current online read is required."

## Step by step
1. See the vessel's lists under **Checklists**, or on the [Survey planner](/app/survey-planner) press **Open checklist** or **Start checklist** on a row.
2. For a new list **Choose a certificate**, choose a **Template** and press **Create checklist**. The template is a starter template, not an approved catalog.
3. In **Survey plan inputs** fill **Planned start**, **Planned end**, **Planned location (text)** and, if available, **Port call**.
4. In **Items** enter **Owner role**, **Due**, **Answer** and, if needed, **Finding** and **Comment** for each item. Link evidence with **Add evidence**, then press **Save**. Every save creates a new revision.
5. As a verifier, on items answered by someone else choose **Verify** or **Reject** under **Decision**, enter the reason and press **Record verification**.
6. Watch **Readiness** and **Blockers**.

## What the states mean
- Item answer: **Open**, **Ready**, **Needs attention**, **Blocked**, **Not applicable**. An item whose answer is unknown stays **Open**; it never counts as "Not applicable" or "Ready".
- Verification: **Pending**, **Verified (human)**, **Rejected**. An answered item stays **Pending** until verified.
- Readiness: **Blocked**, **Incomplete**, **Prepared, pending human confirmation**. Even the best state waits for human confirmation; the system never says "survey approved".
- **Critical / Required / Optional:** item importance. Unresolved critical and required items count against readiness and appear under **Blockers**.
- **Voyage: unknown (no voyage module):** voyage fit cannot be assessed yet and stays unknown.

## What you cannot do / caution
- You cannot verify an item you answered: "You answered this item; another person must verify it."
- Answering an item **Ready** does not verify it; Completed ≠ Verified. Each verification decision is bound to the exact answer and evidence you see.
- Readiness is not survey approval, a booking or certificate validity. Class, flag and authority coordination is external and is not simulated here.
- Template items are not an approved catalog; an authorised person judges whether they fit the vessel.

## If something goes wrong
- **This checklist changed since you opened it. Reload before editing.** Press **Reload current**.
- **The outcome is not known yet. Retry sends the same request.** Use **Retry the same request**.
- **Items not recorded:** correct the listed items and save again.
- **You do not have access to this view.** Check your role, vessel and object scope; contact the Tenant Admin if needed (13.03 §10).
- If an item cannot be verified, check its evidence and source and refer it to the verifier (13.03 §10).

## Related pages
