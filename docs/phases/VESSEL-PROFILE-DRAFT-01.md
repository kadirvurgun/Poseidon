# VESSEL-PROFILE-DRAFT-01 — local engineering package

Status: local integrated developer gate passed, **not accepted**. Independent target-commit QA is a separate later gate. No push, PR, merge, expert approval or canonical screen acceptance is authorized for this worker.

Baseline: clean isolated branch `agents/9ff6b20f711a4809a7bf5a1a3ced7172` at `abd9445`. No prior partial edits were present. DIR evidence is historical local evidence: 98 backend / 63 React / 16 Playwright / 127 Python, then deterministic no-op repair `2443596` and documented API51 PASS in `abd9445`. Initial independent review rejected `e7d5b83`; fresh independent review remains pending. These counts do not verify this package.

## Controlled source and scope

Active source is `docs/sources/active.json`, updated 30.10.2026. Paths below are under `analiz/POSEIDON GUNCEL 30.10.2026/`; older 26.01 archives are not current implementation/test evidence. Retrieval used the explicit Python313 interpreter and bounded `query_sources.py`, followed by targeted cited rows/paragraphs. No whole chunk file was loaded into model context. MCP graph tools and local `graphify-out/graph.json` were absent at initial discovery.

| Source locator | Implemented meaning / traceability |
|---|---|
| `06_DEVELOPMENT/18.01…Backlog_TR.xlsx`, Backlog row21 | DEV-020 / T-S2-001: versioned IMO/flag/class/ship type/GT/build/trading/cargo context, visible provenance and freshness |
| Same, row22 | DEV-021 / T-S2-002: visible server vessel/tenant context; authorized vessel switch; unsaved form guard |
| `05_TECHNICAL_ARCHITECTURE/14.02…Data_Dictionary_TR.xlsx`, Yönetici Özeti row19; `04_UX_PRODUCT_DESIGN/13.06…Master_Katalogu_TR.xlsx`, Yönetici Özeti row18 | POL-DEFAULT-01: incomplete drafts allowed; no implicit Yes/No; no submit with missing mandatory data |
| `05_TECHNICAL_ARCHITECTURE/14.01…ERD_Data_Architecture_TR.docx`, paragraphs83–90 | Shared Vessel root, common profile dimensions/build_date/trading_area, distinct legacy TugProfile |
| `14.02…`, Data Dictionary 2316 row252 | IMO: seven ASCII digits when Known; FR/TC-fleet-vessels-vessel_imo |
| Same, row259,263,271,272 | Flag, ship_type, class_society, class_no: declared text only; no approved lookup catalog; FR/TC-fleet-vessels-{flag,ship_type,class_society,class_no} |
| Same, rows264–269 | gt,nt,dwt,loa,breadth,draft: numeric, nonnegative when Known, zero preserved; FR/TC-fleet-vessels-{gt,nt,dwt,loa,breadth,draft} |
| Same, row270 | build_date: actual ISO calendar date when Known; FR/TC-fleet-vessels-build_date |
| `14.01…`, paragraph87; `18.01…`, row21 | Trading area and cargo context declarations; no invented cargo/trading catalogs |
| `14.02…`, Screen Coverage 196 row86; `03_REQUIREMENTS_REGULATORY/11.01…Traceability_Matrix_TR.xlsx`, 137 Ekran row86 | Fleet field/screen contract remains candidate/BA review, not accepted screen inventory |
| `04_UX_PRODUCT_DESIGN/13.02…196_Ekran…docx`, paragraph94 | Fleet Overview navigation context; implemented within existing fleet workspace |
| `04_UX_PRODUCT_DESIGN/13.04…Design_System…docx`, paragraphs86,97–103,110–114 | Unknown is not NotApplicable; draft separate from submit/verify; authoritative server validation; TR/EN, labels/errors/focus and keyboard |

Declaration, source reference and source-observed-at are package provenance metadata implementing row21, not approved lookup/evidence records. Source observation requires an explicit offset or Z when Known and is normalized to UTC. Missing and Unknown remain distinct; neither is defaulted to system save time. The 500-character text bound and 32KiB command bound are structural implementation limits, not approved operational mandatory rules. Decimal values retain source precision; no invented maximum tonnage or ship-type codes. Field mandatory/lifecycle policy is unresolved, so this package has **no submit, Verified, Effective, permit activation or operational readiness** transition. Documents49/50 do not confer lookup/expert approval.

## Behavior and acceptance criteria

