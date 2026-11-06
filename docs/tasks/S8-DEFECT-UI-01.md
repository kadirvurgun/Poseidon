# DEV-055 defect declaration UI

Owner: UI author. Status SOURCE_AUTHORING / VERIFICATION_DEFERRED. Selected task 13 SP / zero accepted; no canonical S8 completion claimed.

## Sources and boundaries

- Active baseline `poseidon-v46.3-20261030`, `docs/sources/active.json`. Original 18.01 SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`, Backlog row 56, DEV-055 / T-S8-004: Detect→Assess→Safeguard→Action→Verify→Effectiveness, critical defect cannot close without safeguards, temporary expiry monitoring. Dependencies DEV-030/052 are engineering seams, not accepted legal/safety workflows.
- Supporting 14.02 SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`, Data Dictionary2316 rows 443–465, especially record_datetime447, criticality457, defect_no461, consequence462, redundancy/safeguard463 and notes465. This subset is not the full canonical asset-reliability contract. Verifier450/return-to-service464 remain controlled Human decision gates.
- 13.04 SHA256 `83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c`, paragraphs 29–34 context/navigation/header, 80–89 unknown/status/history, 110–114 language/focus/labels/contrast/touch, 124–131 Why/offline/responsive.
- Scoped Graphify query preceded targeted reads; no MCP graph connection was claimed. The UI skill and active baseline were read. Historical prototype/runtime archives are not implementation or acceptance evidence.

## Shared API and declaration fields

The first consumer follows actual domain checkpoint 3252656 and asset reader 8d0bc55. Strict eleven-fact schema contains Known/Missing/Unknown values, original source locators and declared lifecycle phase options. Declaring Verify/Effectiveness does not establish verified/effective status. The actual API capability contract is exposed as `defectApi.capabilities(vessel, signal)` for root's authorized equipment launch consumer. Actual writes use vessel query, expectedVersion, fields, optional exact asset pin and sourceVersion; no actor, legal approval or invented reason field is sent.

Server temporal projection is distinct from immutable facts. It returns a fresh UTC assessment for missing/unknown measure/deadline, declared expiry reached or not yet reached; closureAvailable remains false. Replays preserve immutable fields/IDs/versions/hash while temporal assessment may advance. No browser clock determines expiry authority or silently clears an unknown deadline.

Optional asset context uses an actual current reader and established current head xmin. Older requested revisions return409, rather than fabricated historical xmin. Previously captured defect/history pins stay immutable; current-source metadata may be unavailable after equipment changes. The retained pin can remain during ordinary defect edits under current read rights; new/changed pins require genuine current context. Asset criticality is never copied into the defect declaration.

T-S8-004-UI-01: `DefectFieldsForm.test.tsx` authors unknown expiry, multiline safeguard, declared phase/approval separation and accessible readonly/error association behavior. T-S8-004-API-01: `defect-api.test.ts` authors incomplete facts, no promoted closure/verification, unknown vs not-due, fresh temporal assessment over unchanged facts and exact current asset binding/private expansion checks. All are NOT RUN.

## Next source slice and execution limits

The dedicated workspace, root vessel selector, list/current detail/history, restriction/expiry observations, current asset aside, dirty/unknown command guards and genuine browser source journeys follow this shared checkpoint. Root owns App/global navigation/equipment launch and central browser registration; this author edits only the new defect modules/tests and this task document.

No tests, build, typecheck, lint, generation, browser, lab, provider, runtime, CI, push or PR executed. Only source and whitespace review support this checkpoint. Closure, verification, effectiveness, return-to-service and canonical phase acceptance remain OPEN and unavailable.
