---
key: tug-demo-journey
title: Tug demo journey
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [tug-job-intake, tug-assignment, tug-readiness, towage-plan, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## What this page is for
The demo journey is a guided walk through the real tug screens with **synthetic, unsigned and unverified data**. It answers: "Which screens does a tug job pass through from request to history, and which gate does the server enforce at each step?" The demo creates no decision, release or verification; it only links to the real screens and explains them.

## Who uses it
- **Tug operations coordinator / Shore operations:** to learn the workflow and the gate at each step (13.03 §7).
- **Tug Master:** to see where the onboard decision steps (readiness GO, execution) sit end to end (13.03 §3).

## Before you start
- Select the tug; without a selected tug the page shows "Select a tug vessel to continue."
- If you open the page with a job, the step links carry that job.
- Each link opens the real screen; that screen's own authorization rules apply.

## Step by step
1. Follow the steps in order: **Job intake**, **Tug assignment**, **Bollard pull, crew and equipment readiness**, **Towage plan**, **Risk assessment and briefing**, **Master release (GO human record)**, **Execution**, **Evidence and verification**, **Operation history**, **Offline continuity**.
2. At each step, use **Open screen** to go to the real screen.
3. Open **Why** to read the gate the server enforces at that step; for example, the plan is approved by a Tug Master or Master who did not author it, and every participant acknowledges the exact issued revision.

## What the states mean
- This page has no state badge of its own. The meaning of states on the screens you open is described in that screen's guide.
- As the readiness step explains, UNKNOWN stays REVIEW_REQUIRED, never READY.
- GO is a human record bound to the current readiness assessment; the system never creates it.

## What you cannot do / caution
- Demo data is fictional and unverified. Releases, GO decisions and verifications are always recorded by authorised people, never by the demo.
- A result you see on demo screens never replaces a safety decision for a real operation; the decision stays with the Tug Master (13.03 §3).
- A shore assignment never overrides the Tug Master's onboard safety decision (13.03 §7).

## If something goes wrong
- **"This page address is not valid." message:** open the page again from the menu.
- **A step's screen does not open or access is missing:** check your role and tug context for that screen; contact your tenant administrator if needed (13.03 §10).
- **Unexpected readiness result:** open the Why view of the related screen and review it with the control owner (13.03 §10).

## Related pages
