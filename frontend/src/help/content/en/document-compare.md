---
key: document-compare
title: Document revision compare
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## What this page is for
This page shows **two revisions of a controlled document side by side, read-only**, for the selected vessel. It answers: "What differs between these two revisions at file and metadata level, and what is the publication, verification and custody history of each?" The page shows differences; it does not approve, publish or judge changes. It does not put a revision into force and does not make it supersede an earlier one. Evidence originals are never changed silently; when a new revision replaces an earlier one, this is recorded as a "Superseded" relation (13.03 §1).

## Who uses it
- **Master / Chief Engineer / Crew:** to compare the effective revision with the previous one and record a reading acknowledgement (13.03 §2, §5, §6).
- **DPA / Regulatory and assurance user:** to review revision history and verification records (13.03 §4, §8).

## Before you start
- If no vessel is selected, the page says **Choose a vessel in your scope.** and lists the vessels. Select the right vessel.
- You need file read access to see the page. Restricted content and ISPS restricted documents need their own permission as well.
- Loading content and recording an acknowledgement work only online.

## Step by step
1. Open a document from the **Controlled documents** list. Classification and **ISPS restricted** labels are shown on the row.
2. Under **Choose two revisions**, select the **Left (base)** and **Right (reviewed)** revisions. The page first proposes the latest revision on the right and the one before it on the left.
3. Press **Compare**. Use **Swap sides** to switch them.
4. Read the byte statement and the **Metadata comparison** table: **File type**, **Size (bytes)**, **Content SHA-256**, **Intake**, **Publication**, **Verification** and the time fields.
5. Review the **Approval and custody history** on each side.
6. To see the content, press **Load content (recorded access)**. The content is shown with an attribution watermark.
7. If you have read the effective revision, tick **I have read this revision and could access its content.** and press **Record acknowledgement**.

## What the states mean
- **Unpublished / Effective / Superseded:** the revision's publication state. A revision that is not effective shows **Not in operational force**.
- **Unverified / Verified valid / Verified invalid / Verification inconclusive:** the revision's verification record. Unverified does not mean "valid".
- **Same / Changed / Not comparable:** the result of a metadata row. Not comparable does not mean "Same".
- **Byte-identical files:** only identical bytes; this is not a content review.
- **Files differ at byte level:** which rule changed, and its significance, needs a person's review.
- **Content is restricted for your role; metadata only.** the content is not opened for this role.

## What you cannot do / caution
- You cannot publish, approve, put into force or supersede a document from this page.
- The page does not interpret content differences automatically; an authorized person decides their significance.
- An acknowledgement is recorded only for the effective revision and only once. It is not an approval or a verification.
- A printed copy is uncontrolled. Content is not cached and not kept offline.
- While offline, the comparison is stale and new content cannot be loaded.

## If something goes wrong
- **This document is not available to you:** check your vessel context and permission; contact your tenant administrator if needed (13.03 §10).
- **You do not have permission for this action:** check your role and vessel context (13.03 §10).
- **The record changed meanwhile:** use **Reload** to open the latest version.
- **A revision cannot be verified:** check the evidence and provenance history; talk to an authorized verifier (13.03 §10).
- **The request failed:** send the screen, time and steps to support (13.03 §10).

## Related pages
