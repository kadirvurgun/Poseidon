---
key: towage-execution
title: Towage execution
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## What this page is for
The execution console records the **actual execution** of a submitted tug job as a timeline: the start, observations, the **Suspend / Resume / Abort / Complete** actions and the later verification. It answers: "When did the operation start, when was the towline connected and released, which observations were recorded, what state is the operation in and has the record been verified?" Start and transitions are human records of the Tug Master or Master on the executing tug; the console never issues a GO.

## Who uses it
- **Tug Master / Master:** to start the operation and record the Start/Suspend/Resume/Abort/Complete timeline and its evidence (13.03 §3).
- **Verifier (a person with execution verification authority):** to verify a completed or aborted record or reject its verification (13.03 §1).
- **Tug operations coordinator / Shore operations:** to follow evidence completeness and history of completed jobs; they cannot start or abort an operation (13.03 §7).

## Before you start
- Open the tug from **Tug operations** and choose the job from **Select a submitted tug job**.
- To start, all of these must be current: the tug's active assignment, an approved **Towage plan** (the tug must be in the plan), an issued **Towage risk and briefing** acknowledged by every participant, and a **GO** record on the **Operation readiness gate** bound to the current assessment.
- Start and transitions need a server-recorded **Appointment** (Tug Master or Master) for that tug.

## Step by step
1. In the **Start gate** section, read the **Readiness decision** and **Blockers** for each tug.
2. If there are no blockers, read the confirmation text as the appointed person on board, choose your **Appointment**, optionally enter a **Start position** and press **Start operation**.
3. Once the execution is open, follow **State**, **Verification** and the ledger fields (towline connected/released, maximum observed tow load, communication, winch, emergency release test, active minutes).
4. In **Record observation**, choose the observation type (for example towline connected, tow load, communication check, weather, winch state, emergency release test, position, evidence, note), enter the values and press **Record observation**.
5. In **Tug Master actions**, choose the **Action** (Suspend, Resume, Abort, Complete) and enter your **Appointment** and a **Reason**. Abort and complete need a reason of at least 20 characters and open the **End position** field; completion is not accepted without an end position.
6. If **Completion blockers** are listed, clear them; for example, if the towline is still connected, record its release first.
7. As verifier, write a reason and press **Verify record** or **Reject verification**.
8. See every event in order in the **Timeline**.

## What the states mean
- **Active / Suspended / Completed / Aborted:** the operation state. Completed does not mean the record is verified.
- **Pending / Verified / Rejected:** the verification state. Completed ≠ Verified; verification is a separate person's record.
- **Good / Lost / Pass / Fail / Conditional / Unknown:** the result of communication, winch and emergency release observations. Unknown shows nothing was recorded; it never stands in for "Good" or "Pass".
- **Safety events — Unknown:** this console does not count safety events; no "no events" conclusion is drawn.
- **GO (human record) / NO GO (human record):** the decision recorded by the Tug Master or Master on the readiness gate.

## What you cannot do / caution
- Shore users, AI and system identities cannot start or abort an operation. A shore assignment never overrides the Tug Master's onboard safety decision (13.03 §7).
- Only the appointed Tug Master or Master may suspend, resume, abort or complete. Abort is always available while the operation runs.
- The person who started or ended the operation cannot verify the same record; verification happens after the operation has ended.
- The ledger is append-only; recorded events are never deleted or changed.
- No command is sent while offline. Observations can be queued on **Tug offline operations**; start, transitions and verification stay online.

## If something goes wrong
- **"The server listed missing or blocking items." message:** clear the codes in **Blockers** or **Completion blockers** (for example plan not approved, briefing acknowledgement pending, GO record missing or not current).
- **"The record changed or the command conflicts." message:** use **Reload latest** to review the record.
- **"The outcome is unknown." message:** **Retry** sends the identical command again.
- **No authorization:** check your role, tug and appointment context; contact your tenant administrator if needed (13.03 §10).

## Related pages