- Fleet detail opens an actual profile editor/view/history for its selected Ship/Tug. The profile response supplies trusted tenant/vessel context and edit capability; server scope/grants authorize every request, including history and receipt replay. No body actor/tenant/vessel/capability binding exists. Unknown JSON fields are rejected.
- Fixed typed text/number inputs explicitly represent Missing/Unknown/Known. Blank text normalizes to Missing. Known numeric zero and reusable boolean false remain Known. Undefined states, inconsistent state/value pairs, malformed types/IDs, text bounds, IMO/date/timestamp formats and negative measurements are rejected safely.
- EF migration adds mutable tenant/vessel draft with xmin and append-only immutable revisions. Each successful save, including unchanged content, creates one revision with actual save time and enforces expected version in SQL. Transaction includes business changes, generated attributed audit and durable command receipt. No business SQL/ExecuteUpdate/Delete bypass exists.
- Normalized payload/vessel/base version fingerprints prevent same-key payload reuse; identical retries return fresh scoped state without a second effect. Revocation is checked before receipt lookup. Failed mutations retain no business history/audit/receipt; observed attempt/denial security events survive separately. Fixed ProfileRead/ProfileHistory/ProfileSave codes retain unknown-code rejection.
- Existing synthetic VesselProfile/TugProfile remain unchanged. Import creates Missing/Unknown regulatory draft fixtures marked Synthetic with no source observation date or approved catalog facts. History/audit originate from actual import operations. Reseed preserves operator edits and versions. Three new draft+revision pairs legitimately increase initial audit count from11 to17; old audit assertions are updated for those six actual additions.
- TR/EN reusable RequiredDataField supports text/number/boolean with explicit state and no implicit boolean default. Profile UI has load/empty/error/401/403, read-only view, field errors, version history, dirty navigation/browser unload protection, conflict retention with explicit discard/reload, frozen uncertain payload and same-key retry with duplicate submission protection. There is no runtime mock or fake login.

## Authored verification map

- Domain `RegulatoryDraftPolicyTests`: incomplete normalization, Unknown/zero, illegal state/value/type/length/date/timestamp/range handling; existing RequiredDataPolicy boolean-false cases retained.
- API `RegulatoryDraftTests` (shared existing DirectoryWorkspaceTests real PostgreSQL/API fixture): Ship/Tug creation, draft+immutable history+audit, no-op/stale/replay, deterministic SELECT/competing-commit race, same-key same/different payload races, initial-save race and host restart, wrong vessel/foreign tenant/grants/revocation/history/replay, mass assignment spoofing, CSRF/anonymous/viewer, validation, receipt-failure rollback, compound tenant FK, reseed preservation and DB/EF history mutation denial.
- React `RegulatoryProfile.test.tsx`: actual request shape/CSRF/version/key, Missing/Unknown/zero/false, safe field errors, read-only/empty/loading/error states, frozen uncertain retry across language changes, no duplicate submit, retained conflict and explicit reload, malformed/wrong-vessel contract denial, fleet integration and dirty navigation. Mock coverage is explicitly labeled.
- Existing backend/React regressions remain. Playwright adds real anonymous profile read/history boundaries and fail-closed mutation when the setup host lacks audit DB. It does **not** test authenticated external IdP/TLS browser login. No production test-auth hook was added.

## Integrated gate

All implementation and test authoring preceded build/test execution, per user cadence. Authored checkpoint: `40e5a11`. Its initial NOT RUN status is historical, not current evidence.

Actual commands, from this isolated worktree, with output in local `.runtime/profile-*.log`:

```powershell
dotnet tool restore
dotnet restore Poseidon.slnx --locked-mode
# Initial NU1004 identified the new Persistence -> Domain project dependency.
dotnet restore Poseidon.slnx --force-evaluate
dotnet restore Poseidon.slnx --locked-mode
dotnet build Poseidon.slnx --configuration Release --no-restore
npm ci --prefix frontend
npm ci --prefix tests/Poseidon.Browser.Tests
# Run actual built API in a separate task-owned terminal; stop after export.
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/Poseidon.Api --configuration Release --no-build --no-launch-profile --urls http://127.0.0.1:5286
Invoke-WebRequest http://127.0.0.1:5286/openapi/v1.json -OutFile contracts/openapi.v1.json
npm run generate:api --prefix frontend
# Copy the freshly exported strict snapshot into test output.
dotnet build Poseidon.slnx --configuration Release --no-restore
dotnet ef migrations has-pending-model-changes --context PoseidonDbContext --project backend/Poseidon.Persistence --startup-project backend/Poseidon.DbMigrator --configuration Release --no-build
dotnet test --project tests/Poseidon.Domain.Tests/Poseidon.Domain.Tests.csproj --configuration Release --no-build
dotnet test --project tests/Poseidon.Persistence.Tests/Poseidon.Persistence.Tests.csproj --configuration Release --no-build
dotnet test --project tests/Poseidon.Api.Tests/Poseidon.Api.Tests.csproj --configuration Release --no-build
npm run typecheck --prefix frontend
npm run lint --prefix frontend
npm test --prefix frontend
npm run build --prefix frontend
# From tests/Poseidon.Browser.Tests:
npx playwright install chromium
$env:POSEIDON_API_PORT = '5288'
$env:POSEIDON_UI_PORT = '4288'
$env:DOTNET_ENVIRONMENT = 'Development'
npm test
# From repository root:
& 'C:/Users/avurgun/AppData/Local/Programs/Python/Python313/python.exe' -m unittest discover -s scripts -p 'test_*.py' -v
& 'C:/Users/avurgun/AppData/Local/Programs/Python/Python313/python.exe' scripts/verify_sources.py
graphify update .
git diff --check
```

