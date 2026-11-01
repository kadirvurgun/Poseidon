# SECURE-FILES-01 — authored foundation, UNVERIFIED / NOT ACCEPTED

Owner handoff: coordinator. Date: 2026-11-01. Gate: **AUDIT-FILES-BUNDLE-01**.
User target194/970SP is a delivery target, not an accepted count. Canonical accepted
counts remain unchanged. No product check, scanner scan, build, typecheck, contract
generation, browser, lab or Docker execution occurred in this authoring package.

## Controlled sources and prerequisites

Active baseline `docs/sources/active.json`: `poseidon-v46.3-20261030`. Exact bounded
excerpts, original SHA256 and locators are preserved in
[secure-files-01.sources.json](secure-files-01.sources.json). Source originals were
not changed. These remain controlled candidates, not expert/production approvals.

| Requirement / locator | Scope here |
|---|---|
| 18.01 Backlog row19 DEV-018 / T-S1-006, 8SP, dependency DEV-017 | Secure file transport foundation |
| 18.01 row18 DEV-017 / T-S1-005, 5SP, dependencies DEV-003/006 | Operator environment secret references and repository/client scanning authoring; deployment evidence remains open |
| 18.01 row17 DEV-016 / T-S1-004, dependencies DEV-014/015 | Immutable role-reference snapshot correction; no100% coverage/acceptance claim |
| 18.01 row20 DEV-019 / T-S1-007 | Preserve cross-tenant/role/vessel/protected-object CI negatives and extend to files |
| 18.01 row30 DEV-029 / T-S3-004 | Future evidence graph consumer, not delivered here |
| 18.04 paragraph50 | Checksum/version/malware/type/retention |
| 23.05 paragraph28;23.06 paragraphs51,83 | Upload/download/scan/checksum failures; type/size/immutable references and alerts |
| 14.01 paragraphs141–142 | Append-only attributed audit; binaries outside DB, immutable metadata/hash/MIME/size/reference |
| 15.01 paragraph12 | Metadata-first object storage; hashes/provenance verified server-side |
| 13.04 paragraph101 | Understandable allowed type/size/malware/checksum state |
| 13.02 paragraphs639–644 | Evidence Graph screen identification only; does not specify an approved shared upload form |
| 14.02 Data Dictionary2316 rows1459/1460; Lookup Kaynak Çözümü311/312 | Future scoped photo_evidence/document_evidence references; physical mapping/selection contract explicitly REVIEW/TBD |
| 11.01 2316 Alan v46.3 rows1459/1460 | FR46-work-order-detail-photo_evidence / TC46-work-order-detail-photo_evidence; FR46-work-order-detail-document_evidence / TC46-work-order-detail-document_evidence. Future consumers, not accepted work-order screens |

Read AGENTS, backend/UI skills, architecture, automation, security, dependency and
seed rules. Preserved AUDIT commit3fd26e2 and all earlier code. Clean initial tree
had no partial diff to discard. Before editing, verified exact PR11 merge
`5a2930b17c3895f1d9d20212e0319fff37cf8cd2` contains
`80b057a6d1830eb6c5fe5e33fc0adfc7ca132c44`; both trees were
`868a966ab2ed0fd8816f8bc508569e554be69b35`. Normally merged only that specified
commit into this isolated branch. Supplied4/4 QA and8 CI receipts apply to80b/PR11,
not AUDIT or this package. No reset/rebase/amend/force push or remote-tip selection.

## Authored behavior and acceptance mapping

