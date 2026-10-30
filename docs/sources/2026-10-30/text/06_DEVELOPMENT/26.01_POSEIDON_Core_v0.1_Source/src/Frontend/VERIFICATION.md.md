# VERIFICATION.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/src/Frontend/VERIFICATION.md
SHA256: fac170054ab8998fcf1645d43b58317659974c313feeb52cea1cc0e141fbc7f7

## lines:1-38

# Frontend verification — 2026-10-27

Final commands executed from `src/Frontend` with Node 24.22.1 and npm 11.9.0:

| Command | Result |
| --- | --- |
| `npm test` | PASS: 3 test files, 32 tests; Vitest 5.0.5, 3.04 seconds |
| `npm run build` | PASS: TypeScript build + Vite 8.3.5 production bundle; 1,946 modules |
| `npm run typecheck` | PASS: `tsc -b`, no diagnostics |
| `npm audit --json --cache /tmp/poseidon-npm-cache` | PASS: 0 info/low/moderate/high/critical advisories; full result `audit.json` |
| `npm ls --depth=0` | PASS: resolved direct dependencies, no invalid peer dependency tree |

Production output: HTML 0.46 kB; CSS 18.66 kB (5.28 kB gzip); JavaScript 277.58 kB (88.47 kB gzip).

## Covered by automated tests

- Optional incomplete draft metadata, omission of blank fields, malformed IMO, field limits, calendar dates/ranges, localized validation.
- Bearer header, exact draft envelope, quoted version precondition, idempotency headers, HTTP 401/403/409/412/422/503, no automatic mutation retries.
- Rejection of malformed lists and false success responses that do not confirm DRAFT / submissionAllowed:false.
- Turkish/English switch and document language, input labeling, field error association, memory-only session entry.
- Authentication denial without fake workspace, default-hidden synthetic login, explicit synthetic fixture request and visible synthetic-session warning.
- Save to mocked server, return to list, reopen via detail endpoint; certificate metadata save with explicit vessel selection.
- Dirty edit cancel navigation; uncertain save retains exact request key and body and locks edits; stale update blocks overwrite until explicit reload.
- Failed resource switch and revoked access clear stale rows; clicking current section during delayed load does not orphan loading state.

## Limits — not passed or certified

These are unit/component tests in jsdom with mock HTTP only. No runtime API fallback exists in application code. Actual browser launch/visual QA is blocked in this environment per parent task constraints; no screenshot or browser certification claimed. Real .NET/PostgreSQL round trip, real OIDC login/refresh, deployment, full assistive-technology testing and business/legal acceptance were not performed by this frontend task. Lint is not configured; type checking and formatting are not represented as lint.

## Issues fixed during verification

- Default npm cache path `/home/agent/.npm` was absent/unwritable; used authorized writable `/tmp/poseidon-npm-cache`. Package registry itself was accessible.
- Initial stack had vulnerable development tools; upgraded before final audit, then migrated to latest stable stack under the user's updated instruction.
- Independent review found stale-resource display and active-navigation loading race; both fixed with regression tests.

## Canonical wire follow-up

Updated certificate form keys, validation and list display to controlled-source `type` / `issue` / `expiry`. Added exact field-schema regression and asserted a filled certificate-save request uses those canonical names. Final rerun: 32/32 tests, build and typecheck passed. No dependencies changed; previous zero-advisory audit remains the dependency result. Repository source search found no legacy certificate field names in frontend `src`. `certificate_no` / `notes` are documented development extensions.
