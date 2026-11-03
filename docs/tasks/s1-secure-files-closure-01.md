# S1-FILES-REJECTION-01 — AUTHORED, verification DEFERRED

Date: 2026-11-03. Owner: secure-files backend worker; integrated acceptance and
publication belong to the coordinator. This is a scoped DEV-018 repair, not S1
acceptance or a production storage/scanner rollout.

## Controlled requirement

Baseline [active source](../sources/active.json): `poseidon-v46.3-20261030`.
18.01 Development Backlog, `Backlog` row19, DEV-018, T-S1-006, S1, 8SP;
dependency DEV-017. Source SHA256:
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Literal acceptance criterion:

> Type/size/checksum validation, malware hook, signed access, tenant scope ve audit uygulanır; rejected file state mutate etmez.

Related original excerpts and source digests remain in
[secure-files sources](secure-files-01.sources.json): 15.01 paragraph12
(metadata-first transport intent), 14.01 paragraphs141–142 (attributed audit and
immutable binary metadata), 23.06 paragraph51 (type/size and immutable accepted
references), 13.04 paragraph101 (understandable validation state).
The independent source review did not find an exception permitting a deterministic
checksum, format or scanner rejection to advance an existing durable FileStage.
Metadata-only intent creation itself is an explicit preceding user command.

## Concrete change and acceptance mapping

The existing real S3/ClamAV implementation is retained. Signed access remains bound
to actor, tenant, vessel, object, immutable revision and expiry; request values
cannot select a provider, bucket, object path, actor or clean verdict. Accepted
transport remains unverified evidence.

| Trace | Authored behavior |
|---|---|
| S1-FILES-REJ-01 | Reject current MIME/size/header or provider-context mismatch before changing the Pending intent. Recheck current policy and trusted membership after scanning and before publishing. A MIME removed after staging cannot be accepted through its old declaration. |
| S1-FILES-REJ-02 | Copy, bounded SHA256 validation, actual format validation and clamd scan use the same exclusively created local spool while durable state remains Pending. Deterministic rejection, unavailable scanner and cancellation leave state/xmin and business audit unchanged. A separate SecurityEvent observes the denied attempt. |
| S1-FILES-REJ-03 | Requests never delete a spool by filename. Unix reconciliation can unlink an open file and a stale request can recreate the same pathname; path reuse is not ownership. Failed byte/scanner attempts retain private Pending quarantine for the existing audited xmin reconciler. A competing CreateNew failure returns409 and cannot delete another request's spool. Unrelated I/O failures are not disguised as conflicts. |
| S1-FILES-REJ-04 | After successful validation, current membership/policy is rechecked, then audited Pending→Receiving claims the original xmin. Concurrent reconciliation can win Pending→Abandoned; its xmin fence prevents the late uploader from committing provider bytes or accepted metadata. Existing reconciliation already handles orphan Pending spools after a process crash. |
| S1-FILES-REJ-05 | The actual scanner/storage operation uses one copied operator configuration with separate copied S3 settings and credential references. Live policy/context is checked again after awaited Receiving SaveChanges and before the provider call; an in-flight setting change cannot redirect bytes to a different endpoint/bucket. Once provider finalization can begin, do not remove bytes or guess whether acknowledgement succeeded. Provider failure/unknown acknowledgement or receipt rollback retain Receiving/quarantine for the existing bounded operator reconciliation. Final StoredFile and FileFinalize receipt remain one audited DB transaction. |
| S1-FILES-REJ-06 | Failed new content cannot replace an earlier accepted file, receipt, document revision or evidence revision. The earlier capability-authorized download must return its exact original bytes. |

Known pre-fence transport failures return the fixed safe code
`file_upload_not_accepted`; uncertain post-fence finalization retains
`file_quarantined_or_unknown`. Neither includes provider errors, paths, contents,
credentials or request bodies. No schema/entity/migration/dependency/seed change
is needed. Existing metadata-only SyntheticUnverified fixtures remain unchanged;
no clean/verified transport, receipt or credential is fabricated.

## Authored checks, not executed results

[SecureFilesRejectionTests](../../tests/Poseidon.Api.Tests/SecureFilesRejectionTests.cs)
authors sixteen API/PostgreSQL cases: six content/scanner failures preserving
earlier accepted content, four header/current-policy failures, two mid-scan
authority/policy withdrawals, one competing upload/spool-ownership race, one
reconciliation/finalizer race, one real request cancellation and one explicit
unlink/recreate ABA regression while the first request's xmin claim is paused.
[S3DocumentIntegrationTests](../../tests/Poseidon.Api.Tests/S3DocumentIntegrationTests.cs)
adds two real-provider context-change negatives, including a change during the
awaited Receiving claim. These are **18 authored cases**,
not an observed pass count.

Existing protocol-error/FOUND/truncation/timeout regressions now assert original
Pending/xmin, unchanged stage audit and private unaccepted spool retained until
explicit reconciliation. Preflight rejects create no spool and may retry the
same intent with corrected headers/current policy; actual byte/scanner failures
need explicit reconciliation/new intent rather than speculative request cleanup.
Existing
successful concurrent upload, real S3 immutable-version download, receipt rollback
and orphan reconciliation assertions remain. The exclusive local-object test now
requires the narrow collision exception, preserving its reparse/foreign-root
negatives. Scripted scanner peers are failure/concurrency test fixtures; they do
not establish real malware efficacy. The existing real clamd clean/EICAR test and
real-provider tests remain required at the whole S1 bundle gate.

**DEFERRED / NOT RUN:** restore, builds, domain/API/PostgreSQL/S3/clamd tests,
OpenAPI/type generation, UI/typecheck/lint, browser journeys, repository/client
security scans and independent exact-target engineering QA. No current passing
check, accepted story point, completed sprint, source/pilot/Marine/legal decision,
production IAM/storage/scanner/retention readiness or GDPR/KVKK claim is made.
The coordinator runs the larger S1 bundle once implemented; canonical progress
must not advance from this authoring checkpoint alone.
