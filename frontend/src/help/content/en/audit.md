---
key: audit
title: Action history
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: []
reviewed: false
---
## What this page is for
The Action history page shows a **read-only**, attributed record of changes to business records in your authorised scope. It answers: "When did this record change, with which action, and under which actor and role context?" Security events are restricted separately and do not appear here (13.03 §4, §9).

## Who uses it
- **DPA / HSEQ:** trace finding, action and verification records (13.03 §4).
- **Regulatory / Assurance user:** check record history before an audit or review (13.03 §8).
- **Tenant Admin:** audit membership and permission changes and delegation start and end times (13.03 §9).

## Before you start
- Viewing needs audit read. Without it you see "Audit permission is required."
- Only records in the tenant and vessel scope you are authorised for are listed.
- If you are looking for a specific record, have its **Vessel ID** or **Object ID** ready. History links on other pages open this page already filtered.

## Step by step
1. Fill the filters: **Vessel ID**, **Object ID**, **Object type** and **Action** (**All**, **Created**, **Updated**, **Deleted**).
2. Press **Filter**. Use **Clear filters** to reset.
3. Review the **Time**, **Object** and **Action** columns in **Authorized records**. Move between pages with **First page** and **Next**.
4. Press the object link on a row to open **Record details**. Read **Actor reference**, **Assigned role references**, **Trusted capabilities**, **Reason**, **Metadata**, **Resource version** and **Correlation**.
5. If immutable revision evidence exists, press **Open exact immutable revision** to see the record as it was. Press **Close** to close the details.

## What the states mean
- **Created / Updated / Deleted:** the kind of action on the record.
- **Recorded:** the metadata was captured completely.
- **Historical metadata incomplete / Historical identifier unavailable / Historical context unavailable:** older records did not keep this information. Missing information is never filled in by guessing.
- **Values withheld:** the field value is hidden for privacy; only the fact that it changed is shown.
- **Immutable revision evidence is unavailable for this record.** There is no revision snapshot for this record type.

## What you cannot do / caution
- The page is read-only. An action history entry cannot be edited or deleted.
- A record appearing in the history does not mean the work was verified or effective; Verified ≠ Effective (13.03 §4).
- This page records business changes. On its own it is not complete user-action auditing or proof of privacy compliance; security events are kept separately.
- The list shows only the scope you are authorised for. Records outside your scope are not listed; not seeing a record does not mean it was never changed.

## If something goes wrong
- **Audit permission is required.** Check your role and scope; contact the Tenant Admin if needed (13.03 §10).
- **Sign in to continue.** Sign in again.
- **History is unavailable. Check filters and retry.** Check that the identifiers are complete and correct, then press **Retry**.
- **No authorized records match these filters.** Widen the filters.
- If an application error persists, report it to support with the correlation ID, screen, time and steps (13.03 §10).

## Related pages
