---
key: tug-crew-readiness
title: Tug crew competence
roles: [Tug Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, crew.read, human.review.read, applicability.read]
related: [tug-readiness, tug-assignment, tug-equipment-readiness]
screenshots: []
reviewed: false
---
## What this page is for
This page shows, read-only, the **crew and towage competence** checks for a tug assignment's planned time window. It answers: "For this assignment's crew, what do the minimum safe manning (MSMD), credential and competence, and work/rest checks say, and which source do they come from?" The result is advice only and feeds the readiness gate; it is not an eligibility approval or a GO decision.

## Who uses it
- **Tug Master:** to assess MSMD, credentials, competence, familiarization and work/rest together for crew readiness (13.03 §3).
- **Tug operations coordinator / Shore operations:** to follow the crew blockers of an assignment (13.03 §7).

## Before you start
- Open the page from an assignment: on **Assignment and conflicts**, use the **Crew and towage competence** link under a proposal. Without a selected assignment the page shows "Select an assignment from the job's assignment list."
- Besides tug read access you need crew, human review and applicability read access; otherwise the protected crew source cannot be read.
- The assignment needs a valid current or future time window; otherwise the result requires review.

## Step by step
1. Read the overall result on the **State** line.
2. On the **Crew eligibility evaluator** line, see whether the evaluator is connected and its source.
3. In the checks table, read the **Category**, **Result** and **Reason code** of each row.
4. If there is a blocker or review reason, pass the crew record to its owner; then return to **Operation readiness gate**.

## What the states mean
- **Ready / Conditional / Not ready / Review required:** the advisory assessment. Ready is not a GO decision.
- **Not connected — all tug-specific criteria stay under review.:** the evaluator is not connected; tug-specific criteria stay review required and are not counted as ready.
- **Pass / Fail / Unknown:** the result of a single check. A critical gap is never shown green.
- **Unknown:** the source is missing or could not be read. An unknown check needs review; it never stands in for "Ready" or "No".

## What you cannot do / caution
- No crew record is changed and no eligibility is approved on this page.
- The result is advice; the operational decision stays with the Tug Master and a shore assignment never overrides it (13.03 §3, §7).
- Do not change someone else's verified record; corrections belong to the record owner (13.03 §6).

## If something goes wrong
- **"You do not have access to this record.":** check your role, tug and object context; contact your tenant administrator if needed (13.03 §10).
- **"The service is unavailable.":** press **Retry**.
- **Unexpected readiness:** note the reason codes and review them with the control owner or subject-matter expert (13.03 §10).
- **Record cannot be verified:** check the credential and evidence source; contact the verifier (13.03 §10).

## Related pages
