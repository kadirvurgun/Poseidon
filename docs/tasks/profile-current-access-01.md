# PROFILE-CURRENT-ACCESS-01

AUTHORED / UNVERIFIED. DEV-020 / T-S2-001, 18.01 Backlog row21,
SHA09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:
one controlled versioned vessel/regulatory profile with provenance/freshness.
The repository security baseline requires current tenant/role isolation and safe
privacy handling. This package repairs the existing profile UI's current-access
projection; it does not introduce another profile registry or approve declarations.

Previously a save returning401/403/404 left the previous profile form/history
visible and editable. History denial was treated as a generic history error, and
independent pending reads could repopulate state after another request withdrew
access. The component also relied on callers to remount it when vessel changed;
an old async save could still call parent dirty/saving callbacks after unmount.

The actual exported profile now owns a vessel-keyed child. Scope changes remove
old content before rendering the new scope. Current-access denial from profile,
save or history clears private form/history/success projections, blocks late reads
and requires explicit authorized reload. Save completion after unmount cannot
alter parent state. Ordinary validation/conflict/network-uncertain handling and
same-command retry remain in the existing path; language changes keep the child
and pending retry key. No storage, telemetry or new backend authority is added.

Seven authored React cases cover three save denial statuses, synchronous vessel
context switch plus late save/callback isolation, late history after denial,
history denial before profile response, and explicit same-scope recovery. These
are component tests with typed mocked API adapters, not executed real-host or
provider journeys. Suite/build/typecheck/browser/QA/CI: DEFERRED / NOT RUN.

The scope key is vessel ID, not a replacement for host identity isolation or
server authorization. Disconnected clients cannot learn remote revocation until
an authoritative response; this is not full privacy/GDPR or DEV-020 acceptance.
The larger bundle must run both existing regulatory-profile coverage and these
new regressions against the integrated target. No accepted SP is awarded here.
