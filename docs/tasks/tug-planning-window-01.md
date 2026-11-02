# TUG-PLANNING-WINDOW-01

AUTHORED / UNVERIFIED product-domain support for DEV-081/T-TUG-002 intake and
DEV-082/T-TUG-003 board. Authority: active18.01 Backlog rows82/83, SHA256
09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674;
14.02 Data Dictionary 2316 rows1920/1921, SHA256
3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018.

Source explicitly distinguishes required requested_start from conditional
requested_end. The helper preserves that distinction: MissingStart,
EndNotSupplied, InvalidOrder and Defined describe entered data only. Missing end
is neither automatically filled nor universally mandatory. Job-specific policy
must decide its actual requirement; this helper does not approve such a policy.
Historical/future data is not rewritten using the current clock.

Comparison of fully entered windows returns Separate, BoundaryTouch or
Intersecting, with Unknown for incomplete windows and InvalidWindow for inverted
ones. UTC instants are compared across offsets without buffer arithmetic.
Equal endpoints remain a declaration; no unsupported minimum duration is invented.
BoundaryTouch preserves the absence of a controlled turnaround/buffer policy.

These are not tug assignment, availability, BP sufficiency, crew/gear fitness,
operational conflict resolution, readiness, dispatch or Master approval. The
actual board must not translate Separate to operational NoConflict/Available or
Defined to Ready. Real assignments, current authority, source policies and
evidence remain required by their own contracts.

Ten meaningful unit cases authored; execution/build/typecheck **DEFERRED**.
No actual passing cases, legal catalog, human sign-off or accepted SP awarded.
This pure projection introduces no persisted operational entity requiring fake
seed data. The following Tug package must wire it into real scoped intake/board
flows, audits/versions/EF/seed/UI and real PostgreSQL/browser cases. It is not
completion of either full story.
