# S5 integrated workspace experience

Status: AUTHORED / UNVERIFIED. Tests, build, typecheck, lint, generated contracts,
browser execution and independent QA are deferred until actual PR preparation.
No test result, phase acceptance, preview publication or SP acceptance is added.

## Source and ownership

Active [source baseline](../sources/active.json): poseidon-v46.3-20261030.
Original 18.01 Backlog workbook SHA256:
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
DEV-039 / T-S5-001 / Backlog row40 and DEV-042 / T-S5-004 / row43 own
onboarding and the passport. Supporting DEV-036 / T-S4-004 / row37 owns
notification routing. Dependencies and actual acceptance remain in the parent
[backend task](s5-compliance-core-01.md) and phase report.

The coordinator integrates the backend and UI authors on
`foundation/s5-compliance-foundation-20261104`. This integration does not assign
independent QA to either author. New backend views remain reads; the existing
profile save keeps its server authorization, expected version, audited write
and exact retained idempotency command.

## Acceptance cases authored

- Onboarding at 390px and 1440px retains a local declaration across all four
  steps. Step navigation sends no mutation. Review explicitly reports an
  unavailable assessment, and submission stays disabled.
- The previous/next controls have no geometric overlap and the page does not
  overflow horizontally at either width. These assertions are authored source,
  not a claim that a visual/browser check has passed.
- Leaving an unsaved wizard offers keep/discard. Keep retains the declaration;
  explicit discard returns to the vessel without a save command.
- Passport blockers have readable Turkish/English explanations; the exact server
  code remains inspectable. An unfamiliar code remains a source condition for
  review without an invented interpretation or compliant state.
- Maintenance notifications use a readable source-action heading, with routing
  key/version available in a disclosure. An acknowledgement remains distinct
  from source verification and operational approval.

Sources: [onboarding browser regression](../../tests/Poseidon.Browser.Tests/specs/compliance-onboarding.spec.ts),
[blocker explanations](../../frontend/src/product/ComplianceBlockers.tsx),
[notification workspace](../../frontend/src/product/NotificationWorkspace.tsx),
and its existing component regression. All new execution remains NOT RUN.

## Remaining gate

Generate contracts against the actual integrated host; run required domain,
persistence/API, component, ordinary browser and authority/security gates;
obtain independent exact-target QA and required CI before publication or merge.
Missing certificate authority/catalog, source-authorized rule override and
critical-obligation coverage remain visible product/source constraints, never
fabricated approvals or zero counts. Existing S0/S1 and S2 scoped evidence is
preserved separately and does not validate changed S4/S5 code.

## Daily offline static-shell continuation

The dedicated daily adapter also needs the public bootstrap after an offline
restart. The static worker now accepts only the three exact daily resource
routes and either no query or one noncredential `id`/`draft` UUID. Duplicate,
mixed, unknown/private, mutation, cross-origin and non-navigation contexts are
not intercepted. Only the integrity-pinned public index/assets enter its cache;
authenticated navigation responses and context/query values do not.

Six parameterized static-worker routing/privacy regression sources were added to
the existing build-worker test file; none was run. The dedicated adapter prepares
the shared shell explicitly before enabling local capture. Existing worker
upgrade/activation readiness still requires the later real browser gate; no
forced activation, client claim or automatic reload was added.
