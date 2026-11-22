---
key: industry-assurance
title: Industry assurance (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## What this page is for
The Industry assurance page imports SIRE 2.0 inspection results for a tanker from a workbook, puts the changes up for human review, links findings to safety actions and records field evidence. It answers: "Which findings of this inspection are open, which have an action linked, and where is the evidence?" Imports are drafts: a different person accepts every changed item. Findings stay open and unverified; this page never closes or verifies them. The chain is Finding → Action → Verification, and verification and effectiveness are separate human records on the action in Safety events & near misses (13.03 §4, §8).

## Who uses it
- **DPA / HSEQ:** link inspection findings to actions and follow the Finding → Action → Verification chain (13.03 §4).
- **Regulatory / Assurance user:** manage column mappings and stage or review imports (13.03 §8).
- **Master and ship staff:** record field evidence (photos) for a finding.

## Before you start
- Choose a vessel. SIRE 2.0 applies to tankers only. If the ship type is not declared in the vessel profile, imports stay **review required**.
- Reading needs the inspection read permission. Importing, accepting an import, writing evidence, linking findings and writing mappings are separate permissions. Linking an action also needs the safety event read permission; recording evidence also needs file read.
- The column schema is synthetic; the licensed OCIMF format is not used.
- Offline only photo captures are queued on this device; imports and decisions need a connection.

## Step by step
1. **Choose a vessel** and check the **Applicability** line.
2. If needed, in **Column mappings** enter a **Mapping key**, write one line per column (header=field) and press **Save as new version**.
3. In **Stage an import** choose the **Mapping version**, **Inspection reference**, **Inspection date** and **Workbook (.xlsx)**, then press **Stage for review**.
4. Another person, in **Import review**, chooses **Accept** or **Keep current** for each changed item, writes a **Reason** of at least 10 characters and presses **Accept import** or **Reject import**.
5. Open an inspection from the **Inspections** list. Review **Items** and **Findings**.
6. To link a finding to an action enter the **Safety action id** and press **Link action**.
7. For field evidence enter **Media**, **Clean file id**, **File revision** and **File checksum (SHA-256)** and press **Record field evidence**. Offline the button becomes **Save on this device**; when online send it with **Send now**.

## What the states mean
- **Tanker: applicable / Not applicable / Ship type unknown: review required:** with an unknown ship type the result stays **Review required**; it is never assumed not applicable.
- **Awaiting review / Accepted / Rejected:** the import state.
- **New / Changed / Unchanged / Not in this import:** the item compared with the previously accepted import. An item not in this import is **Kept (never deleted)**.
- **Negative observation / Positive observation / No observation / Unknown:** the observation on an item.
- **Open / Unverified:** findings here always show as open and unverified.
- **No action linked / Action linked:** whether the finding is linked to a safety action. A linked action does not mean the finding is verified or the action is effective.
- **Captured online / Captured offline:** how the evidence was captured.

## What you cannot do / caution
- The person who staged an import cannot review it: "You staged this import; someone else must review it."
- This page does not close or verify findings. Verified is not Effective; effectiveness is a separate human review on the action.
- Video evidence is unavailable until the operator enables a scanned video file type.
- An item missing from a later import is never deleted; history is kept.

## If something goes wrong
- **The request was refused:** resolve the reasons listed below it.
- **The record changed meanwhile. Reload and review again.** Press **Reload**.
- **The outcome is unknown (connection lost).** Use **Retry the same request**.
- **You are not authorised to read industry assurance records.** or **You are not allowed to do this.** Check your role and vessel context; if needed contact the tenant administrator (13.03 §10).
- If evidence cannot be verified, check the file id, revision and SHA-256 checksum; if needed contact the verifier (13.03 §10).

## Related pages
