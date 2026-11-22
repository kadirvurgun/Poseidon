---
key: tug-command
title: Tug operations
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## What this page is for
Shown in the menu as **Tug command**, this page shows the **source context** of the selected tug. It answers: "Which role, type, bollard pull (BP) and equipment declarations are recorded for this tug, where do they come from and how current are they?" The page shows declarations and their sources only; it gives no operational readiness, eligibility, permit or safety approval. The badge at the top says so: "Source declarations · operational readiness unknown".

## Who uses it
- **Tug Master:** to confirm the role, BP and equipment context of their own tug before work starts (13.03 §3).
- **Tug operations coordinator / Shore operations:** to make sure they are in the right tug context before moving to the job board and job intake (13.03 §7).
- **Crew:** to see their role lens and the Ship Mode screens open to them (13.03 §6).

## Before you start
- You need Tug mode and tug read access. Without it the page shows "Access is not available."
- If no context is selected, the page lists the authorized tugs under **Select tug**; open one.
- The equipment list also needs equipment read access; without it the equipment source shows as "unavailable or not granted".

## Step by step
1. Open the tug from the **Select tug** list. The **Tug context** line under the heading shows the tug code, name and mode.
2. In **Role (server assignment)** and **Tug-mode menu lens**, read the role and menu lens the server assigned to you.
3. In **Mode visibility**, see whether Ship Mode screens are open. The **Ship Mode screens** section links only to screens granted to you.
4. Check from **Tug type**, **Type source / revision** and **Type source observation / policy** which source and revision the type comes from.
5. Read **Profile BP (unverified)**, **BP source / version / provenance** and **BP freshness / observation**. This is a declared value, not a measured one.
6. In **Equipment context**, see the equipment declarations, their levels, revisions and provenance.
7. Use the page tabs to move to **Job board** or (with write access) **Job intake**, or press **New request**.

## What the states mean
- **Unknown — no role assignment:** the server has no role assignment for you on this tug. This is not "no role, no problem"; an administrator assignment is needed.
- **Role labels unavailable / Menu lens unavailable:** the source cannot be read now. Missing information is not taken as empty.
- **No lens configured for Tug mode:** no menu lens is defined for you in this mode.
- **Hidden in Tug mode. A cross-mode role is required.:** Ship Mode screens need an explicitly authorized cross-mode role.
- **Profile BP (unverified):** the declared bollard pull. It is not a verified test result.
- **No equipment declarations returned. Verified operational fit remains unknown.:** an empty list never means the equipment is fit.
- **Unknown / Unavailable:** the source value is missing or could not be read. Unknown never stands in for "Ready" or "No".

## What you cannot do / caution
- No field is edited on this page and no readiness result is produced. The operational decision and safety authority stay with the Tug Master on board (13.03 §3).
- A shore assignment proposal never overrides the Tug Master's onboard safety decision (13.03 §7).
- Even with a declared cross-mode permission, every Ship Mode screen still needs its own server authorization.
- While offline, current profile sources are not shown; reconnect to recheck access and source versions.

## If something goes wrong
- **Access is not available:** check your role, tug and object context; contact your tenant administrator if needed (13.03 §10).
- **"Current identity and vessel access must be checked" alert:** press **Recheck access**.
- **Data unavailable:** check your connection and press **Retry**.
- **Unexpected BP or equipment values:** note the source and version fields and review them with the source owner; if a record cannot be verified, check its evidence and provenance (13.03 §10).

## Related pages
