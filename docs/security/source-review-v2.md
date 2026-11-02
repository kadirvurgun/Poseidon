# FILES-SOURCE-COMPLETION-01 repair contract — UNVERIFIED

This is the implementation response to SOURCE-CLASSIFICATION-REVIEW-01 rejection of
`97a1500e937de43ca3e83d0c1e49e034c22f6af7`, not approval of its proposals. The immutable
private receipt SHA256 is `02183cf634aff093beb33a8952db99b071ad02aa5f2ccd23967be2b2963bd523`.
All 159 earlier proposed waivers were rejected for common implementation/evidence
deficiencies. This does not establish 159 live credentials. The original pending
ledger is preserved as `files-source-classifications.97a1500.pending.json`; the old
context proposals and private receipt remain unchanged. Actual or uncertain credentials
retain explicit private remediation/rotation gates. No approved exceptions file exists.

## Exact identity, independent of unrelated commits

`source-binding-v2` target keys are `path`, `rule`, `line`, `occurrence`, `fingerprint`,
`context_sha256`, `immutable_source_revision`, `identity_version`. Path includes every
archive member and the projection suffix (`#text` or `#utf16`). Line is one-based;
occurrence is the zero-based character offset in the canonical presentation, distinguishing
identical same-line XML expressions. Every occurrence is retained; no line-level deduplication.

`immutable_source_revision` is `git-blob-sha256:` followed by SHA256 of the Git blob
preimage `blob <canonical-byte-length> NUL <canonical-bytes>` of the entire top-level
source file. It is a canonical Git SHA256 blob identity, not HEAD, the current commit,
a native SHA1 object ID, or an assertion that a synthetic canonical blob was stored in Git.
Git-tracked input enumeration remains mandatory for repository scanning. The historical
checkpoint is provenance only: every new scan recomputes source identity from actual inputs.
Changing another repository file or committing documentation does not invalidate an unchanged
historical input. Changing its source bytes, member path, surrounding member bytes, rule,
line, occurrence or matched value cannot inherit that input's prior approval. Repacking an
archive changes the source blob and requires review even if one member is unchanged.

Canonicalization deliberately changes only CRLF/CR newline units to LF. For BOM-marked
UTF-16 it handles aligned endian-specific code units, retaining BOM and all other bytes,
including an odd trailing byte. Other non-ZIP bytes receive byte-level CRLF/CR handling.
ZIP containers beginning with PK remain byte-exact. No Unicode, case, whitespace or invalid
byte replacement normalization participates in identity. Member `context_sha256` hashes
these canonical original member bytes, including for XML text projections. The top-level
source blob plus member context therefore binds both enclosing archive and original XML.
Presentation decoding may replace invalid bytes for pattern detection; it never supplies
identity bytes. Two distinct invalid byte sequences cannot gain equal identities from equal
replacement-decoded text. UTF-8/binary scanning remains; BOM UTF-16 adds a separate scan for
top-level and nested archive members. Existing bounded member/total/depth limits remain;
ambiguous duplicate ZIP names fail closed. Originals are read-only.

## Proposal and receipt consumption

Canonical JSON hashing uses sorted keys, compact separators, ASCII escaping and SHA256.
A proposal has exactly `target`, `classification`, `rationale`, `evidence_sha256`,
`author_reference`; its hash binds every field. The evidence hash binds the entire shared
safe context evidence object. Current ledger entries are all `unresolved` and cannot be
consumed as approved exceptions. An independently justified classification would change
the proposal hash and require a review of that exact new proposal.

A future exception envelope has exactly `schema_version: source-binding-v2`, `proposals`,
`receipts`, and `exceptions`. Each selection contains `proposal_sha256` and `review_sha256`.
Each receipt has exactly `schema_version`, `status`, `proposal_sha256`, `evidence_sha256`,
`author_reference`, `reviewer_reference`, `target_sha256`. Status must be
`independently-reviewed`; author and nonempty reviewer references must differ; proposal,
target, evidence and author must agree exactly. Rejected/pending/legacy receipts cannot waive.

