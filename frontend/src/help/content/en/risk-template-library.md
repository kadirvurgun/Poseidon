---
key: risk-template-library
title: Risk template library
roles: [DPA / HSEQ, Master, Tug Master, Crew / OOW / Engineer]
permissions: [hse.risk_template.read, hse.risk_template.write, hse.risk_template.publish, hse.risk_template.suggest, hse.risk_method.write, hse.risk_method.publish]
related: [risk-assessment, high-risk-work]
screenshots: []
reviewed: false
---
## What this page is for
The Risk template library manages, with revisions, the risk assessment templates used across the tenant (work type, hazards, default controls, required barriers, SMS and requirement sources) and the risk methods (matrix). It answers: "Which published template applies to this work type, and which revision is in force?" The page is a **development target only**: a publication here is a recorded HSEQ human record, not a permit, an expert risk acceptance or an HSE gate pass (13.03 §4).

## Who uses it
- **DPA / HSEQ:** as part of risk governance, prepare, review and publish template revisions, barriers and the risk matrix (13.03 §4).
- **Master, Tug Master and crew:** read published templates and send **Crew suggestions** for a template.

## Before you start
- Reading needs the template read permission. Writing and publishing templates need tenant-wide scope and the template write or publish permission. Sending a suggestion needs only the template suggest permission.
- Creating and publishing a risk method are separate permissions and need tenant-wide scope.
- Risk commands need a current online connection; nothing is queued or approved offline.

## Step by step
1. See templates in the list. Each row shows the state, the **Current** revision and the **Suggestions** count. Open a template.
2. For a new template press **New template**. Fill in **Template code**, **Title**, **Work type**, **Vessel / tug scope**, **Approval owner**, **Change reason**, **Required barriers**, **SMS / requirement sources** and **Hazards**, then press **Save draft**.
3. On a draft revision press **Submit for review**. Missing items are listed as blocking items.
4. A publisher enters **Effective from** and a **Reason** of at least 10 characters and presses **Publish (HSEQ human record)** or **Return**.
5. To change a published template write a reason and press **Start new revision**; the older revision does not change.
6. Crew write a suggestion in **Send suggestion** and send it. An authorised person reviews it with **Accept** or **Reject** and a reason.
7. In **Risk methods (matrix)** use **New method** to enter **Method code**, **Likelihood codes**, **Severity codes** and **Matrix cells**; another person publishes it with **Publish method**.

## What the states mean
- **Draft / In review / Returned / Published / Superseded:** the template revision lifecycle.
- **Current / Scheduled / Not effective:** the effective view. The latest published revision whose effective date has come is **Current**; one with a future date is **Scheduled**. A future revision never supersedes the older one early.
- **Unknown:** the field is not filled in; an empty value is never filled with a default.
- **No published risk method: ratings stay not assessed.** The system offers no default matrix or threshold.

## What you cannot do / caution
- An author of a revision or the person who submitted it cannot publish the same revision; publication is a separate human record (separation of duties).
- Publication is not a permit, an expert risk acceptance or an approved production catalog.
- A template is applied to a risk assessment only as a **copy**; it carries no rating, signature, participant confirmation, approval or acknowledgement. When a template changes, existing assessments do not change by themselves.
- A published revision is never edited; start a new revision to change it.

## If something goes wrong
- **Blocked until the listed items are completed.** Complete the blocking items and submit again.
- **This record changed elsewhere.** Press **Reload** to see the current revision.
- **You are not permitted to take this action (separation of duties or scope).** Publication must be done by another authorised person who did not author the revision; if a permission is missing contact the tenant administrator (13.03 §10).
- **This action no longer applies to the current state. Reload.** The revision state has changed.

## Related pages
