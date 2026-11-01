# REG-BOUNDARY-TLS-01 — local corrective gate

## Coordinator manual transport-boundary correction after rejected b2ec2fa

The bounded native author/automatic-repair budget was exhausted and has NOT been reset. The coordinator directly repaired the newly reported GUID terminal-line-character defect on the existing clean b2ec2fa tree; no new native author/model worker was started. Approved baseline93de and all rejected-target history remain unchanged. This candidate still requires independent exact-commit QA and remote CI; canonical8/970SP remains unchanged.

Shared `identifiers.ts` now validates exactly36 characters, the complete case-insensitive .NET GUID shape and nonempty identity. Today, profile and register transports reuse the same guard; the latter two already enforced the same length/nonempty semantics. Today also reuses the existing unchanged complete-string calendar/timestamp boundary, removing its duplicate regex that could accept a terminal newline. No backend/auth/provider/TLS/lookup/source/operational authority changes.

25 additional transport regression cases exercise trailing LF/CR/Unicode line/paragraph separators in vessel id/companyId and Today/Why timestamps, and retain valid mixed-case complete GUIDs. They call real transport decoders through mocked HTTP responses, so they verify response rejection before navigation/domain state rather than mirror the helper implementation. Existing partial-draft, context/dirty form, precision and unavailable/unknown behavior remain covered.

Affected final checks on this manual candidate: full frontend11 files/346 PASS,0 failed/skipped,18.49s; lint exit0; explicit typecheck exit0; production build exit0 (tsc+Vite). Commands use the pinned root Node26.13 tool and locked existing dependencies. Logs are ignored under `.runtime/reg-boundary-tls-01/manual-guid-*`. Graph AST and git diff whitespace checks are recorded at handoff. No unchanged backend135/persistence29/domain61/Python/source/lab/browser suites were rerun for this transport-only change; their exact earlier evidence remains separately scoped. The previous321/298/193 and strict-browser results below are historical, not claimed reruns. No publication, self approval or source/human sign-off.

The earlier rejected candidate reports follow unchanged for traceability.


## Current successor to rejected9c40266 — Today pagination

Date: 2026-11-01. Rejected parent: `9c402664c5920cb1fe413ad035a1d02376259846`. Candidate is the local commit containing this report, supplied in the delivery message. Developer frontend gate passed; NOT ACCEPTED. Independent target-commit QA and required coordinator CI remain pending. Whole318a147 and9c40266 remain REJECTED; approved baseline93de and canonical8/970SP,2/121tasks,0/196 accepted screens are unchanged. Same package/source scope, no delegation, publishing or self-approval, no original budget reset.

The initial worktree was clean. Today now explicitly requests offset0/limit25 and requires those exact response values, even when cards are empty. Vessel-page decoding enforces requested offset equality plus safe integer0–100000 bounds and requires a full25-item page when hasMore is true. Existing exact members, vessel validation, uniqueness and terminal-page semantics remain enforced. The selector navigation fixture now supplies25 distinct synthetic vessels for continuation. REG-BOUNDARY-03/REG-SRC-08 adds23 regression cases: wrong Today offset/limit on empty/populated pages via real transport decoding; exact request parameters; empty/short continuation on first/later vessel pages; invalid offset bounds; valid terminal/full/boundary pages; component error rendering for fabricated empty results. No backend/auth/TLS/fixture-service changes.

Affected checks ran once after code and regressions were complete (npm commands from frontend):

| Command | Result |
|---|---|
| `npm run lint` | exit0; zero ESLint warnings |
| `npm test` | exit0;11 files,321 PASS,0 failed,0 skipped;19.64s; Today42 cases including23 new regressions |
| `npm run typecheck` | exit0 |
| `npm run build` | exit0; tsc + Vite,38 modules;461ms Vite build |
| `graphify update .` | exit0; AST-only3235 nodes/5271 edges/260 communities;12 existing zero-node warnings |
| `git diff --check` | exit0 |

No failed or skipped affected checks. npm emitted its existing user-config always-auth deprecation warning; no config/dependency changes. Current frontend evidence supersedes only the historical298-test frontend run below. Unchanged lab/provider/TLS/backend/persistence/domain/Python/source suites were not rerun for this exact Today-only rejection. Earlier6 strict-TLS authenticated register cases,3 real negative TLS cases,2 provider journeys and backend135/persistence29/React193 remain separately attributed historical evidence, not current-target reruns. No lab was started or private state created, so no profile/service cleanup was necessary in this repair. All prior failures/evidence are retained below. Source/security/production/legal/expert/pilot/human gates remain open as listed below.

Next step: coordinator fresh independent exact-commit review of the complete diff against93de, then required CI. No accepted-point increment. The following report describes the rejected9c40266 candidate's earlier work and results, not approval of this successor.

## Preserved9c40266 implementation and verification

