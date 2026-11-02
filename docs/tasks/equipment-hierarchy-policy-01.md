# EQUIPMENT-HIERARCHY-POLICY-01

Status: AUTHORED, UNVERIFIED supporting implementation for DEV-052 / T-S8-001.

Source: active30.10.2026, 18.01 Development Backlog sheet:Backlog/row53,
SHA25609890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674;
14.01 Product Wide ERD paragraph105, SHA256c3f5e612d69e33d2e5e3a1227644cb9a0ec8937bdcf74b20d2b5a60b968344ab.
The criterion requires versioned parent-child structure within vessel scope and blocks invalid cycles.
14.02 Lookup Kaynak Çözümü row286 identifies Asset.parent_asset_id but explicitly
does not establish a complete authorized selection-query contract. That remains part
of the following actual Equipment API/UI work.

The pure domain policy validates a complete authorized tenant/vessel forest and an
immutable proposed reparent. It rejects duplicate/empty identifiers, missing or
foreign parents, self-parent and indirect cycles including disconnected components.
Iterative O(n) traversal does not invent a depth limit or risk recursive stack overflow.
An empty forest and multiple roots are permitted; unavailable equipment data is not
converted to verified equipment or operating fitness.

Thirteen meaningful unit cases are authored: empty/multi-root, input order/shared
ancestors, self/indirect cycles, disconnected cycle, missing parent, duplicate hiding
a cycle, cross-vessel/tenant, invalid scope/identities, immutable rejected reparent,
valid move/detach, absent node/parent, stale concurrent-snapshot composition and deep
20,000-node import. Execution, build and typecheck are **DEFERRED** by user direction.
There is no passing-test or canonical acceptance claim.

Persistence must take a tenant/vessel transaction lock or equivalent serialization,
read fresh state, apply this policy, retain EF concurrency/version checks and write
through attributed audited SaveChanges. The stale-snapshot domain example is not a
real PostgreSQL concurrency test; the following Equipment package must implement and
author actual concurrent transaction regressions. This helper does not implement
authorization, storage, migrations, seeds, API, lookups, UI or maintenance planning,
and cannot by itself complete DEV-052. No operational entity or fake safety evidence
is seeded by this pure projection/validation policy.
