**Current bounded repair — AUTHORED / UNVERIFIED / NOT ACCEPTED.** The independent SOURCE-CLASSIFICATION-REVIEW-01 rejection of97a1500 is preserved. Byte-bound source/context identity, exact coordinator-pinned receipt/proposal binding, nested UTF-16 coverage and safe structural evidence are authored with negative tests. The new v2 ledger has160 exact occurrences across104 shared contexts, all unresolved,0 approved; the prior159-proposal ledger remains preserved. All suites/build/typecheck/browser/provider/full scans are DEFERRED. Canonical8/970 unchanged. [Repair report](../phases/FILES-SOURCE-COMPLETION-01-REPAIR.md), [exact reviewed contract](../security/source-review-v2.md). Earlier checkpoint text below is historical and does not define the repaired approval contract.

# FILES-SOURCE-COMPLETION-01 — authoring checkpoint, UNVERIFIED

Date: 2026-11-01. Receiving worktree clean at `0bb2492f8eff66e92109ebad18c8c8c55b64621b`.
That commit's AUDIT-FILES-BUNDLE-01 is **NOT MET**, not approved. Independent engineering
baseline remains `80b057a6d1830eb6c5fe5e33fc0adfc7ca132c44` and its committed review proof.
No predecessor results, private lab material, controlled analysis bytes or hashes are rewritten.
No agents, account/authentication changes, publication, push, PR, merge or self-acceptance.
Esra-first eligibility remains the coordinator's existing policy; this worker does not switch accounts.
Canonical **8/970 SP, 2/121 tasks, 0/196 screens** unchanged. **194/970 is the target**, not acceptance.

## Source and acceptance trace

All locators use `docs/sources/active.json`, baseline poseidon-v46.3-20261030, source root
`analiz/POSEIDON GUNCEL 30.10.2026`. Bounded `query_sources.py` retrieval and exact cited
paragraph/field reads followed graph-first discovery; no code-memory MCP tool was available.

| Criterion | Source / FR / TC | Authored implementation and later verification |
|---|---|---|
| FSC-SEC-01 | 18.01 Backlog row18, DEV-017 / T-S1-005 | Explicit operator reference resolution for S3 credentials; no default AWS credential chain. Official SDK pins/locks. Full repository and actual built-client scans must still execute and pass honestly. |
| FSC-SEC-02 | DEV-017; archived failed report and sanitized evidence | Targeted context inventory of every156 archived repository finding plus6 overlapping changed findings:159 unique current targets,162 provenance observations. Entire-member normalized context SHA256 and exact match fingerprint, path/member/line, baseline revision and substantive justification.60 public-example,55 synthetic-fixture,44 formal-reference **proposals only**;0 approvals. Public examples retain operator reuse/rotation gates. |
| FSC-S3-01..03 | 15.01 paragraphs271–274;14.01 paragraph142; DEV-018 / T-S1-006 | Configurable actual official-SDK S3 adapter, generated keys, conditional writes, bucket versioning, returned version/ETag/SHA256, read-back verification before EF finalization, exact-version authorized download, orphan reconciliation. Real PostgreSQL/API/S3/clamd tests authored. |
| FSC-DOC-01..05 | 14.01 paragraphs49–52,113–115 | Document root plus immutable draft snapshots; tenant/vessel/document/revision FK ownership; optional title/incomplete draft; optimistic append and original receipt replay. Attachments use the actual persisted revision. No controlled publication/effective decision is added. |
| FSC-TYPE-01 | 13.04 paragraphs97–103;26.02 paragraph201, historical implementation reference only | Configured subset of PDF/PNG/JPEG. No mandatory PDF. NFC letters/digits/spaces/underscore/hyphen filenames, one extension,120-character conservative limit; no separators/control/bidi/encoded paths. Up to5,242,880 bytes, bounded streaming/hash, limited signature validation, actual clamd. Signature checks are not full PDF/JPEG parsing or malware efficacy proof. |
| FSC-UI-01 | 13.04 paragraph101;14.02 Data Dictionary2316 rows466–485 | Accessible TR/EN draft/attachment consumers, loading/empty/error/read-only/dirty/frozen retry/revision states. Actual signed-host document-owner browser case authored; React tests are mock UI coverage only. |
| FSC-SCHEMA-01 | 14.01 paragraphs114–115,140–142; seed policy | Migration/designer/snapshot authored; metadata-only synthetic unverified fixtures, add-missing/operator-preservation and tenant/FK/DB immutability tests. Expected initial audited writes28→34; this is an authored expectation, not an executed result. |

Field trace read explicitly:14.02 `Data Dictionary 2316` rows466(record_id),467(vessel_imo),
468(requirement_ref),469(responsible_role),470(record_datetime),471(status),472(evidence_ref),
473(verifier),474(knowledge_type),475(knowledge_id),476(knowledge_title),477(revision),
478(knowledge_effective),479(knowledge_owner),480(knowledge_scope),481(supersedes),
482(knowledge_approval),483(ack_required),484(change_summary),485(notes).
Their IDs are `FR-controlled-knowledge-<field>` and `TC-controlled-knowledge-<field>`.
This delivery supplies **preparation support**, including persisted identity, scope, title,
revision and document attachment references. It does **not** claim the20-field Controlled
Knowledge screen or `/api/v1/controlled-knowledge` contract is implemented/accepted. The source
marks physical mappings/owner and lookup semantics for review. Required approved references,
responsible/verifier/knowledge-owner role appointments, effective dates, controlled types,
approval, acknowledgement and lifecycle remain explicit source gates. Missing data stays
unknown/unverified; the draft has no submit/publish/verify endpoint. The generic16.01 skeleton
describes itself as an unverified candidate refinement gap, not a production-certified DTO.