| Trace | Intended acceptance / implementation | Authored regressions (all DEFERRED) |
|---|---|---|
| SF-01 | Explicit trusted file.read/file.write plus directory vessel read/edit. No generic admin/IdP claims or file presence grants safety authority. Scope precedes list/detail/receipt/capability/bytes. | SecureFilesTests, SecureFileAuthorityTests; existing Foundation/Directory/Workspace/Audit negatives retained |
| SF-02 | Metadata-first Pending intent; server UUID; browser name never builds storage path. One bounded immutable intent per tenant/actor/key; conflict and ambiguous attempt semantics. | API metadata/mass-assignment/traversal/race/xmin tests, component frozen-retry test |
| SF-03 | Stream actual bytes under operator max and declared length; compute SHA256 server-side; match declared expectation; extension+MIME+magic+PNG chunk order/CRC/pixel limit; no decompression/rendering. | Domain byte/format/cancel tests; chunked overflow, matching-hash disguised-content and checksum API tests |
| SF-04 | Real loopback clamd INSTREAM scans the same exclusive handle after hash/type validation. Only exact clean frame succeeds. No runtime always-clean verifier. | Scripted protocol negative/API transaction tests; separately required real ClamdIntegrationTests clean/EICAR |
| SF-05 | Receiving is quarantine, never downloadable evidence. Immutable StoredFile revision1 and FileFinalize receipt commit through audited SaveChanges in one DB transaction after bytes close. | Real PostgreSQL/API receipt collision rollback, one-effect concurrency, immutability and foreign-key tests |
| SF-06 | Actor/tenant/vessel/object/revision/expiry-bound Data Protection capability in request header; live membership/capability checks on access; hash/length recheck before response; no range/public list/URL fetch. | Capability binding/expiry/tamper, API scope/revocation, real-host browser negative access cases |
| SF-07 | Crashes leave Pending/Receiving intents and possibly orphan bytes. Explicit trusted reconciliation transitions to Abandoned using xmin before deleting. No deletion on ambiguous commit acknowledgement. | Reconciliation/finalizer fence, rollback/orphan cleanup and symlink/exclusive object tests |
| SF-08 | Existing vessel workspace TR/EN list/detail/upload/progress/cancel/quarantine/retry/download, parent dirty guard, same frozen key/file/hash on retry, bounded client download integrity. | VesselFiles component/transport tests; standard browser negative test and private real-provider fileCases |
| SF-09 | Append-only metadata/tenant-composite FKs/xmin/migration/snapshot; no raw content/errors/body snapshots; safe audit State/Name-withheld projection. | PostgreSQL owner FK/role snapshot/audit rollback/reseed regressions |
| SF-10 | Idempotent SyntheticUnverified metadata intent per synthetic vessel; no fixture bytes, scanner verdict, clean revision, receipt, credentials or fabricated audit import. | Existing seed tests extended:25→28 actual writes; after one operation26→29; negative assertions preserved |
| SF-11 | Strict audit wire types and complete numeric-string grammar including LF/CR/U+2028/U+2029; no normalization into successful states. | Extended AuditWorkspace transport regressions |
| SF-12 | Operator role-reference IDs snapshotted separately from capabilities. Missing/malformed/unconfigured role assignments remain null; IdP/body labels never local authority. | Resolver revocation/untrusted/malformed tests and PostgreSQL retained snapshot/redaction test |

The passive file subset is deliberately **PNG only**,8-bit RGB/RGBA, no interlace,
IHDR/IDAT/IEND only, valid chunk CRC, no trailing bytes. This is an engineering
restriction pending operator/source-supported type policy, not an invented legal
rule. It does not decode/render raster content or establish evidence authenticity.
PDF, JPEG, SVG, Office files, archives, scripts and other types are denied. Other
object owners, crew/medical/protected reports and operational evidence attachments
are not implemented: unknown owner/role/clean inputs are rejected. No safety
control, readiness, signed decision or evidence verification is activated.

## Storage, scanner, cleanup and deployment gates

`SecureFiles` defaults to unavailable. Only explicit `LocalDevelopment` in
Development can operate. Required operator settings: dedicated absolute Root,
MaxBytes, MaxPixels, ScanSeconds, ClamdPort, CapabilitySeconds,
RetentionReference, ScannerReference. Timeouts/capability lifetimes have technical
bounds1–300seconds; byte/pixel/retention values have no implicit legal defaults.
Root must be outside repository/content/web/lab webroot, with no symlink/reparse
ancestors, service-account-only write access and no independent static server.
The development filesystem adapter assumes exclusive operator ownership; it is
not a portable race-proof adversarial multi-user filesystem or a production
object-store implementation. No provider subscription or production SDK added.

Objects use immutable UUID names and create-new/exclusive handles. The same
object remains in quarantine until the DB transaction commits its clean metadata.
Storage and PostgreSQL are not atomic. A failed/uncertain finalization leaves bytes
without accepted metadata or, if the commit succeeded, a retrievable committed
object. Retry reauthorizes and resolves the same intent. Cleanup does not guess
whether an uncertain commit succeeded. `FileReconciliation.AbandonAsync` requires
an explicitly scoped trusted operator job/context, policy cutoff, bounded batch
and tenant lease; no public cleanup endpoint, scheduled production deletion,
retention expiry or legal-hold behavior is claimed. Deployment of this job and
crash/restore/retention operations remain open.

