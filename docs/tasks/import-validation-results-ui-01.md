# IMPORT-VALIDATION-RESULTS-UI-01

AUTHORED / UNVERIFIED support for DEV-069/T-S11-001; active18.01
Development_Backlog_TR.xlsx, sheet:Backlog/row70,
SHA25609890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674.
The source requires validation, reject reporting and provenance. This component
presents final validation outcomes; it does not change import policy, choose
supported source formats or implement domain writes.

The importer adapts generated authorized DTOs to a typed, privacy-reduced page:
preview/revision/template references, exact aggregate counts, offset/limit,
physical source row number, validation outcome and fixed issues with optional
authorized template field ordinals. Raw cells, source record keys, previous
entity data and server exception bodies are deliberately absent from the
presentation contract. Unknown diagnostic codes have fixed non-disclosing text,
including prototype-like strings. Field labels render as inert text.

TR/EN summaries distinguish validation from actual import, verified evidence and
operational approval. Current session/tenant/vessel/upload/schema context must
match before rendering any rows, totals or report actions. Loading, stale,
denied and unavailable states hide previous data. Inconsistent counts/page
metadata, duplicate/nonordered source rows and invalid field ordinals fail
closed without displaying partial results. A valid zero-row source is distinct
from missing results. Paging uses reported result offsets, not sparse physical
row numbers. Reject-report download is a current-capability callback without
an arbitrary client-provided URL; the real endpoint must reauthorize each
request. Busy operations disable duplicate paging/download actions.

The workspace must cancel obsolete requests, invalidate parent caches on
context/revision changes, bind report retrieval to the actual preview/version,
and recheck current authorization and source/domain concurrency on commit.
Client validation does not establish those server guarantees. The component
has no commit action, persistent private cache or invented progress counter.

Meaningful React tests are authored for counts, sparse row numbers/paging,
capability-sensitive report actions, empty/missing data, context changes,
four load/error states, unknown diagnostic redaction, inert labels, malformed
server projections, in-flight actions and English/accessibility semantics.
Execution/build/typecheck/API generation/browser/QA/CI are **DEFERRED**, never
PASS. Actual four-category importer, endpoint integration, format parsing,
domain validation, provenance and real-host journey acceptance remain in
IMPORT-FOUNDATION-01. No operational entity or synthetic seed is introduced.
