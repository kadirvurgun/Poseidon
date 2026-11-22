---
key: port-activity-sof
title: Port activity and SOF
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## What this page is for
The port activity and SOF page keeps the selected ship's **port events** as an operational record with evidence references, and lets an appointed person finalize the **Statement of Facts (SOF)** internally. It answers: "Which port events did we declare, in which UTC order and interval, with which cargo, draught, pilot, tug, delay and protest references, and has an appointed person finalized this SOF revision?" The page produces no signature, laytime or demurrage, and recorded events are not instructions or approved planned windows.

## Who uses it
- **Master:** to keep arrival and port activity as an operational record with evidence and to finalize the SOF (13.03 §2).
- **Chief Officer / Chief Engineer:** to record port events and, with a current appointment, to finalize the SOF.

## Before you start
- In the top context bar, select the **right company and ship**. If no ship is selected, the page shows "Select a ship".
- Reading needs `operations.port.read`; saving drafts needs `operations.port.write` and edit rights on this ship.
- Finalizing needs `operations.port.write`, edit rights on this ship and `operations.port.finalize`, plus a current finalization appointment (Master, Chief Officer or Chief Engineer) for this ship.

## Step by step
1. Pick the ship. **Recorded port event timeline** lists the returned events in the server's UTC order. **Filter returned page** narrows this page only.
2. Read **Statement of Facts timeline** below the list: each event with **Recorded interval**, **Delay reason** and **Remarks**.
3. Press **New draft record**. Fill in **Port call context**, **Recorded activity and timing**, **Cargo and draught declarations** and **Support, exceptions and evidence**. Event times need Z or an explicit UTC offset.
4. Press **Save unverified draft**. Each save adds a revision to **Recorded revision history**.
5. On a saved record, open **Statement of Facts finalization**. If **Required facts missing** is shown, complete those fields first.
6. Tick each listed unresolved reference to acknowledge it, choose your **Appointment** and press **Finalize SOF**. The system records who finalized, when, the duty and the reason.

## What the states mean
- **DRAFT:** the SOF is not finalized.
- **FINAL (internal):** an appointed person finalized this revision. It is internal: not a signature and not an external agreement.
- **Signature not recorded:** always shown; no signature exists on this page.
- **Amended after FINAL: a new review and finalization are required.:** the record was saved again after finalization and is back to DRAFT.
- **UNKNOWN (unresolved references):** no owning lookup exists for these required references. Acknowledging them does not make them known.
- **Review required / Unverified draft record:** port events are declarations; nothing is verified.

## What you cannot do / caution
- The system never finalizes the SOF. Finalization is a human act by an appointed Master, Chief Officer or Chief Engineer (13.03 §2).
- Finalize SOF stays disabled while required facts are missing, no appointment exists or unresolved references are not acknowledged.
- Finalization is not a signature, not an external agreement and not laytime or demurrage.
- The finalization state is shown only in **Statement of Facts finalization** (DRAFT / FINAL (internal)). The "SOF review state" field, the timeline note and the page footer do not reflect finalization and may say finalization is unavailable.
- Saving drafts and finalizing need a current online session; this page has no offline capture.

## If something goes wrong
- **"No current finalization appointment for this vessel.":** check your role and ship context with your tenant administrator (13.03 §10).
- **"Outcome unknown: retry sends the same command once.":** use **Retry**; do not start a new finalization.
- **"Command not accepted":** read the code shown, reload the record with **Read current saved record** and try again.
- **"The saved source changed.":** explicitly reload or discard before saving again.

## Related pages