Date: 2026-11-01. Status: developer-validated local candidate, NOT ACCEPTED; independent exact-commit QA and coordinator CI pending. Candidate is the local commit containing this report, parent `318a14788a37b5d30dfd27c6c2eca94745b546b5`; final commit ID is supplied in the delivery message. Whole318a147 remains REJECTED. No publishing, PR, merge, extra agents, self-approval or accepted-point increment. [Source/criteria](../tasks/reg-boundary-tls-01.md); [CORE history](CORE-BUNDLE-01.md). Approved review baseline remains `93de7b6b376fb4592150c5c7b15a8c52e39bd4b3`.

## Delivered behavior

Register decoding now requires the exact actual serialized DTO members at draft, current/history revision, content, field and page levels. Both nullable source members must be present; paired links require a safe positive revision and a distinct nonempty UUID. Draft xmin is1–4294967295; revision/sourceRevision must be positive safe integers. Kind, DRAFT lifecycle, exact provenance strings, uppercase64-character hash, IDs, explicit state/value combinations, controls and field bounds are checked without coercion. Sources cannot have requirement-only instrument; requirements cannot declare title/authority/locator. Partial Missing/Unknown/Known drafts remain supported.

Response offset must equal the request and stay within0–100000; limit must equal requested25; items cannot exceed it and hasMore requires a full page. Unknown/missing metadata and malformed nested entries fail closed. History additionally rejects another ID and non-descending/duplicate revision numbers. Existing profile calendar/timestamp validators were extracted unchanged into `date-validation.ts` and reused: real leap/calendar days, hours0–23, minutes/seconds0–59, explicit Z/offset, maximum offset14:00, fractional precision1–7 and .NET UTC year range. Date.parse is used only after component validation for UTC range. Values, offsets, fractional precision, provenance and profile exact decimal handling are preserved, never normalized.

Both register contexts and the separate signed-provider runner now use `strict-browser.ts`. Playwright1.64.1's official installer downloaded its pinned Firefox157.0/revision1555 from `https://cdn.playwright.dev/dbazure/download/playwright/builds/firefox/1555/firefox-win64.zip`; no npm lock/dependency changes. Previously installed bundles were Chromium only; Windows system certutil is not NSS certutil and was not used. No arbitrary third-party executable was fetched. The installed `nss3.dll` SHA256 is `9ba4d419970951748ff0a9d11e4975e0141d7cb9782c5849229d5d490ced8e0a` (observation of the official installed distribution, not a separately published upstream checksum).

`lab_profile_ca.py` calls public NSS APIs from that distribution: NSS_InitReadWrite, certificate decode/import and CERT_ChangeCertTrust, with SSL VALID_CA|TRUSTED_CA only, then closes NSS before browser launch. Each fresh profile is contained under the explicit private worktree lab directory; the only added certificate is its one `ca.pem`. Enterprise/automatic OS-root import is disabled. Browser ignoreHTTPSErrors is explicitly false; no ignore/SPKI flags, proxy, route-fulfill, clientCertificates or host/user/global certificate changes. BrowserContext API requests separately use process-scoped NODE_EXTRA_CA_CERTS for the same CA; ambient NODE_TLS_REJECT_UNAUTHORIZED is removed in the browser child environment. This client trust does not substitute for browser NSS verification.

