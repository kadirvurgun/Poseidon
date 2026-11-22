---
key: tug-operations-offline
title: Tug offline operations
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## What this page is for
This page keeps a **prefetched snapshot** of a tug job on the device and puts three kinds of captures made without a connection into a **durable, encrypted queue**: briefing acknowledgements, execution observations and daily-log entries. It answers: "What can I safely capture without a connection, what is waiting in the queue and how did the server take it?" Master release, readiness GO, execution start/transitions, verification and reviews stay online human records.

## Who uses it
- **Tug Master:** to continue critical work where the connection is weak and follow the sync state (13.03 §10).
- **Crew:** to make briefing acknowledgements and watch entries offline, and to resolve conflicts without assuming their own record wins automatically (13.03 §6).

## Before you start
- Open the page from a tug job; without a job the page shows "Open this screen from a tug job to prefetch its snapshot."
- The **Offline policy** line must show a configured storage policy (`CONFIGURED_UNVERIFIED`); without a policy, snapshot prefetch and offline capture are unavailable.
- Prefetch the snapshot **while online**. Before capturing, check that the snapshot lease has not expired.

## Step by step
1. While online, press **Prefetch job snapshot**. The message "Snapshot sealed on this device." appears.
2. In **Job snapshot**, check the job number, its state and **Lease until**.
3. If you are a participant of an issued briefing, choose **Acknowledge** or **Dispute** (with a reason of at least 10 characters) in **Acknowledge briefing** and press **Seal and queue**.
4. For a running execution, choose the execution and observation type (Note, Towline connected, Towline released) in **Record execution observation**, write the note and press **Seal and queue**.
5. For a draft or returned daily log, choose the log in **Daily-log entry**, write the **Day summary** and press **Seal and queue**.
6. Follow each capture's state in **Queued captures**. Captures are sent in order when the connection returns; to send manually use **Send queued captures**.
7. If a capture shows **Material conflict**, read **Changed on the server**, explicitly choose **Keep the server record** or **Apply my captured value on the current record** and press **Resolve conflict**.

## What the states mean
- **Queued on device (not sent):** the capture is sealed on this device; queued is not sent.
- **Accepted by server:** the server received the capture. This is not an approval or verification.
- **Material conflict:** the server record changed after the capture; it is never merged automatically and waits for a person's decision.
- **Server value kept / Refused by server:** the server's outcome for the capture.
- **UNKNOWN (offline):** the policy state could not be read while offline; no policy is assumed.

## What you cannot do / caution
- Online only: Master release, readiness GO, execution start and transitions, briefing issue, verification, daily-log submit and review.
- Material conflicts are never merged automatically; resolution is always an explicit human choice (13.03 §1).
- No new capture can be made with an expired snapshot lease; prefetch a new snapshot.
- On replay each capture is processed once with its original id; do not create the same capture again.

## If something goes wrong
- **"The device key for this queue is unavailable." message:** captured records are retained but cannot be read; do not delete them and contact support.
- **"Outcome unknown" message:** the same capture will be retried with its original id; do not create a new one.
- **"The offline policy does not allow a snapshot." message:** contact your tenant administrator about the policy configuration (13.03 §10).
- **Material conflict:** use the **Resolve conflict** form on this page; conflicts are also listed in the **Material conflict center** (13.03 §10).

## Related pages