The ordinary-path source verifier failed on a long Windows path in this nested worktree. The same verifier, unchanged source bytes, passed with an extended-path root:

```powershell
@'
from pathlib import Path
import sys, json
sys.path.insert(0, str(Path.cwd() / 'scripts'))
from verify_sources import verify
print(json.dumps(verify(Path('\\\\?\\' + str(Path.cwd())))))
'@ | & 'C:/Users/avurgun/AppData/Local/Programs/Python/Python313/python.exe' -
```

| Gate | Actual result |
|---|---|
| Locked restore after project graph regeneration | PASS; zero resolved package version changes |
| Release build, including timestamp correction | PASS; 0 warnings/errors |
| EF migration model parity | PASS; no pending model changes |
| Domain unit | 27 PASS, 0 failed/skipped |
| Persistence unit + real PostgreSQL | 25 PASS, 0 failed/skipped |
| API + PostgreSQL + OIDC + strict actual OpenAPI snapshot | 65 PASS, 0 failed/skipped (final rerun 2m42.739s; initial63 PASS/2 FAIL corrected) |
| React component/transport | 76 PASS, 0 failed/skipped |
| Frontend lint/typecheck/build | PASS |
| Chromium desktop/mobile | 16 PASS, 0 failed/skipped |
| Python regressions | 127 PASS, 0 failed/skipped |
| Source byte/chunk verification, extended root | PASS; 269 files, 34,958 chunks, 0 mismatches |
| Graphify AST update | PASS; 2,774 nodes / 4,251 edges / 226 communities; no model call |
| Whitespace check | PASS |

Initial gate corrections: one invalid Testing Library `getByRole` option in an authored test was removed; typecheck then passed. Two API equality regressions caught first-save timestamps retaining 100ns precision while PostgreSQL stores microseconds. Production save time now uses microsecond precision before history/receipt/response, preserving equality on fresh read and replay. Assertions were retained unchanged. Full API rerun passed65/65 after a fresh zero-warning Release build. No test was skipped, deleted or weakened.

Actual host export added profile GET/PUT/history to `contracts/openapi.v1.json`; `frontend/src/generated/api.d.ts` was generated using the locked tool. Consumers use schema aliases; no handwritten generated DTOs were added. Lock changes add only the Domain project reference; package resolved versions are unchanged.

Local derived logs: `.runtime/profile-{domain,persistence,api,api-final,react,browser,python,typecheck,ui-build,final-build,sources,sources-longpath,graphify}.log`. These are local diagnostics, not canonical approval evidence. Browser16 comprises4 real connection/retry,6 mocked ops,6 real anonymous/setup/keyboard. Authenticated forms have component and actual API/PostgreSQL coverage only. Real external IdP/TLS browser acceptance remains OPEN.

Unresolved: `npm ci` reports13 moderate frontend advisories with existing pinned dependencies (browser installation reports0). No new npm dependencies or forced upgrades were introduced; dependency remediation/release review remains open. All canonical dependencies, lookup/role/mandatory/lifecycle/expert gates remain open. This is local developer validation only.

Current phase: coherent local draft-profile package, canonical S0 exits and S2 acceptance OPEN. Canonical progress unchanged: **8/970 SP=0.82%; 2/121 tasks; 0/196 screens**. Canonical dependencies, operational role catalogs, approved lookups, human mandatory/lifecycle decisions and real IdP/TLS remain OPEN. Next: independent QA of the final local target commit by coordinator; no self-acceptance. Publishing/merge remains with coordinator and outside this worker's instruction.
