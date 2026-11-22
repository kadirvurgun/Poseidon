---
key: notification-escalation
title: Notifications
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## What this page is for
The notifications page lists the **in-app configured source exceptions** (maintenance due, maintenance review required, maintenance source changed or unavailable) for the selected vessel or tug. It answers: "Which maintenance notifications reached my role, how severe are they, which were escalated because nobody acknowledged them in time, and which source record are they based on?" Acknowledgement does not verify evidence or approve operations.

## Who uses it
- **Chief Engineer / Technical superintendent:** to follow maintenance due dates and review requests and move to the source plan (13.03 §5).
- **Tug Master / Master / Crew:** to see notifications routed to their roles and acknowledge them (13.03 §6).

## Before you start
- You need notification, maintenance and equipment read access. Acknowledging and saving preferences need separate permissions; without them the related buttons are not shown.
- Choose an authorized vessel or tug from **Notification vessel**.
- No acknowledgement or preference command can be sent while offline.

## Step by step
1. Choose a vessel from **Notification vessel**.
2. Read the policy line: "Configured · unverified source policy · in-app delivery only".
3. For each notification, read the title, the severity badge (**Info**, **Warning**, **Critical**) and the state badge; check **Source / revision**, **Due date** and **Created at**. **Routing rule** shows the rule code.
4. Use **Open source plan** to go to the related maintenance plan.
5. To record that you have seen a notification, press **Acknowledge** (only in **Open** or **Escalated** state).
6. If you have preference access, edit **Mute nonmandatory advisory notifications** and **Quiet hours** (start, end, IANA time zone) and press **Save advisory preference**.
7. Use **Previous notifications** / **More notifications** for other pages and **Reload inbox** to read again.

## What the states mean
- **Open:** the notification waits for acknowledgement.
- **Acknowledged:** a person recorded that they saw it. This does not mean the maintenance was done or evidence verified.
- **Escalated:** the notification was not acknowledged in time and was routed to the escalation role of its rule.
- **Source changed / Source unavailable:** the basis record changed or cannot be confirmed. The recorded basis is retained; this does not clear or rearm the notification.
- **Mandatory route · cannot be muted:** preferences cannot mute this notification.
- **Unknown (Due date):** the source has no due date. This does not mean nothing is due.

## What you cannot do / caution
- Acknowledgement does not verify evidence or approve operations.
- Mandatory and critical routes cannot be muted by advisory preferences. Quiet hours only delay optional notices.
- An empty page does not mean all clear. If the policy or sources are unavailable, the page says so separately.
- There is no automatic retry; an uncertain command is retained in this tab and context changes stay locked.

## If something goes wrong
- **"Outcome unknown" message:** use **Retry exact notification command** to send the retained command again.
- **"Notification version changed" message:** use **Reload inbox** to load the current list.
- **"Notification sources unavailable or access denied." message:** check your role and vessel context; contact your tenant administrator if needed (13.03 §10).
- **"The saved quiet hours could not be applied" message:** all notices are shown; check your quiet hours settings.

## Related pages
