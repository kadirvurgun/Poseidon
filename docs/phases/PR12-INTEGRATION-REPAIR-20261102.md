# PR12 integration gate repair — 02.11.2026

Requirement/traceability: FOUNDATION-ACCEPTANCE-BUNDLE-01, AUDIT-TRACE-01 AT-01..08, TODAY-MAINTENANCE-01 TM01..07; existing owning/qualified-capacity test contracts. Source authority remains docs/sources/active.json. This is a gate repair, not canonical sprint acceptance.

Actual preceding GitHub head b7926a3: full tracked repository scan PASS; Release build PASS; persistence unit25/25, domain550/550, API unit82/82 PASS; persistence integration75/75 PASS. API integration524 discovered/executed,478 passed,46 failed,0 skipped. Frontend/browser check PASS. The S3 driver successfully launched its child; exit2 came from failed API tests. Raw child diagnostics remain unpersisted.

## Repairs and acceptance

- Multi-file qualification/owning fixtures explicitly opt into a repeating INSTREAM protocol peer. The ordinary one-shot peer, malware, truncation, unavailable-scanner tests and actual provider gates remain intact. Each connection still parses and validates the complete protocol. Fixture shutdown cancels and observes its owned receive task.
- Historical timestamp fixtures use PostgreSQL microsecond precision or reload their persisted baseline before exact conversion comparisons. Assertions remain exact, without tolerances or skipped cases.
- Empty-maintenance scenario expects the existing200 response from vessel creation, then still verifies missing coverage and absence of invented plans.
- Audit expected counts enumerate the actor-visible entity contract; the complete page is additionally checked to prevent leaking newly seeded entities.
- Exact assigned human trace readers no longer require tenant-wide regulatory privileges to read their assigned owning-document trace. Explicit regulatory/file/evidence/control permission, exact live reader assignment, vessel and protected crew checks remain. Added permission-removal404 and assignment-removal403 assertions; audit reader metadata uses auditItems with private items empty.
- OpenAPI was exported from the actual Release host (157 paths,176 schemas), and TypeScript regenerated with the pinned7.13.0 generator.
- The API gate exports only existing test source filenames/line numbers for failures, enabling diagnosis without persisting request, exception or SDK bodies.

Verification so far: API gate helper13/13 PASS; API project Release build0 warnings/errors before final audit-test adjustment; maintenance historical-reference regression1/1 PASS. An incremental rebuild concurrent with an owned test failed from Windows apphost file locking; this is not a code/build PASS, and a serialized rebuild is required. Final targeted regressions, required exact-head CI and independent target review remain PENDING. No test removed, disabled or weakened; no check bypass. Canonical8/970 SP and operational/human approval gates unchanged.

## Additional isolated failures found before the next CI round

At f69d838 the focused Compliance6, Capacity7, Today1, maintenance-history1 and audit-scope1 cases passed (16 total). Four further isolated regressions failed: stale unconditional file blocker assertion, missing catalog audit result codes, simulation snapshot wire/hash escaping mismatch, and a second submicrosecond fixture timestamp. Construction-record and operation/service cases exposed a forbidden no-op update to immutable CleanTransport and a fixture passing an actor ID where the API requires a provisioned role ID. These failures were not counted as passing.

The next delta keeps optional files source-policy dependent, adds only the two closed catalog governance audit codes (catalog_incomplete, invalid_transition) with negative-vocabulary coverage, normalizes the snapshot JsonElement to its actual emitted JSON before computing its exact hash, fixes the timestamp/role fixtures, and removes the invalid terminal-stage UPDATE. Real clean-stage lookup, exact file FK, transactional checks and post-write RecordAccess remain. CleanTransport is already immutable in the database; its trigger is unchanged. Prior immutable snapshot rows/hashes are not rewritten.

Final serialized solution build, affected regressions and exact-head CI/independent review are pending for this additional delta. Autocomplete is temporarily disabled while the new delta awaits review. No gate, assertion of meaning, or privacy rule is removed; canonical8/970 remains unchanged.

The additional c04ec70 delta built as the complete Release solution with0 warnings/errors. Persistence unit27/27 PASS, including both closed catalog result cases; simulation exact wire-snapshot hash regression1/1 PASS. Independent reviews approved disjoint authorship portions of c04ec70. Construction linking progressed, exposing the same forbidden terminal-stage no-op in the attestation path; that second occurrence is removed with exact stage lookup and byte SHA256/profile/qualification concurrency guards retained. No other FileStage State no-op remains in the API. Affected construction/operation scenarios and final exact-head CI remain pending; no merge is claimed.

A further targeted Operation9 run exposed an interaction in c04ec70: snapshot wire encoding was normalized, but staleness compared fresh provenance to captured raw JSON spellings. Its prior no-blocker review was withdrawn for that interaction. Both provenance and scalar-fact comparisons now serialize both JsonElements through the same existing encoder. Actual string values, number lexemes, revision differences and array/property order remain significant. Seven focused escape/material-change regression cases PASS (1 positive,6 negative); no tolerance or DeepEquals relaxation is used. The corrected API Release build passed0 warnings/errors. Construction record's complete link/attestation/integrity/revocation scenario nowPASS1/1. Final Operation9 and required exact-head CI remain pending; fresh independent code review is required before auto-merge is re-enabled.

## Last two isolated regressions

On95a2a18: Requirements graph1/1, plan conflict1/1 and legacy historical conversion2/2 passed. The full Operation9 and Construction1 were also passing. Remaining isolated failures were observation replay and the overlap fixture.

Maintenance observation replay compared incoming100ns timestamps with PostgreSQL-stored microseconds and incorrectly rejected the identical capture. Capture now validates the original future instant first, then normalizes accepted ObservedAt to UTC microseconds consistently for storage and replay. The regression forces a seventh fractional digit, verifies identical replay, rejects a one-microsecond change, checks the exact persisted timestamp and retains immutability. Its positive current-calculation fixture supplies the already eligible synthetic owner role.

The overlap test could select the seeded empty plan because both new jobs had null start times and UUID tie ordering. It now filters the two authored synthetic jobs, asserts a selected authored job and HasMore, and verifies the exact other conflicting job beyond page1. All human-review/no-clearance assertions remain.

Latest serialized API Release build PASS0 warnings/errors; both affected real-API/PostgreSQL regressions PASS1/1 each,0 skipped. Triage independently reviewed all three source/test changes with no blocking findings. Final committed-target review and required full CI remain pending. No check bypass, operational sign-off or canonical acceptance is granted.