The receipt's canonical hash must additionally be supplied through `--review-sha256`
from **protected coordinator/CI configuration**, outside the exception document. No pins
are inferred from reviewer names, receipt contents, old approvals, ledger fields or an
author-controlled evidence reference. The scanner is not an identity provider or signature
verifier: protected pin provisioning is the explicit authority boundary. An author who can
change the invocation's trusted pins can defeat that boundary; coordinator controls must
prevent this. Merely writing two reviewer strings into an exception file grants no approval.
No pins, approved receipt, reviewed exception file or CI bypass are supplied in this repair.
Synthetic receipts exist only in isolated unit-test fixtures (including temporary files), never in repository approval inputs.

## Safe evidence for independent review

`files-source-review-evidence.pending.json` uses `safe-source-review-v2` and binds the exact
canonical pending-ledger hash. Every proposal carries its exact target/hash and a shared
context ID/evidence hash. Contexts are emitted once, including each separate member and
projection. All detected occurrences within each targeted context are enumerated, not only
the nearest archived finding. Same-line duplicates have distinct offsets. The targeted
authoring pass resolves archived observations; it is not a full repository/client scan.

Safe lexical evidence retains operators, token kinds, offsets, literal-vs-identifier shapes
and bounded per-occurrence windows, including long XML lines. Source identifiers are SHA256
pseudonyms shared across references; only a fixed language/runtime vocabulary is rendered.
Quoted strings, numbers, arbitrary characters and constant AST values are withheld. No raw
source line or matched expression is emitted. Full parseable Python contexts additionally
carry AST node kinds, assignment/call/attribute/reference shapes, exact source line/column
ranges, imports and matching symbol Store/Load occurrences. Generation and consumer evidence
is structural and source-bound, not an author's safety assertion. Arbitrary comments, strings,
AST constants, exception messages and tracebacks are not rendered.

Unparseable prose, XML/code fragments, truncation/budget limits, string placeholders and
unknown external consumers are explicitly unresolved. Bounds are 8,192 characters/256 tokens
per line or window, 256 full line views, 1 MiB Python context, 20,000 AST nodes, 128 selected
nodes, 1,024 symbol occurrences, 8,000 serialized AST nodes and 12 MiB total compact evidence.
Per-occurrence structural windows remain even when a whole line/context exceeds its budget.
These artifacts do not establish operator non-reuse, runtime generation for every case or
complete external consumer provenance. Inconclusive evidence must remain unresolved, never
waived. No current classification is asserted conclusive by this author.

## CI and deferred verification

The existing required `persistence-and-security` job directly executes the full tracked
repository scan. The existing required `frontend-and-browser` job directly executes the
client scan on `frontend/dist` after the actual npm production build. Neither scan uses a
conditional step, continue-on-error, exceptions file, unrequired side job or needs-skipped
chain. Exit 1 for findings and exit 2 for input/coverage/receipt failures fail their existing
jobs. Missing client index/nonempty JS remains a failure. Machine evidence records the exact
workflow source blob and commands; a static wiring regression is authored. Branch protection
is not changed or claimed reverified here. No green gate is manufactured.

Coordinator adapters consuming the old flat ledger, context hashes, reviewer-string schema
or 159-target count require v2 adaptation before the renewed gate. Do not migrate approvals
automatically or treat the new pending ledger as an exception envelope. Run the combined
gate, source-bound independent review and exact-target independent code QA before any later
publication. All suites/build/typecheck/browser/provider/full scans remain DEFERRED here.


## REVIEWED-SOURCE-SCAN-01 consumer wiring — supersedes direct-invocation paragraph

The required repository and postbuild-client steps now call reviewed_secret_scan.py, which unconditionally delegates to the same full scanner or fails nonzero on invalid trust. Optional reviewed-source exceptions require the externally provisioned POSEIDON_SOURCE_REVIEW_SHA256 JSON hash list. No hashes, reviewer verdict, exception file or external configuration value is supplied by this authoring. Current pending cases remain unresolved and old rejected waivers remain rejected. Actual protection/provisioning of external trust is a coordinator obligation, not proved by a workflow variable. Current pending safe evidence must be regenerated against the final workflow and consumer identities before independent review. See the exact [consumer contract](../tasks/reviewed-source-scan-01.md); all new regressions/scans/CI remain DEFERRED/NOT RUN.
