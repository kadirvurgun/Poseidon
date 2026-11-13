# AUDIT-CAPABILITY-CONTRACT-01 — current audit metadata vocabulary

Status: **AUTHORED / execution DEFERRED / acceptance false**. This corrective
subpackage joins the larger foundation gate. It adds no accepted story points.

Active baseline: `poseidon-v46.3-20261030`; 18.01 `Backlog` row17,
DEV-016 / T-S1-004 (SHA256
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`),
14.01 paragraphs140–143 and [AUDIT-TRACE-01](audit-trace-01.md) AT-03/06/07.
Server attribution, privacy-safe bounded metadata and exact authorized immutable
revision links remain the controlling contract.

## Confirmed defect and scope

The genuine `faf7f91` diagnostic failed to display Record details at
[audit-cases](../../tests/Poseidon.Browser.Tests/register-specs/audit-cases.ts):28.
Passive status-only evidence does not alone prove which decoder branch caused
that individual run. Independent source comparison does establish a contract
defect: [ActorResolver](../../backend/Poseidon.Api/FoundationIdentity.cs) admits
94 capability codes and [TrustedActor](../../backend/Poseidon.Api/FoundationIdentity.cs)
records its filtered permissions in `AuditCapabilities`, while the
[client decoder](../../frontend/src/product/audit-api.ts) previously recognized
only63. A valid nonempty audit result containing one of the missing31 codes was
therefore rejected before list/detail display.

The decoder now recognizes precisely those31 existing operational, certificate,
daily, health and notification codes. This changes observation parsing only;
server permissions, role/tenant/vessel scope and endpoint authorization do not
change. Unknown strings, the2048-character bound, exact DTO members, revision
and reason/source validation remain enforced.

## Acceptance cases and authored tests

1. Current operational metadata opens an authorized record, displays the exact
   observed role/capability context and versions, and retains its exact immutable
   revision link.
2. A page mixing a valid record with an unknown capability token fails closed;
   neither a partial table nor selected detail/revision link is displayed.
3. Existing malformed DTO, protected-value, unknown-capability, unavailable,
   permission-revocation and revision-lineage regressions remain unchanged.

Cases1/2 are authored in
[AuditWorkspace.test.tsx](../../frontend/src/product/AuditWorkspace.test.tsx).
No test/build/typecheck/browser execution occurred during authoring. The next
combined gate must run the full frontend suite and the genuine audit journey;
source closure alone is not real-browser or sprint acceptance.
