# QA-LOW-NOTES-20261119 — bundle 4 low-severity independent-QA notes

Branch `fix/qa-low-notes-20261119` from main `55d5e1f`. Status: locally validated follow-up subpackage (AUTHORED, targeted checks run); not a sprint/release gate. CI and independent QA pending.

## Changes

1. `tests/Poseidon.Api.Tests/EndpointProfileRegistryTests.cs:148` — probe permission regex now `[a-z0-9_.]+`, so permission names with digits are no longer skipped.
2. `contracts/endpoint-profiles.v1.json:259` (NATIVE-COMMAND-IF-MATCH-1 summary) — documents that a 40001/40P01 serialization/deadlock failure not answered by a receipt replay is 409 `version_conflict` (distinct from stale If-Match 412) and DB state-rule rejection is 409 `invalid_state`. Runtime semantics unchanged.
3. `contracts/endpoint-profiles.v1.json:186,214` (NATIVE-COMMAND-STORED-OUTCOME-1 lostReplyRetry/summary) — key binding stated per member: parameter commands tenant-wide (other actor 409), DeclareProfileFieldSource and membership commands incl. import per actor (other actor = independent command, never the stored outcome). Verified against receipt keys `(TenantId, Key)` / `(TenantId, ActorId, Key)`.
4. `backend/Poseidon.Domain/ControlledDocumentPolicy.cs:78,85` — `activeUseOpen` is now a required parameter of `DisposalRequestBlocker`/`DisposalApproveBlocker` (no fail-open default). Persistence callers already passed it; domain tests updated.
5. `tests/Poseidon.Browser.Tests/specs/s2-axe.spec.ts` — new states: fleet list, Today with no vessel selected, workspace search dialog open; same WCAG tag set and zero-violation assertion; axe `incomplete` results attached via `test.info().attach` without failing. `docs/dependency-policy.md` lists axe-core / @axe-core/playwright 4.14.0.
6. `frontend/src/product/EnterpriseChrome.tsx:181` — the conflicting brand `aria-label` was already removed on main (f408876); the tagline `aria-hidden` is now removed and a space separator added, so the link is announced as "POSEIDON <tagline>" (label-in-name still satisfied: the name starts with the visible "POSEIDON"). Link-name queries in vitest and `product.spec.ts` use `/^POSEIDON\b/`; EnterpriseChrome.test asserts the full accessible name.

## Verification (repo-local SDK/Node, 19.11.2026)

| Check | Result |
|---|---|
| `dotnet build Poseidon.slnx -c Release` | exit 0, 0 warnings, 0 errors |
| Domain.Tests | exit 0, 933/933 |
| Persistence.Tests non-Integration | exit 0, 83/83 |
| Api.Tests non-Integration | exit 0, 276/276 |
| Api.Tests `*EndpointProfile*` classes | exit 0, 15/15 |
| `scripts/test_endpoint_profiles.py` / `test_s1_audit_contract.py` (unittest discover -s scripts) | exit 0, 6 + 1 OK |
| frontend lint / typecheck | exit 0 / exit 0 |
| vitest EnterpriseChrome, TechnicalWorkspace, Workspace | exit 0, 3 files, 57 tests |
| Browser.Tests typecheck (no tsconfig; ad-hoc `tsc --noEmit --strict` over changed specs) | exit 0 |

DEFERRED: Playwright run (axe gate incl. new states, product.spec), PostgreSQL Integration tests (no Docker), CI, independent QA. Note: `py -m unittest scripts.test_endpoint_profiles` fails to import `endpoint_profiles` (path); `discover -s scripts` is the working form.
