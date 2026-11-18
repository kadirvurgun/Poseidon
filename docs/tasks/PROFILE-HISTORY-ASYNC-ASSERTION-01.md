# PROFILE-HISTORY-ASYNC-ASSERTION-01

State: authored correction; independent review and larger gate pending. Base
`002a931106e021631c8ca5ef24b98428f884edcf`. Canonical acceptance and added SP:0.

Source: active18.01 Backlog21 DEV020/T-S2-001 versioned profile/provenance and
[PRO06/07 history privacy](PROFILE-OWNER-RECOVERY-01.md). The existing
[history recovery contract](PROFILE-DEFINITE-VALIDATION-HISTORY-01.md) requires
the independently owner-authorized history read to finish before its contents
can be displayed. Profile and history need not become visible simultaneously.

The actual002 combined gate passed1659/1660 component cases and failed the
existing profile presentation case at its synchronous history assertion
([RegulatoryProfile.test.tsx](../../frontend/src/product/RegulatoryProfile.test.tsx)).
One selected-case reproduction confirmed that the version-history row was not
yet present, with1 failure and79 other cases not selected. These unselected cases
are not full-file regression evidence. Original reports and source remain bound
to002; no runner rule, application authority or product behavior is changed.

**PHA01:** Await the actual version-history row, preserving its exact version
and Synthetic provenance assertion. Keep every accessible-field, explicit Missing,
unresolved-policy, operational-authority warning and absence-of-Ready assertion.
No sleep, arbitrary timeout extension, catch, test skip or weakened matcher.

**PHA02:** Rerun only the diagnosed case against the corrected source as a narrow
verification. Full-file/package regression, fresh build/scans, genuine/TLS/S2,
independent QA and required CI remain separate gates. A local passing assertion
does not complete the phase or authorize merge.

The parallel author/reviewer agents reported a provider usage limit. Do not
retry them or rotate accounts; independent review remains pending. The user's
latest002 preview is separately running, with its real local test-user login
verified; this test-only correction does not change that built application.