## Storage and operator configuration

`SecureFiles:Adapter=S3` requires a dedicated operator-owned private spool `Root` outside the
repository/webroot, MaxBytes/MaxPixels, loopback ClamdPort, bounded ScanSeconds/CapabilitySeconds,
RetentionReference/ScannerReference, and AllowedTypes. `SecureFiles:S3` requires Endpoint,
Bucket, Region, AccessKeyReference and SecretKeyReference. Reference fields contain **environment
variable names only**, resolved on the server with the existing bounded resolver. No values
are committed, returned or logged. Bucket/host/keys/version/actor cannot be supplied by uploads.

Production requires HTTPS, explicit credentials, version-enabled bucket and SHA256 checksum
support. `OwnedLoopback=true` permits only a literal loopback HTTP endpoint in Development.
There is no silent local fallback. LocalDevelopment remains Development-only. S3 records bind
provider context and version separately from SHA256; configuration changes fail closed and
require explicit operator recovery. ETags are opaque validators, never SHA256. No S3 URL is
issued: bytes pass through the existing actor/tenant/vessel/file/version/expiry capability
contract and current membership checks, plus immutable document/revision ownership when present.

The actual immutable spool is validated/scanned before S3 PUT; PUT uses If-None-Match and
requires a non-null provider version/checksum. Read-back verifies length, actual SHA256,
version, ETag and provider checksum. EF state/StoredFile/receipt finalization remains one
audited transaction. Receipt failure leaves a Receiving intent and provider orphan, never
a downloadable StoredFile. Reconciliation first commits the xmin fence, then deletes exact
generated-key versions in a bounded operation. Unknown acknowledgement is quarantined;
configuration mismatch/cleanup failure needs later recovery, never speculative deletion.
Historical local PNG intent fingerprints and their exact immutable replay form are retained.

## Pending security review

`docs/security/files-source-classifications.pending.json` is **not** an exception file.
`files-source-context-proposals.json` binds manually inspected fingerprints to the complete
set of inspected member hashes; a new context cannot inherit a proposal. The inventory driver
reads only archived cited inputs/members and nearest exact-rule occurrences, not a new full
repository gate. Both overlapping scan scopes remain in each observation's provenance.
Original DOCX/ZIP bytes are untouched. Canonical newlines prevent Windows/Linux match loss.

The scanner now rejects legacy unbound exceptions, pending/self-review records and changed
contexts. Reviewed exceptions require exact path/member/rule/line/fingerprint/context,
substantive rationale, allowed classification and a separate reviewer/evidence reference.
Review identity/evidence is a **trusted coordinator input**, not proven by a string or by this
scanner. No reviewed exception file is supplied or wired into CI here. Actual exact-target
independent review and operator assessment are required before publication. Publicly documented
values may have been reused by an operator: all60 public-example occurrences retain an open
reuse/rotation assessment. No proposal proves that no real credential was ever exposed.

## Later gate commands and remaining work

All execution below is **DEFERRED**, including restore/compatibility, builds, typecheck,
OpenAPI/types generation, model parity, suites, browser, scanner and provider setup. Tests
were authored, never executed or skipped by this worker. The old gate stays archived as failed.

For real owned S3 tests, the coordinator supplies a reviewed local source-built MinIO image
by exact `sha256:<64hex>` image ID with OCI source `https://github.com/minio/minio` and exact
source revision label. No floating image, archived unpatched image selection, download, pull
or build is performed here. Official release/build instructions:
https://github.com/minio/minio/releases . This is a lab dependency input, not a production vendor.

With real loopback clamd already selected, run the authored driver at the renewed gate:

```text
py -3.13 scripts/s3_gate.py --image sha256:<reviewed-local-image-id> -- <owned-dotnet> test --project tests/Poseidon.Api.Tests --configuration Release --no-build
```

The driver creates random ephemeral credentials privately, an exact labelled disposable
container, Docker-selected loopback port, and an explicit child environment. Missing driver
input fails real integration tests; it is never skipped or replaced with a fake provider.
Child output is suppressed because SDK failure output may be sensitive; only safe exit metadata
is printed. The driver verifies ownership before cleanup and never prunes volumes or services.
Real-host S3 browser configuration/bucket bootstrap must be selected explicitly at the later
gate; the signed document case also works with the preserved configured local adapter. Passing
local-browser behavior alone cannot establish actual S3-browser coverage.

Run locked restore, backend build/domain/PostgreSQL/API/S3/clamd suites, frontend lint/typecheck/
component/build, actual-host OpenAPI export and locked TS generation, EF model parity/migrations/
seed preservation, actual signed/browser owner journeys and full repository/actual-built-client
scans once at the combined gate. A renewed gate must retain crash/race/rollback/revocation/expiry
evidence and distinguish inherited unchanged tests, new real provider tests and scripted faults.
No passing result is asserted for SDK/API compatibility, provider semantics or new tests here.
An absent provider image/bucket setup, unsupported checksum semantics, test failure or incomplete
source coverage remains NOT MET. Then independent QA of the exact checkpoint and coordinator CI;
canonical source acceptance is still separate. No production/privacy/GDPR/Marine/pilot claim.
