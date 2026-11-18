# UI-SHELL-CONSISTENCY-20261108 — compact navigation and shared controls

Source delivery for the user's 08.11.2026 feedback: unexpected left navigation scrollbar, inconsistent button size/placement, reusable professional components. All new tests/build/typecheck/lint/browser/contract generation remain **DEFERRED / NOT RUN** until actual larger PR preparation.

## Controlled source and scope

Active baseline `poseidon-v46.3-20261030`. 13.04 UX/UI Design System SHA256 `83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c`: paragraphs12,31–32 grouped sidebar/search/current context/role-mode intersection;33–34 page title/context/primary action;47–52 one primary advancing action and distinct secondary style. Current implementation groups organize implemented authorized destinations; they do not claim completion of the canonical11-menu/39-module/196-screen map in05.01/13.05.

## Implemented source behavior

- [EnterpriseSidebarNav](../../frontend/src/product/EnterpriseSidebarNav.tsx) is the shared presentation component. Only current server/lens-authorized entries are projected. One module group is expanded at a time; a committed route opens its active group. Changing a group never navigates or grants authority. Closed content remains outside the keyboard loop and removed capabilities disappear. Empty/loading navigation remains valid.
- The role work-view disclosure starts compact while its authorized projection stays mounted. Brand/footer do not shrink. The inner navigation owns overflow with `min-height:0`; the outer fixed sidebar clips neither an independently scrolling parent nor a second mobile scroll container. Necessary overflow on short or zoomed viewports stays scrollable. It is not hidden by an inaccessible scrollbar mask.
- The mobile drawer locks background document scrolling, restores the previous style on close/unmount, and retains the existing focus/Escape/dirty-navigation guard. Crossing to desktop closes the mobile modal state. The translucent click-to-dismiss overlay is excluded from ordinary button fallback styles, including hover.
- [Button](../../frontend/src/components/ui/button.tsx) and its [control scale](../../frontend/src/components/ui/button.css) own dimensions and padding for buttons and slotted links. Standard40px/compact36px desktop controls and44px narrow/coarse targets are implementation density choices, not an operational field/catalog rule. Icon controls remain square. Long labels wrap inside available width.
- [EnterpriseActions](../../frontend/src/product/EnterprisePatterns.tsx) owns action layout separately from Button appearance. Explicit `layout="form"` adapts form actions to available narrow width without relying on translated labels. Header/form/toolbars stop overriding the shared padding/height. Directory, crew, technical, daily, fuel/port, defect and tug forms adopt the explicit action pattern. Header language, role and context actions use the shared Button.

## Acceptance cases and authored checks

| Trace | Behavior | Source |
|---|---|---|
| UI-SHELL-NAV-01 | Current module open; other authorized groups collapsed; native keyboard activation; no hidden tab targets | EnterpriseSidebarNav component tests |
| UI-SHELL-AUTH-02 | Group changes grant no destination; capability withdrawal removes entries; actor-change navigation stays fenced | Sidebar and existing Chrome component tests |
| UI-SHELL-DRAWER-03 | Compact settings remain focus-safe; mobile background scroll is restored with Escape and close | Chrome component and genuine shell sources |
| UI-SHELL-GEOMETRY-04 | Single scroll owner, footer reachable, no forced overflow at900px height; short/mobile overflow remains usable; overlay remains translucent | Signed enterprise-shell browser source |
| UI-SHELL-CONTROLS-05 | Form actions have one primary, matching row heights, no overlap/horizontal escape,40/44px targets across1280/390/320 widths | Signed form-control browser source |

Browser sources use the real signed host and actual authorized menu projection. Dense-navigation case requires at least12 real destinations in5 groups; the form case requires a real editable vessel. Missing setup fails; no mocked business/navigation responses, grants or private runtime provisioning are introduced. Existing role-menu journey sources explicitly open settings/categories before interacting; their authority and dirty-guard assertions remain.

The independent bounded source review found a mismatched JSX opener/closer during conversion and an older backdrop CSS specificity conflict. Both were corrected in source before delivery. This is **SOURCE_CORRECTION_REVIEWED_ONLY**, never an executed build/browser PASS. Exact integrated source/AST/historical acceptance metadata belongs to the phase report. No claim is made that an observed running preview contains this new source.

The source integration also uses a shared SourceDraftLaunch for equipment→defect and maintenance-plan→work-order entry. Exact target capabilities stay Session/source bound; URLs carry only paired source UUID/revision metadata. Parent pending/uncertain/offline guards remain in control. Work-order and existing defect launch component sources cover this boundary; no new approval or evidence adoption is implied.
