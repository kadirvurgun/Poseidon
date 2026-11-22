---
key: fuel-bunker-log
title: Fuel, bunker and ROB
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## What this page is for
The fuel, bunker and ROB page keeps the selected ship's **fuel and bunker declarations** as unverified drafts with their source history. It answers: "What fuel was received or consumed, what ROB did we declare before and after, which BDN, supplier, sample and tank references belong to it, and does the declared ROB chain continue from one record to the next?" The page does not correct quantities, does not calculate emissions and does not establish compliance or verification.

## Who uses it
- **Chief Engineer / engine officers:** to record bunkering, consumption and ROB declarations with their evidence references (13.03 §5).
- **Master:** to follow fuel declarations next to the Master-reviewed noon summaries (13.03 §2).

## Before you start
- In the top context bar, select the **right company and ship**. If no ship is selected, the page shows "Select a ship".
- Reading needs `operations.fuel.read`. Creating and saving needs `operations.fuel.write` and edit rights on this ship.
- Times must include date, seconds and Z or an explicit UTC offset; a local time without a timezone is not accepted.

## Step by step
1. Pick the ship. **Fuel and ROB records** lists the returned records. Use **Filter returned page** to narrow this page only.
2. Read **ROB continuity review** below the list. It shows the balance (before + received − consumed − after), **Continuity with previous record** and **Review flags** per declared fuel type.
3. Read **Master-reviewed Noon fuel and ROB summaries** as context. They are shown as declared text and are not copied into the ledger.
4. Press **New draft record**. Fill in **Record and delivery context**, **Quantities and ROB declarations** and **Fuel properties and sample record**. For each field keep a declared value, **Missing** or **Unknown**.
5. Press **Save unverified draft**. Each save adds a revision to **Recorded revision history**.
6. To start from a saved noon report or engine log, use **Open fuel record from saved source** on that page. The **Linked daily declaration** stays read-only here.

## What the states mean
- **Draft declaration / Unverified draft record:** a person declared the values; nothing is verified.
- **Review required:** the compliance review state of every record; compliance has not been assessed.
- **Pending; verification not established:** the verification review state; no verification exists.
- **No discrepancy detected:** the declared numbers add up. This is not verification.
- **Unknown:** continuity cannot be judged, for example when a value is missing or older history is outside the assessed window. Unknown does not mean "No discrepancy" or "Ready".
- **Missing / Unknown:** separate from zero.

## What you cannot do / caution
- Human review, operational or legal approval, finalization and offline capture are not available on this page.
- The system flags differences for human review; it never corrects a quantity or decides fuel compliance.
- Only the newest 100 records are assessed. Without a tank reference, a gap on a multi-tank vessel is a question about missing tank data, not a data error.
- Noon or engine log quantities with an unresolved unit are never converted into received, consumed or ROB values.

## If something goes wrong
- **"The saved source changed.":** your inputs are kept; explicitly reload or discard before saving again.
- **"Outcome unknown.":** use **Retry exact operational command**; do not retype.
- **"Check the declared values, explicit time offsets and current source permission.":** correct the marked field or keep it Missing or Unknown.
- **Operational source or access unavailable:** use **Retry source read**; if access stays denied, check your role and ship context with your tenant administrator (13.03 §10).

## Related pages