ClamAV protocol and official Docker references:
https://docs.clamav.net/manual/Usage/ClamdProtocol.html and
https://docs.clamav.net/manual/Installing/Docker.html . Registry metadata read
2026-11-01 identified stable1.5.4 with digest
`sha256:bda39705e291105049c3936e2803ac90b955172af8030f94f05dce1252df6c6d`.
CI authors a gate-only loopback container using this pin and real clean/EICAR
tests; no pull/container/scanner was executed here. Signature freshness, scanner
operator trust, production scanner identity/isolation/limits and monitoring remain
deployment gates. Configured ScannerReference is not evidence that a scanner ran.
Protocol-peer tests prove only protocol/failure logic, never malware efficacy.

## DEV-017 / T-S1-005 explicit closure ledger

| Criterion | Actual authoring / gap |
|---|---|
| DB secret reference | API defaults to real operator process reference POSEIDON_DATABASE; optional SecretReferences:Database selects a bounded environment name. Production has no literal appsetting fallback. Migrator already reads POSEIDON_DATABASE. Isolated Development/Test configuration remains supported. No environment value was read/output during authoring. |
| OIDC secret reference | Optional Authentication:Oidc:ClientSecretReference resolves a real operator environment reference; missing/invalid required references fail with fixed safe errors. Existing public/confidential code+PKCE/cookie flow and existing environment-configured ClientSecret remain compatible. No account, IdP, permissions assignment or live auth setting changed. Production reference/client/MFA/rotation selection is not configured or accepted here. |
| Object-storage secret reference | No production storage adapter selected; no credentials required by the isolated local filesystem adapter. Production uploads fail closed. Actual object-storage provider/secret reference/rotation is a real missing prerequisite, not a fictional vault. |
| Repository scan | scripts/secret_scan.py scans every git-tracked input, including fixtures/docs/builds and bounded DOCX/XLSX/PPTX/ZIP contents/XML text. No auth homes, ignored private runtime/lab files or environment enumeration. Required unreadable/malformed/oversized/zero-byte-coverage inputs fail. No suppression manifest supplied. |
| Client scan | Same scanner validates required index.html and nonempty actual production JS output, scans all output files after frontend build, and fails missing/empty coverage. Source-only scans do not replace this. |
| Redaction/exceptions | Findings expose path/rule/line only; values/raw lines/exception details are not emitted. Exact path+rule+line+match fingerprint+rationale exceptions are supported; no path-wide fixture/docs/build exemption. No controlled originals or hashes edited. Tests cover detection, safe negatives, redaction, missing inputs, malformed archive/XML and changed fingerprints. |
| CI/gate | New repository scanner job; existing Python discovery includes scanner regressions; actual frontend/dist scan follows production build; existing backend unit/PostgreSQL and browser negative CI commands retained. No scanner/test was run now. Actual corpus findings remain UNKNOWN until the combined gate and must be reported honestly. |

DEV-017 authoring is included, but **closure acceptance is pending** real repository
and client scan results plus deployment secret-reference decisions. Pattern
matching cannot prove absence of all secrets or a vault deployment.

DEV-016 role criterion likewise remains unaccepted. Operator RoleReferences carry
bounded actual locally assigned reference IDs, not invented Marine labels or
permission-derived roles. This repo has no approved production role catalog or
configured external-to-local role assignment deployment. A nonnull reference is
not proof of a named operational authority, expert sign-off or100% critical-write
coverage. Historical/configuration gaps remain unavailable. Preserve existing
AUDIT exclusions and security-event separation.

## Next combined gate — all checks DEFERRED / NOT RUN

Run once at AUDIT-FILES-BUNDLE-01: locked restore/build, Domain/persistence/API
unit and real PostgreSQL integration, current migration/designer/snapshot parity,
reseed, existing DEV-019 negatives plus new file/role tests, real clamd integration,
repository secret scan, real-host OpenAPI generation/consumer reconciliation,
lint/typecheck/components/frontend production build, actual built-client scan,
standard browser cases and private strict-TLS signed-provider audit/register/file
journeys. Private writer must explicitly receive file.read/file.write/audit.read
and actual vessel edit scope; restricted actor must remain denied. Gate operator
must configure real disposable storage/scanner/settings and existing safe lab
runner; no auth fixture change or lab run occurred here.

No new PASS, skip, self-approval, accepted point, sprint/release, GDPR/KVKK,
source/pilot/expert/permit claim. Coordinator owns combined execution, independent
exact-target QA, any fixes and later publication. Production provider, retention,
role assignments, secrets/rotation and scanner deployment stay explicit open gates.
