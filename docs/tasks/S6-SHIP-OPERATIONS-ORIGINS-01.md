# Ship operational records — owning daily origin context

Source authoring only. DEV-100 / T-OPS2-004 (Backlog row101, dependencies097/099/030)
and DEV-101 / T-OPS2-005 (row102, dependency097) use actual saved daily sources.
Active18.01 SHA256: `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Original14.02 SHA256: `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`.
Source rows2144/2145 describe Noon fuel/ROB summary,2172/2179 Engine consumption/
bunkering; target rows2186–2207 Fuel and2208–2225 Port preserve their own fields.

## Authored acceptance cases

- Optional `ShipOperationOriginPin` is engineering lineage outside the40 target
  field dictionary: kind, resource, revision, actual saved version, hash and source
  version. It never accepts a caller's actor/tenant/approval or an arbitrary URL.
- The owning helper exposes only actual authorized Noon/Engine immutable revisions
  for actual Ship vessels. Current head and historical snapshots are distinct;
  every pin is rechecked by one composable SQL predicate with current read scope.
  Fuel permits Noon/Engine origins; Port permits Noon only.
- The context endpoint provides a bounded read-only subset, actual provenance,
  exact pin and current/historical context. It requires owning daily permission
  even when the caller can read Fuel. Scoped source/hash/head matching and live
  rights are checked outside old snapshots before and after the security audit.
- No unstructured summary or unknown-unit Engine quantity is converted into fuel
  mass, ROB, sulphur compliance, emissions, cargo or financial results. The origin
  never gives the separate operational draft approval or finalization authority.
- Daily capabilities advertise launch destinations only when the actual Ship
  target writer grants current write attribution/scope. Saved-source launch passes
  only kind/id/revision, no values, actor, hash, tokens or command keys in URLs.
  Pending ordinary/review/offline forms block launch; retained editors are not
  silently rebased when a newer source head is observed.
- New/changed pins must match the current owning source. An unchanged pin may
  remain historical under current read permission; the operational owner also
  fences historical output against its CURRENT head's source restrictions.

## Authored checks, not executed evidence

Real API/PostgreSQL sources cover exact current/history/hash/actualversion,
unstructured-summary preservation without conversion, scope/permission/query
denials and controlled advisory-lock source-head/permission changes during the
actual success-audit await. Source-head changes use the normal audited daily API.
Component/contract sources cover private values absent from launch URLs, retained
form blocking, closed scoped destinations and Engine/Port exclusion.

All tests/build/typecheck/lint/generation/model/migration/browser/CI execution is
DEFERRED until the actual larger PR bundle. No runtime, seed import, provider or
preview activation was performed. This source contract does not complete canonical
S6, DEV-100/101 acceptance or any legal/safety transition. DEV-102 is not selected:
its DEV-055 critical-defect-lifecycle prerequisite is in S8 and unavailable here.
