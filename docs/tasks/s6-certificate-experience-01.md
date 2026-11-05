# S6 certificate experience — source package 01

Owner: UI author. Status: SOURCE_AUTHORED / VERIFICATION_DEFERRED. No acceptance claimed.

## Scope and controlled sources

This package implements the enterprise certificate command center and renewal preparation consumer for DEV043 / T-S6-001 (original Backlog row 44, 13 SP) and DEV044 / T-S6-002 (row 45, 8 SP). These two tasks total 21 SP. Canonical S6 remains nine tasks / 87 SP / zero accepted; this package does not complete S6.

- Current baseline: `docs/sources/active.json`, poseidon-v46.3-20261030. Original 18.01 workbook SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`, Backlog rows 44–45. Original statuses remain immutable.
- 14.02 SHA256 `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`, Data Dictionary 2316 rows 33–40 (vessel document declaration), 1634–1657 (owning crew metadata), 1708–1717 (center), 1721–1723 (survey dates), 1736–1755 (renewal). Unresolved lookup/authority fields do not become approved catalogs.
- 13.02 SHA256 `b0b5e9fb3cad5137d76a4de912c3a60351b667d0aeb08252856e3a9445928b45`, paragraphs 975–992: command center, survey and renewal screen contracts. Prototype 12.01 screen metadata confirms center ten fields/eight actions and renewal twenty fields/ten actions; counts do not confer write authority.
- 13.04 SHA256 `83489a0f0752e22410d7534cdbe0b89726f00012d869e1b868e8127f5bcc573c`, paragraphs 29–34, 80–89, 110–114, 124–131: context hierarchy, unknown states, language/accessibility, Why and responsive layout.
- 11.01 SHA256 `bc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9`, matching196 rows 160/162 and v46Delta row 25; 15.01 SHA256 `68955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837`, paragraphs 170/1427 and 1617–1627: owning workflow, audit, exact retries and current authorization.

Discovery used the scoped existing Graphify query before targeted source reads. The MCP graph service was unavailable. Source retrieval stayed bounded; historical runtime archives are not execution evidence.

## Delivered behavior

- Actual center capabilities select the permitted vessel/crew/all scopes and the exact server crew purpose. App URLs cannot inject purpose or person identifiers. Crew details open the existing owning assignment route; no second credential writer or broad person lookup is introduced.
- Authorized page counts and calendar filters are visibly scoped to the returned page. Calendar VALID/DUE/SURVEY_WINDOW/EXPIRED remains separate from unresolved qualification. Missing critical-gap coverage stays unavailable; clean transport and dates do not imply verified evidence.
- Vessel declaration forms use shared enterprise headers, grids, labeled action groups, shadcn buttons and responsive layouts. Numeric declarations retain exact strings. Missing optional values stay null. File selection binds an exact scoped clean-transport revision/checksum and changes its matching metadata reference together.
- Renewal preparation uses actual owner options, source due/survey dates, three-valued survey/document/action inputs, exact original certificate pins, exact replacement pins and optional evidence pins. No dates or approval values are inferred from an old certificate. A linked replacement draft never supersedes the original.
- Separate immutable history and source details expose revision, source version, hashes and supersession lineage only when returned. Source-derived temporal and authority states remain distinct.
- Dirty forms guard internal/external/history navigation. Identity changes withdraw private retained fields and prohibit transferring uncertain commands. Unknown outcomes retain the original idempotency key/body/base and remain unknown after a later denial. Confirmed writes first show their immutable outcome; a separate current-head GET must establish current editing rights.
- Actual App routes and the closed authorized navigation/search/lens consumers include center and renewal in the review group. Backend destination admission remains owned by the backend/root integration.

## Contracts and traceability

The consumer follows source-owned `CertificateEndpoints`, `CertificateReader`, `CertificateWriter` and `VesselCertificates` at backend checkpoint c64c9cb4871ab030a099fb47d402b8e884c97a5a. Renewal HTTP/content/provenance follows coordinated source checkpoint 1d70592c3c950adc257df06a008147bbc43a16fa. No endpoint or mutation payload is invented. The UI-only checkpoint does not assert these backend objects are merged into this author branch.

| Criterion / test trace | Authored evidence | Acceptance |
| --- | --- | --- |
| S6-043-UI-01 scope, calendar vs qualification, unavailable coverage, strict current/pin contract | `certificates-api.test.ts`, `CertificateWorkspace.test.tsx` | OPEN / NOT RUN |
| S6-043-UI-02 source/file lineage, private crew fields excluded from vessel form, null vs exact numeric text | `CertificateMetadataForm.test.tsx`, `CertificateDocumentPicker.test.tsx` | OPEN / NOT RUN |
| S6-043-UI-03 dirty guard, identity withdrawal, no command auto retry | `CertificateWorkspace.test.tsx` | OPEN / NOT RUN |
| S6-044-UI-01 renewal original/replacement exact pins, immutable outcomes, no supersession from draft | `certificates-api.test.ts`, `CertificateRenewalForm.tsx`, `CertificateWorkflowHistory.tsx` | OPEN / NOT RUN |

All paths above refer to [frontend/src/product](../../frontend/src/product/). Fixtures are explicitly synthetic and unverified; they are not real authority or integration evidence.

## Remaining work and exact verification limits

Component/contract test sources are authored. Tests, build, typecheck, lint, browser, contract generation and lab journeys have NOT RUN under the active larger-bundle cadence. Only source inspection and whitespace review support this handoff; no runtime geometry, contrast, server authority or passing regression claim is made.

Source-qualified verification and controlled renewal completion are still unsupported by this consumer; completion is explicitly unavailable. Future additive source contracts need coordinated consumers and independent gate evidence. Real-host browser source journeys, fuller renewal interactions, current authorization races and visual/accessibility acceptance remain open for the larger delivery package. No offline storage or grant is inferred for certificates.

## Follow-up 02 — document assistance boundary

After the clean first UI checkpoint 0252b19d599a9486f4666bb5915f10874301049b, normal merges brought in actual extraction checkpoint 704aff0883ec5dd7a02f73a5741a73e12be48e71 and renewal HTTP checkpoint 1d70592c3c950adc257df06a008147bbc43a16fa. Additive registration/inventory conflicts preserve both owning workflows; the combined 104 mutation routes / 96 critical routes / 106 operations are source definitions only, not observed gate results.

The separately owned extraction component is mounted only against a saved vessel certificate with a current editable owning head. It remains mounted while offline, hidden or denied, allowing its own withdrawal to hide private suggestions without losing its retained uncertain command. Independent parent guard channels aggregate ordinary form and document work; retained suggestions disable ordinary editing/saving, while ordinary unsaved/frozen/conflicted fields disable new document confirmation. A confirmed child outcome installs a read-only immutable receipt and rereads current head without clearing another dirty form or rebasing old inputs.

S6-043/045-UI-04: `CertificateWorkspaceExtraction.test.tsx` authors child-boundary regressions for mutual write exclusion, beforeunload protection and retained child work across offline withdrawal. These are explicit component-boundary probes; actual extraction security tests remain with `CertificateExtractionReview.test.tsx`. S6-043-UI-05: `CertificateWorkspace.test.tsx` authors a delayed-read identity-switch regression. Parent source publication and post-save current reads recheck the actor/tenant boundary before displaying their result. All added tests remain NOT RUN.
