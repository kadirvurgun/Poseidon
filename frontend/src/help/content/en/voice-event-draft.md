---
key: voice-event-draft
title: Voice event draft
roles: [Crew / OOW / Engineer, Master, DPA / HSEQ]
permissions: [voice.draft.read, voice.draft.write, hse.safety_event.report]
related: [safety-events]
screenshots: []
reviewed: false
---
## What this page is for
The Voice event draft page turns a short spoken or typed note into **draft suggestions** for a safety event report. It answers: "Were the event type, location, time, what happened and the immediate action correctly taken from what I said or wrote?" Suggestions come from fixed local rules, each traced to your own words with a confidence. A person edits and confirms the draft; nothing is submitted, closed or reported automatically (13.03 §1, §6).

## Who uses it
- **Crew / OOW / Engineer and Master:** take a quick note in the field and start a safety event report (13.03 §2, §6).
- **DPA / HSEQ:** watch **Measured usability (this vessel)**. These figures are an operational observation, not an accuracy claim.

## Before you start
- Choose a vessel in your scope. Creating a draft needs the voice draft write permission and edit scope on that vessel.
- Handing a confirmed draft over to a safety event draft also needs the safety event report permission.
- Privacy: audio is not stored. Speech uses only a local engine on this installation; if none is installed, type or paste the note. Avoid names and personal health details.
- While offline nothing is sent.

## Step by step
1. Press **New voice or typed note**.
2. If a local engine is configured press **Record with local engine**, then **Stop and transcribe**. Otherwise type a short note in **Note transcript**: what, where, when, what you did.
3. Check **Date of the note**. It is used only to resolve words like "today" or "yesterday".
4. Press **Create draft suggestions**.
5. Check every field: **Event type**, **When it happened (local time)**, **Location on board**, **Location detail**, **What happened**, **Immediate action taken**. Under each field you see the suggestion's confidence, rule and the matching part of your note. Read **Notes from the rules** as well.
6. Correct wrong suggestions and press **Save edits**. When **Still required before confirmation:** is empty press **Confirm draft**. Confirming records that you checked every field; it does not submit or close an event.
7. After confirmation, if a safety event draft was created, follow **Open the safety event draft** to the Safety events & near misses page and complete and submit it there. If none was created yet and you hold the permission, use **Create the safety event draft now**.
8. If the note is not an event or is wrong, choose a **Reason**, add **Note (required for Other)** if needed and press **Reject draft**.

## What the states mean
- **Draft (unconfirmed):** no person has confirmed the suggestions yet.
- **Confirmed by a person:** the fields were checked; this is not a safety event report and nothing was submitted.
- **Rejected:** the rejection reason is recorded for evaluation.
- **Suggested, unchanged / Changed by you / Entered by you / Empty:** the origin of a field. Suggested, unchanged is a suggestion a person still has to check.
- **Typed or pasted / Local speech engine:** how the note was entered. Editing text from the local engine turns it into a typed note.
- Rule notes such as **Time mentioned without a day; the date was not assumed** show missing information; the system does not guess the missing value.
- A note such as **2 missing**: how many required fields of the draft are still empty in the list.

## What you cannot do / caution
- Suggestions are only a draft; a confidence is not a guarantee of correctness. Check every field yourself before confirming.
- This page does not submit, close or report a safety event. You submit the report on the Safety events & near misses page.
- If evidence is mentioned, attach it yourself; the page attaches no evidence.
- **Confirm draft** is not available while edits are unsaved; save them first.
- Without the safety event report permission no safety event draft is created from a confirmed draft.

## If something goes wrong
- **Speech capability unavailable: no local engine is configured.** or **Transcription was not completed.** Type or paste the note.
- **Microphone access was not granted.** Check the browser permission or type the note.
- **The record changed meanwhile.** Press **Reload** and redo your edit.
- **You are offline. Nothing was sent.** When online use **Retry the same command**.
- **You do not have permission for this action.** Check your role and vessel context; if needed contact the tenant administrator (13.03 §10).

## Related pages
