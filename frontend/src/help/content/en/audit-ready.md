---
key: audit-ready
title: Audit-ready trace
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## What this page is for
The Audit-ready trace page shows the trace chains assigned to you for a vessel, stage by stage: Requirement → control → evidence → verification → decision, with states, timestamps and declared source locators. It answers: "Which requirement does this evidence rest on, who verified it and which decision was recorded?" A recorded chain is a human record, not an approval or readiness statement (13.03 §4, §8).

## Who uses it
- **Regulatory / Assurance user:** checks that no orphan record is left in the Requirement→Control→Evidence→Verification chain (13.03 §8).
- **DPA / HSEQ:** shows the Finding→Action→Verification trail with its evidence when preparing for audit, PSC or inspection (13.03 §4).

## Before you start
- The page needs audit read and human trace read plus a current trace reader appointment for that vessel. The permission alone is not enough; without the appointment no chain is listed.
- Choose the right vessel. The list shows only the chains assigned to you.
- The page loads over a live connection. Offline, the shown data may be stale.

## Step by step
1. Choose a vessel in **Vessel** (**Select a vessel**).
2. Open a chain from **Assigned chains**.
3. In **Trace stages** expand each stage (Requirement, Control, Evidence, Verification, Decision) and review **State**, **Recorded at**, **Revision**, **Content hash** and **Source locator (declared, unverified)**.
4. If **Open items** are listed, pass the codes to the owner of the record concerned.
5. When readable assigned chains exist, compile a pack in **Audit pack** with **Compile new pack**, open it from **Compiled packs** and use **Export JSON** if needed.
6. Press **Retry** in the header to refresh the data.

## What the states mean
- **Recorded chain — human record only, not an approval:** every stage is recorded. This does not declare the vessel or operation ready or compliant.
- **Review required:** at least one stage is missing, unknown or unavailable. An unknown or unreadable input is never shown as complete.
- **Not recorded:** no record exists for that stage; for example no verification or decision has been recorded yet.
- **Unavailable / UNKNOWN:** the record cannot be read now; its content is not assumed.
- **Stale: an input was superseded or expired; review required.** Evidence or a source changed later; review the chain again.
- **Locator not provided:** no source locator was entered; a locator is a declaration, not a verified fact.
- Verified evidence does not mean the action was effective: Verified ≠ Effective (13.03 §4).
- **Audit pack:** "System compilation; not an official record". Items outside your appointments are only counted, not shown; **Superseded (kept for trace)** items are never treated as current.

## What you cannot do / caution
- You cannot edit records, verify or decide on this page; it is read-only. Verification and decisions are recorded by the appointed human on their own pages.
- You cannot see chains not assigned to you; hidden chains are never named, and unavailable assigned chains are shown only as a count.
- Integrity and context hashes are not authority or approval.

## If something goes wrong
- **You are not authorized for this view.** Check your role, vessel and appointment context; contact the Tenant Admin (13.03 §10).
- **No trace chains are assigned to you for this vessel.** Check you selected the right vessel; ask the responsible person if an appointment is needed.
- **The trace could not be loaded.** Press **Retry**.
- **Chain not found or no longer visible to you.** Your appointment may have ended or the record left your scope.
- If a record cannot be verified, check its evidence and provenance and contact the verifier if needed (13.03 §10).

## Related pages
