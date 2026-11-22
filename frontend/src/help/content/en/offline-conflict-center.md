---
key: offline-conflict-center
title: Material conflict center
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [canRead, crew.read, mode.tug, tug.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## What this page is for
The material conflict center lists, for the selected vessel or tug, **material conflicts between your offline changes and the current server record** and lets a person resolve them with a reasoned decision. It answers: "Which of my offline changes clashed with a newer server record, and how, by whom and with which reason will it be resolved?" Nothing here is overwritten or resolved automatically.

## Who uses it
- **Crew / Tug Master / Master / Chief Engineer:** to see and resolve their own conflicting records after working offline. When a conflict appears, do not assume your own record wins automatically (13.03 §6).
- A material conflict needs a human resolution; this page is the target of the "Material conflict → Conflict Center" flow in 13.03 §10.

## Before you start
- The page lists only conflicts you recorded and needs read access to at least one readable source (for example crew assignment, tug operations, daily record, work order evidence, certificate renewal draft).
- The list and decisions need a current server connection. While offline your local changes stay retained on the device.
- Choose the vessel or tug from the **Vessel** list.

## Step by step
1. Choose a vessel from **Vessel**. The table opens with **Source**, **Record**, **Versions**, **Captured**, **Received by server** and **State** columns.
2. If there is a "Not included:" note, sources you have no read permission for are not in the list.
3. On a row in **Open — decision required**, press **Review**.
4. In **Conflict review**, compare the **Before capture**, **Captured** and **Server** values for each field. No choice is preselected.
5. For each value, explicitly choose **Keep server value** or **Use captured value**, fill in **Resolution reason (required)** and press **Submit resolution**.
6. To remove only the local copy on this device, tick the confirmation in **Discard my local change** and press **Discard local change**.
7. Use **Close review** to return to the list.

## What the states mean
- **Open — decision required:** the conflict waits for a human decision.
- **Superseded by a newer conflict:** a newer conflict exists for the same record; review the new one.
- **Resolved by explicit decision:** someone resolved it with a reasoned decision. This is not an operational approval.
- **Withheld / Not provided / Unknown:** the value is hidden by authorization, was not recorded or could not be read. An unknown value is never taken as empty or "No".
- **Server receipt recorded for your explicit decision:** the server received your decision. This receipt is not an operational approval.

## What you cannot do / caution
- Conflicts are never merged or resolved automatically; every value needs an explicit human choice and reason (13.03 §1).
- The center resolves crew assignment conflicts directly. For other sources it shows "Resolve this conflict from the owning record's offline panel."; for example tug records are resolved on **Tug offline operations**.
- Discarding a local change removes only the device copy; the server record is unchanged and the conflict stays recorded until a reasoned decision.
- A change captured on another device must be resolved on that device; nothing is discarded.
- Submission rechecks your access and record versions; it does not grant operational approval.

## If something goes wrong
- **"The submission outcome is unknown" message:** before submitting again, use **Check submission outcome**.
- **"The server record changed after your review" / "The server record changed again" message:** your decision was not applied; review the conflict again.
- **"Material conflicts could not be loaded. Nothing was resolved.":** press **Try again**.
- **"You do not have access to the conflict center." / "Conflict access is unavailable for this vessel.":** check your role and vessel context; contact your tenant administrator if needed (13.03 §10).

## Related pages
