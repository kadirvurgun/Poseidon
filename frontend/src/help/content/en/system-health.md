---
key: system-health
title: System health
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## What this page is for
"System health" shows administrators **service observations** on one screen: API, audit writer, synchronization, notification jobs, file service and input freshness. It answers: "Which service signals were observed for the selected vessel and this tenant, and which were not?" Configuration and missing telemetry do not establish healthy service or vessel readiness.

## Who uses it
- **System / Tenant Admin:** to notice service and synchronization problems and handle them in a controlled way (13.03 §9).
- **Support / Engineering:** to review observations when an application error is reported (13.03 §10).

## Before you start
- The page is visible only to users with tenant-wide read, action history read and system health read access.
- In the top context bar, check that the **right company** is selected.
- Observations are requested per vessel; you must select a vessel first.

## Step by step
1. Choose the vessel in **Vessel observation scope**. For a long list, use **Previous vessels** / **Next vessels**.
2. Check the "Read at" time.
3. In **Service liveness**, read the process start time and uptime.
4. In the **API** and **Audit writer** cards, read the request, error, persisted and latency figures. These cover this tenant since the process started.
5. In **Synchronization**, see committed outcomes, unresolved conflicts and the last committed sync.
6. In **Notification jobs**, see open notifications, escalations past due and the evaluation job state.
7. Read the **File service** and **Input freshness** cards.
8. Under **Pilot metrics**, review observations from enrolled pilot clients.
9. Press **Refresh observations** for current data.

## What the states mean
- **Not observed / Unavailable:** there is no observation for this signal. This does not mean "healthy" or "no problem".
- **Configured · unprobed:** the file service appears configured, but no live storage or scanner probe was made.
- **Within schedule / Running / Overdue / Scheduler not enabled / Run abandoned (lease expired):** the state of the notification evaluation job.
- **Unresolved conflicts:** offline conflicts waiting for a human resolution. They are never merged automatically.
- **Current / Stale / Unknown / Unavailable:** counts of input freshness assessments. Unknown does not mean "Current".
- **This snapshot is older than two minutes:** refresh before relying on it.

## What you cannot do / caution
- This page is read-only; no action is taken on services, vessels or records.
- Notification records are in-app only; they are not proof of delivery or of any safety decision.
- Service liveness shows only that this API process answered; it does not prove other services are running.
- Pilot metrics are declared and unverified; they are not an acceptance claim.
- Do not fix data directly in production; publish configuration changes in a controlled way (13.03 §9).

## If something goes wrong
- **Health access is unavailable for this context:** check your role and tenant scope.
- **Service observations unavailable:** no healthy state is inferred; use "Refresh observations" after a while.
- **There is an unresolved conflict:** direct the user concerned to the Conflict Center (13.03 §10).
- **Application error:** report it to support/engineering with the Correlation ID, screen, time and steps.

## Related pages
