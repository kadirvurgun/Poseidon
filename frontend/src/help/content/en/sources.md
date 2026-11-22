---
key: sources
title: Source drafts
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## What this page is for
The Source drafts page keeps the regulatory and rule sources your organisation relies on (convention, flag, class, company or local rule) as **versioned draft records**. It answers: "Which revision of which official source does this requirement rest on?" The notice at the top applies: "Tenant-local DRAFT · Declared and unverified. Dates and hashes confer no legal authority. Source references are stored as text." (13.03 §8).

## Who uses it
- **Regulatory / Assurance user:** registers the official, versioned source and states its effective date and lifecycle (13.03 §8).
- **DPA / HSEQ:** reads it to see which source revision an audit or finding refers to (13.03 §4).

## Before you start
- Viewing needs tenant-wide read and regulatory draft read. Creating and saving drafts also needs regulatory draft write; without it the **New draft** button is not shown.
- Have the official text of the source and its version/amendment details at hand. POSEIDON does not download or verify the source text; you declare it.
- Saving needs a live server connection. Offline, your edits are kept in the tab but not saved.

## Step by step
1. See the recorded source drafts in the list. To search, type in **Search drafts** and press **Search**. Move between pages with **Previous** and **Next**.
2. For a new source press **New draft**.
3. In **Draft fields** choose a **State** for each field first: **Missing**, **Unknown** or **Known (declared)**. You can enter a value only after choosing **Known (declared)**. The fields are **Title**, **Declared authority**, **Source reference (text only)**, **Version / amendment declaration**, **Declared effective date**, **Declaration / provenance** and **Source observed at (with UTC offset)**.
4. Press **Save draft**. On success you see "Draft saved. Unverified."; open the record with **Open saved draft and history**.
5. Open an existing draft from the list, change it and save again. Each save creates a new **Revision**; older revisions stay in **Immutable history** with their content and **Integrity hash (not authority)**.
6. To move to applicability rules or the control-evidence chain use **Lifecycle and applicability** or **Controls and evidence** above the list. Press **Back to register** to return.

## What the states mean
- **DRAFT:** the stored state shown in the list and the record header. Every source record on this page is a draft; it is not an official adoption or an in-force decision.
- **Missing:** no information has been entered for the field yet. Later steps (for example an applicability evaluation) are blocked while required data is missing.
- **Unknown:** the information was sought but is not known. The system never reads Unknown as "no" or "not applicable"; when critical data is unknown the result stays in review (REVIEW_REQUIRED).
- **Known (declared):** you declared the value. A declaration is not verified information.
- **Integrity hash (not authority):** shows that a revision's content has not changed; it is not proof of legal validity.

## What you cannot do / caution
- You cannot change a saved revision. When the source changes, save a new revision; requirements stay bound to the older revision and history is kept (13.03 §1).
- Do not assume class, flag, company and local rule sources stand in for each other; record each as its own source (13.03 §8).
- This page does not officially admit a source and gives no one a compliance, permit or readiness result. Source admission and independent review are human authority.
- Effective dates and hashes confer no legal authority.

## If something goes wrong
- **Permission denied.** Check your role and tenant scope; ask the Tenant Admin if needed (13.03 §10).
- **Conflict. Your edits are retained. Reload only after discarding them.** Someone changed the record before you. Open the current revision with **Reload saved draft** and make your change again.
- **Check field states, bounds, dates and source revision.** A field value or date format is invalid.
- **Service unavailable. Your draft is retained.** When the connection returns, resend the same command with **Retry frozen command**.
- **Sign in to continue; retain your draft.** Your session ended; use the **Sign in** link.
- If an application error persists, report the correlation ID, screen, time and steps to support (13.03 §10).

## Related pages
