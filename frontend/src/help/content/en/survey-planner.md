---
key: survey-planner
title: Survey planner
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read]
related: [pre-survey-checklist, certificate-command-center, renewal-workflow, drydock-planning]
screenshots: []
reviewed: false
---
## What this page is for
The Survey planner shows declared survey windows on a fleet calendar together with voyage and port fit. It answers: "In the range I chose, which certificates on which vessels have a survey window opening or passed, and is there a preparation checklist?" It is a planning aid only: not an authority schedule, a booking or a survey result (13.03 §8).

## Who uses it
- **Master, Chief Engineer / Technical Superintendent:** see upcoming surveys and start the preparation checklist in time (13.03 §5).
- **Regulatory / Assurance user:** watch declared windows and missing declarations across the fleet (13.03 §8).

## Before you start
- Viewing needs certificate read. Only vessels you can access are listed.
- Windows come from declarations on the certificate record. If no window is declared, the row appears under **Window not declared**; enter the dates from the source document on the [Certificate command center](/app/certificate-command-center).
- The page needs a live read. Offline you see "Offline: a current online read is required."

## Step by step
1. In **Calendar range** choose **From** and **To** and press **Show**.
2. Read the open, upcoming, overdue and unknown counts in the summary line.
3. On each row review **Certificate**, **Issuer**, **Declared flag**, **Regulatory basis**, **Declared expiry**, **Survey window** and **Voyage/port fit**.
4. Press **Open certificate** to see the certificate.
5. Use **Open checklist** if a preparation list exists, or **Start checklist** if not, to go to the [Pre-survey checklist](/app/pre-survey-checklist).

## What the states mean
- **Upcoming / Window open:** position relative to the declared window.
- **Window passed, satisfaction not recorded:** the window has ended and no record shows the survey was done. It does not prove the survey was missed either; check the records.
- **Window unknown:** the date is missing or unreadable. An unknown window never means "no issue"; it needs review.
- Voyage/port fit: **Conditional**, **Not fit**, **Unknown**. The page never gives a "fit" approval.
- Checklist readiness: **Blocked**, **Incomplete**, **Prepared, pending human confirmation**.

## What you cannot do / caution
- The planner books nothing and files nothing with an authority. Class, flag and authority coordination is external and is not simulated here.
- What the calendar shows is not a survey result or certificate validity. Voyage and survey decisions belong to authorised people.
- The page is read-only; changes are made on the certificate and checklist pages.

## If something goes wrong
- **Only part of the fleet is shown; narrow the range.** Choose a shorter calendar range.
- **No certificates with survey windows in this range.** Widen the range or check the certificate declarations.
- **Could not load. Try again.** Press **Retry**. If it persists, report it to support with the screen, time and steps (13.03 §10).
- **You do not have access to this view.** Check your role and vessel scope; contact the Tenant Admin if needed (13.03 §10).

## Related pages
