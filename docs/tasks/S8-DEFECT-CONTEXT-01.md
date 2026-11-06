# S8-DEFECT-CONTEXT-01 — actual equipment context and navigation

**Source delivery; execution DEFERRED / NOT RUN.** DEV-055 / T-S8-004 is an S8 dependency of S6 DEV-102. Existing equipment and evidence engineering sources permit declaration authoring; this is not completion of either canonical sprint.

## Sources, scope and acceptance

Controlled [domain/source task](S8-DEFECT-DOMAIN-01.md): 18.01 Backlog56, dependencies030/052; 14.02 supporting DD447/457/461–465. Asset context is optional engineering lineage, not an approved mapping for the DD451 text field or the entire asset-reliability screen. No asset criticality, safeguard, identity or human decision is copied into a new defect.

- DEFECT-SOURCE-002: the actual current Asset and immutable AssetRevision must agree on tenant/vessel/asset/revision. The pin uses the current head's actual PostgreSQL xmin and SHA256 over exact stored ContentJson. Read-only summaries omit notes, serial number, responsible-role identifiers and private actor/tenant attributes.
- DEFECT-SOURCE-006: an explicit historical revision cannot be assigned a fabricated committed xmin from pre-SaveChanges snapshot JSON. The context endpoint returns `asset_context_stale`409 if the requested revision has advanced. Existing defect revisions retain their original established pin, with current source access rechecked.
- DEFECT-AUTH-007: source visibility, prospective current origin and the returned/current defect data must share a final fresh SQL fence, with synchronous actor/rights recheck after the await and a repeat after success audit. The [reader](../../backend/Poseidon.Api/DefectAssetContextReader.cs) exposes a composable predicate rather than two sequential boolean checks. No raw SQL or tenant-filter bypass is introduced.
- DEFECT-NAV-008: explicit `defect.read` and `equipment.read` control actual global destination/search discovery and scoped Ship/Tug role lenses. The equipment launch uses the target API's write capability; source workspace access alone grants no defect write. It sends only paired assetId/assetRevision metadata. Dirty, busy, ambiguous, offline or pending observation changes block launch through the owning form guard.
- DEFECT-CONTEXT-009: switching vessel drops the old record/source selection and private query values. Root and scoped defect routes dispatch to the owning workspace; role labels never create server permissions.

## Authored checks and limitations

[Actual API/PG source](../../tests/Poseidon.Api.Tests/DefectAssetContextTests.cs) covers real xmin/source hash, summary privacy, stale revision conflict, foreign vessel/permission/query denial, and source-head or permission changes while the success-audit await is held by a test-only database barrier. [Launch component source](../../frontend/src/product/DefectEquipmentLaunch.test.tsx) covers target capability, metadata-only navigation, pending-form guards and session replacement. [Navigation source](../../frontend/src/product/defect-navigation.test.ts) covers exact server destinations, scoped role-lens paths and removal of source IDs when changing vessel.

No test/build/typecheck/lint/contract generation, migration parity, browser, private fixture, provider or CI execution occurred. Backend source/security inventory and API tests accompany the owning core task; UI workspace/receipt/session recovery accompanies the UI task. Verification, effectiveness, controlled closure, authoritative evidence adoption, dedicated offline policies and canonical acceptance remain open.