Primary references checked: [NSS certutil scoped database/trust documentation](https://nss-crypto.org/reference/security/nss/legacy/tools/certutil/index.html), [Mozilla NSS](https://firefox-source-docs.mozilla.org/security/nss/), public [cert.h](https://github.com/mozilla/gecko-dev/blob/master/security/nss/lib/certdb/cert.h), [certt.h](https://github.com/mozilla/gecko-dev/blob/master/security/nss/lib/certdb/certt.h), [certdb.h](https://github.com/mozilla/gecko-dev/blob/master/security/nss/lib/certdb/certdb.h), [pk11pub.h](https://github.com/mozilla/gecko-dev/blob/master/security/nss/lib/pk11wrap/pk11pub.h) and [Playwright persistent contexts](https://playwright.dev/docs/api/class-browsertype#browser-type-launch-persistent-context). NSS public C APIs avoid the unavailable officially distributed certutil binary.

Coverage is honestly relabeled Firefox desktop1280×720 and Firefox narrow responsive390×844. Firefox does not support isMobile emulation; this is actual responsive layout coverage, not Chromium/mobile-engine/touch-device coverage. All existing manifest assertions are retained. Session cookies come from genuine signed-provider login and stay in memory between isolated contexts; no state JSON is written. Existing response-loss test retains its real API commit via route.fetch followed by connectionreset, then real retry; no fabricated business response or TLS route-fulfill substitution is introduced. Provider login asserts real origin/return, authenticated server session, secure HttpOnly cookie and no localStorage token.

## Exact affected developer gate

Commands below ran from the worktree root except npm commands (frontend) and the pinned installer (tests/Poseidon.Browser.Tests). No backend135/persistence29/domain/Python/source suite rerun was performed.

| Command | Result |
|---|---|
| `npx playwright install firefox` | exit0; official locked browser distribution installed, no dependency update |
| `npm run lint` | exit0; final repeat after the test-only type correction also exit0 |
| `npm test` | exit0; 11 files /298 tests PASS,0 failed,0 skipped;19.23s. Includes104 new register boundary transport cases,15 register component cases (one new malformed-success frozen-command case),73 profile regression cases. The later correction adds only a TypeScript non-null assertion to a test target; runtime test behavior is unchanged. |
| `npm run typecheck` | final exit0; initial nullable test-target error and an unsuccessful correction command from the wrong working directory are retained below |
| `npm run build` | exit0; tsc + Vite8.3.5,38 modules,485ms Vite build |
| `frontend/node_modules/.bin/tsc --noEmit --module nodenext --moduleResolution nodenext --target es2023 --allowImportingTsExtensions --types node --typeRoots frontend/node_modules/@types --skipLibCheck tests/Poseidon.Browser.Tests/lab/register-run.ts tests/Poseidon.Browser.Tests/lab/run.ts` | exit0, including final affected lab source |
| `py -3.13 scripts/oidc_lab.py start` | exit0; existing private configuration reused, no credential/account rotation |
| `py -3.13 scripts/oidc_lab.py register_browser` | final exit0;6 discovered/executed/PASS,0 failed/skipped, same3 manifest cases ×2 actual Firefox responsive projects; separately3 real browser TLS rejection cases PASS |
| `py -3.13 scripts/oidc_lab.py browser` | exit0;2 genuine signed-provider manager/restricted journeys PASS,0 failed/skipped, scoped Firefox trust |
| `graphify update .` | exit0; AST only,3224 nodes/5252 edges/265 communities;12 existing zero-node warnings; no model calls |
| `py -3.13 scripts/oidc_lab.py stop` | exit0; stops only marked worktree app and its compose project; no --volumes/prune or global process termination |

Register results (all PASS): desktop draft-citation5567ms, response-loss-conflict2906ms, restricted-membership2549ms; narrow-responsive draft-citation3355ms, response-loss-conflict2978ms, restricted-membership2166ms. Real negative navigations: app_untrusted_ca (SEC_ERROR_UNKNOWN_ISSUER), provider_untrusted_ca (same), trusted_ca_wrong_hostname (SSL_ERROR_BAD_CERT_DOMAIN against a real loopback HTTPS fixture using the lab leaf). Only bounded fixed result codes/booleans are persisted. No case selector was set; six-case discovery and actual result count match.

Failures remain evidence: first typecheck failed TS2769 at the new test's dynamically indexed target; an attempted correction used a frontend-relative path while already in frontend, made no edit and repeated the same type failure. Corrected with an explicit non-null assertion; final typecheck/build pass. Initial register driver failed before any browser navigation because its expected-private relative path climbed one directory too far:6 discovered,0 executed,1 setup failure; TLS results empty (0 executed,3 incomplete), not passing negative proof. Fixed only the path; final complete gate above passed. These attempts are not skipped tests or waived failures.

Sanitized prior and failed/final counts are preserved separately under ignored `.runtime/reg-boundary-tls-01/`; existing `.runtime/core-bundle/` evidence is untouched. Historical backend135/persistence29/React193 passing results at318a147 and all original approved93de and rejected20f1198 results remain historical; this task does not claim they reran or that historical browser TLS was strict. CORE's original repair budget/history is retained.

## Cleanup, scope and handoff

Every profile is removed in finally after resolving the absolute target, checking its direct containment in the explicit private lab directory and matching its random owner marker; NSS also requires a fresh empty owned profile. Context/session cleanup runs on successful and failed journeys. No persisted session JSON, raw authentication exception, DOM, cookie/storage dump, trace, screenshot or video is created by these direct runners. Only bounded stage/count/code/source-line diagnostics are retained. Final cleanup:0 task-port listeners (5443/8443/55432),0 owned disposable profiles,0 transient register states; both owned lab volumes preserved and credential files remain ignored. Complete staged candidate against93de:70 changed files scanned for private password/client-secret/subject/database/provider values,0 matches. Bounded fingerprint/manifest is ignored under `.runtime/reg-boundary-tls-01/complete-candidate-evidence.json`; no secret values are emitted. `git diff --cached --check` exit0.

This is a locally validated corrective package, not a canonical sprint/release gate. Independent QA must review the complete candidate from93de, including these changes and preserved earlier code. Publishing/merge remain coordinator-owned. Canonical8/970SP,2/121tasks,0/196 accepted screens; production IdP/MFA/session rollout, source admission/DEV-005/008/DEV-026 lifecycle, legal/lookup/role decisions, privacy/retention/export/DB privileges, dependency advisories, expert/permit/pilot/release and human approval gates remain OPEN. No production credential copying/rotation or new live resources.
