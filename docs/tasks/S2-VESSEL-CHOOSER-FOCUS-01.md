# S2-VESSEL-CHOOSER-FOCUS-01

Baseline poseidon-v46.3-20261030; DEV-021/T-S2-002, DEV-025/T-S2-006 and13.04
paragraphs110–129 keyboard/modal-focus contract. Author Root; independent QA
frontend gate role. Engineering correction, no canonical SP or phase acceptance.

The actualfaf7 diagnostic failed the chooser search-focus assertion at
[navigation-role-lens-cases.ts](../../tests/Poseidon.Browser.Tests/register-specs/navigation-role-lens-cases.ts)
line155:96. The source had mount-time autoFocus while its native dialog was still
closed, followed by showModal with the Close button before the search field.
It did not establish focus after the native modal opened.

[WorkspaceVesselSwitcher](../../frontend/src/product/WorkspaceVesselSwitcher.tsx)
now focuses the existing labeled search through a ref immediately after
showModal. Removing mount-time autofocus also lets the existing effect capture
the original trigger for focus return. Existing context/rights checks, dirty
navigation, close/Escape, list/search/paging, source values, styles and17locale
labels remain unchanged.

Acceptance:

1. Actual modal opening ends with the search focused, preserving keyboard entry.
2. Closing/unmount returns focus to the still-connected original trigger.
3. The [component regression](../../frontend/src/product/WorkspaceVesselSwitcher.test.tsx)
   deliberately makes the native-dialog stub focus the first Close button,
   then checks search focus and trigger return. Existing authorization/denial/
   scope tests remain intact; no assertion is weakened.
4. Independent source review, later combined component/typecheck/build and
   genuine keyboard gate are required. Focus-ring contrast remains part of the
   broader S2 rendered accessibility observations, not proven by this unit case.

State: AUTHORED / execution DEFERRED to the next combined frozen bundle.
Tests/builds/browser run for this change:0; acceptance=false. The earlier actual
failed focus observation is retained underfaf7, never relabeled PASS.
