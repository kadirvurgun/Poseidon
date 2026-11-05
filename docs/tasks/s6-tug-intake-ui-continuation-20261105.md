# S6 Tug intake and navigation continuation

State: SOURCE_AUTHORED / UNVERIFIED / NOT ACCEPTED. DEV-081 and DEV-082 retain zero
accepted story points. Canonical S6 contains nine tasks / 87 SP; this continuation
addresses a UI reliability subset of its existing Tug intake and board software.
It does not close assignment, operational availability or lifecycle policy gaps.

## Authority, dependencies and ownership

Active baseline: `poseidon-v46.3-20261030`. Backlog 18.01 SHA-256
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`,
sheet Backlog rows 82 / 83, stories T-TUG-002 / T-TUG-003, DEV-081 / DEV-082.
Prior DEV-080 and scoped request/API source exists at the S5 checkpoint
`79518c760d92d5bf9864c0a4d5d3bdbbcab161fc`; dependency acceptance remains open.
Root owns this continuation on `foundation/s6-certificate-foundation-20261105`.

Supporting sources:

- 14.02 Data Dictionary rows 1914–1931 (intake), 1932–1945 (board), SHA-256
  `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`.
  FR-/TC-tug-job-intake-* and FR-/TC-tug-job-board-* traceability applies.
- 13.02 screen inventory paragraphs 1065–1080 (intake / board / detail), SHA-256
  `b0b5e9fb3cad5137d76a4de912c3a60351b667d0aeb08252856e3a9445928b45`.
- 15.01 API inventory paragraphs 1525–1549 (current Tug resources), SHA-256
  `68955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837`.
- 13.04 design system paragraphs 20–34: controlled lists, contextual time,
  field validation, draft/authority distinction, table scoping and keyboard/TR/EN.
- Existing [Tug foundation contract](./tug-foundation-01.md) and its unresolved
  D-STATE-01 lifecycle and operational evidence limitations remain in force.

## Authored acceptance cases

1. Group request/vessel, requested time/location and resource/notes fields with
   labeled fieldsets, a stable two-column desktop grid, one-column mobile grid,
   meaningful text areas and aligned shared enterprise action buttons.
2. Preserve exact decimal BP, nullable conditional values and current server
   option lists. Allow incomplete draft saves; never claim operational approval.
3. Check the original tenant/actor and current scoped write permission before
   sending the frozen original body/key/base version, and again before showing a
   successful result. Suppress duplicate submissions synchronously.
4. A potentially committed write stays uncertain through subsequent validation,
   denial or conflict responses. Rechecking current access preserves its original
   inputs and key. Only original-identity recovery or explicit discard can resume
   or drop retained local data; a new identity receives no editor values.
5. While the intake/editor is open, focus/reconnect/periodic identity reads
   withdraw private projections during checking and preserve editor memory.
   Navigation, unload, board refresh and save guards consult synchronous flags.
6. Associate server field validation with its input and accessible description;
   preserve input for correction when an initial validation failure is definite.
7. Decode owning request responses before rendering: exact resource/vessel IDs,
   source version, draft provenance, version bounds and field shape. Pin save
   receipts to their original client resource ID; a malformed success response
   leaves the original command uncertain. Apply the same request decoder to
   board rows rather than accepting malformed nested editor data.

## Verification and remaining limits

Meaningful mocked component regression sources cover grouping/field errors,
actor and tenant changes before send, identity change after a successful HTTP
write, retained 503→403→exact-retry, duplicate submits, and focus identity changes.
The existing intake/board/decimal/navigation cases are retained and adapted to
explicit synthetic actor and tenant references.
Contract regression sources additionally reject cross-resource receipts,
cross-vessel responses, malformed field values and fabricated verified provenance.

All component/backend/real-browser tests, build/typecheck/lint, generated contract
parity and regression execution are DEFERRED until the actual larger PR bundle
gate under the user's cadence. This document records source behavior, not PASS,
independent QA or canonical acceptance. No preview, provider or real user settings
were changed. The retained draft lives only in memory and is lost on a confirmed
page unload. Planning/offline panel authority and lifecycle acceptance remain
separate work; this continuation does not certify those existing flows.
