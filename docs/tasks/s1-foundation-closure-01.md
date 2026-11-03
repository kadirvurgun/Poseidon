# S1-FOUNDATION-CLOSURE-01 — controlled foundation delivery contract

Status: **AUTHORING / UNVERIFIED / NOT ACCEPTED**, 2026-11-03. All execution,
final independent QA, CI and publication remain **DEFERRED / NOT RUN**. This
contract records the current combined code and remaining gate, not completed S1.

## Source, literal criteria and acceptance order

Active baseline: [poseidon-v46.3-20261030](../sources/active.json). Original
[18.01 development backlog](../../analiz/POSEIDON%20GUNCEL%2030.10.2026/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx),
SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Backlog rows14–20 define exactly7 tasks and50 SP. Original `Not Started` cells
are source history and are not rewritten to describe current implementation.

| Task / row / SP | Trace | Literal acceptance criterion | Dependencies |
| --- | --- | --- | --- |
| DEV-013 /14 /8 | T-S1-001 | OIDC login/logout çalışır; token validation server-side; expired/invalid token state mutate etmez; correlation ID üretilir. | DEV-002, DEV-006 |
| DEV-014 /15 /8 | T-S1-002 | Her query/write tenant-scoped; cross-tenant testler 403/404 policy ile bloklanır; leakage testleri PASS. | DEV-013 |
| DEV-015 /16 /8 | T-S1-003 | Server-side policy role/vessel/object scope uygular; menu hiding güvenlik sınırı değildir; unauthorized state change audit edilir. | DEV-014 |
| DEV-016 /17 /8 | T-S1-004 | Actor, role, tenant, vessel, before/after, reason, timestamp, correlation ID kaydedilir; kritik write coverage hedefi %100. | DEV-014, DEV-015 |
| DEV-017 /18 /5 | T-S1-005 | DB/OIDC/object storage secrets vault/reference ile sağlanır; repository ve client bundle secret scan PASS. | DEV-003, DEV-006 |
| DEV-018 /19 /8 | T-S1-006 | Type/size/checksum validation, malware hook, signed access, tenant scope ve audit uygulanır; rejected file state mutate etmez. | DEV-017 |
| DEV-019 /20 /5 | T-S1-007 | Cross-tenant, role escalation, vessel scope ve protected object negatif testleri CI'da koşar. | DEV-014, DEV-015 |

S0 prerequisites are separately accepted. S1 acceptance must follow
013→014→015→016/019 and017→018. Existing inherited implementation supports work;
it does not accept a dependency or task without actual final-target evidence.

Original [14.01 ERD](../../analiz/POSEIDON%20GUNCEL%2030.10.2026/05_TECHNICAL_ARCHITECTURE/14.01_POSEIDON_v46.3_Product_Wide_ERD_Data_Architecture_TR.docx),
SHA256 `c3f5e612d69e33d2e5e3a1227644cb9a0ec8937bdcf74b20d2b5a60b968344ab`,
paragraph141 requires before/after **summary** and paragraph143 protected-data
redaction. Original [15.01 API](../../analiz/POSEIDON%20GUNCEL%2030.10.2026/05_TECHNICAL_ARCHITECTURE/15.01_POSEIDON_v46.3_Product_Wide_API_Integration_Specification_TR.docx),
SHA256 `68955985d1823f7f1bc495984f10f55db0d5b8447f424c676844830e15b28837`,
paragraph12 permits metadata-first upload intent. That separate intent command
does not permit deterministic byte/scanner rejection to mutate an existing stage.

## Combined authored behavior and test ownership

| Scope | Actual authoring and remaining evidence |
| --- | --- |
| Identity / secrets | [S1 identity contract](s1-identity-secrets-01.md): production OIDC literals rejected; public code+PKCE/no-secret preserved; malformed/missing/ambiguous explicit references fail closed; explicit DB reference never falls back. Real options registration/rotation/revocation and signed-protocol invalid/expired mutation invariants authored. Existing DB/S3 reference boundaries and full scans still require execution. |
| Trusted mutation context | [S1SecurityContract](../../backend/Poseidon.Api/S1SecurityContract.cs) and [current scope contract](../../contracts/security/s1-current-scope.v1.json): matched server method/route metadata supplies closed operation/reason; required real role attribution precedes current critical handlers. Denials retain safe durable security observations. This catalog grants no permission. |
| Immutable audit | [S1 audit contract](s1-audit-context-01.md): actual parent/receipt scope, explicit tenant-person ownership, trusted actor/role/command/reason and safe changed-field or exact revision/xmin lineage. No raw body/private values/hash/reason copying, guessed vessel, fake role or immutable-audit backfill. Guard/projection and real PG rollback/scope tests authored. |
| Secure transport | [S1 file contract](s1-secure-files-closure-01.md): actual length/hash/format/scanner validation before durable Receiving; fixed failure classes; current rights/policy rechecks; original xmin fence and accepted-file atomicity. Failed Pending quarantine is reconciler-owned; pathname reuse never establishes request deletion ownership. Actual attempt uses pinned provider configuration, including after awaited Receiving save. Eighteen content/scanner/cancellation/concurrency/ABA/provider-context cases are authored, not passing counts. |
| Negative surface | [S1ScopeCoverageTests](../../tests/Poseidon.Api.Tests/S1ScopeCoverageTests.cs): actual EndpointDataSource drift, anonymous/forged IdP and role-less request matrices; all mapped business rows across both synthetic tenants compared in memory while intentional SecurityEvents remain separate. Write-assembly raw/bulk bypass guard authored. Existing per-module vessel/object/revision/privacy/revocation cases remain mandatory. |
| Positive aliases/actions | [S1 journey contract](s1-critical-write-journeys-01.md):8 new actual-host cases target15 missing positive operation IDs, retaining genuine fictional qualification commands, real P256 enrollment, immutable history and exact replay/stale/revoked semantics. Existing human and canonical positives remain separate. |
| Audit consumer | [Strict decoder](../../frontend/src/product/audit-api.ts), [client vocabulary](../../frontend/src/product/audit-command-vocabulary.json) and [drift test](../../scripts/test_s1_audit_contract.py): finite command→reason map, nonempty actual resource/vessel semantics, trusted role/actor/correlation, historical/system distinction and TR/EN rendering authored. They are not frontend verification results. |

The contract defines83 mutation route templates,77 persistence-capable templates
and82 concrete critical operations, including six supported Tug action values.
The remaining six routes require their applicable security observation or denial
semantics. These are static definitions. The actual HTTP/audit observer, strict
evaluator and fail-closed gate are authored and integrated; actual positive
operation capture and measured complete coverage remain unexecuted.
Manual test DB contexts, declared names, replay-only requests or static counts
cannot substitute for successful real HTTP writes with committed attributed audit.

Independent source/static authoring review identified actual spool ownership,
provider-context and decoder cross-field defects. Code and meaningful regression
responses were authored. Final independent rereview and execution remain open;
there is no engineering PASS, current S1 P0=0 assessment or acceptance receipt.

## Whole-bundle gate — planned, none executed

Use the final combined source target and repo-local SDK for local commands;
CI versions/commands remain defined in the [workflow](../../.github/workflows/backend-foundation.yml).
Do not execute per-module loops before the bundle is ready.

1. Review the integrated real HTTP→committed audit observer and fail-closed
   coverage parser, frontend vocabulary/projection and current test helpers.
   Freeze an actual clean commit/tree and source pins. Preserve all121 immutable
   source records,114 non-S1 task objects and13 accepted records/evidence.
2. Restore locked packages/tools and build the whole solution:
   `dotnet tool restore`, `dotnet restore Poseidon.slnx --locked-mode`,
   `dotnet build Poseidon.slnx --configuration Release --no-restore`.
3. Execute actual Domain/Persistence unit suites and
   `python3 scripts/api_test_gate.py --scope unit`. Execute real PostgreSQL
   Persistence integration and API integration with the owned pinned clamd and
   source-built immutable S3 provider; the workflow runs
   `python3 scripts/s3_gate.py --image "$(cat .runtime/s3-gate-image-id)" --child-timeout 1980 -- python3 scripts/api_test_gate.py --scope integration`.
   Actual discovery/native counts, missing/skip failures, safe evidence and all82
   positively observed operation IDs must be checked; definitions are insufficient.
4. Run migration parity with
   `dotnet ef migrations has-pending-model-changes --context PoseidonDbContext --project backend/Poseidon.Persistence --startup-project backend/Poseidon.DbMigrator --configuration Release --no-build`;
   run `python3 -m unittest discover -s scripts -p 'test_*.py' -v` and
   `python3 scripts/verify_sources.py`. The new audit ABI/parser tests must be
   discovered, not merely present as files.
5. Restore frontend/browser locks; run `npm run generate:api --prefix frontend`,
   exact generated API diff, frontend lint/typecheck/component/build and actual
   API/DbMigrator build. Run `python3 scripts/browser_gate.py` and the current
   registered signed-provider journeys with fresh target/source fingerprints,
   explicit manager/restricted actors and invalid/expired/revoked scope cases.
6. Execute `python3 scripts/reviewed_secret_scan.py --mode repository` over all
   tracked inputs and `python3 scripts/reviewed_secret_scan.py --mode client --bundle frontend/dist`
   over actual built output. Maintain independently reviewed source classifications;
   never blanket-exempt controlled documents or weaken patterns for a green result.
7. Diagnose genuine failures before changes and reproduce narrowly under the
   repository CI protocol. Once exact-target evidence is complete, independent QA
   reviews literal criteria and dependencies. Only genuine accepted task records
   may change canonical S1 points. Required head CI checks, normal PR and expected-
   head merge follow; no force push, disabled gate or invented count/authority.

The repository/client scan and actual scanner/provider gates are required
software evidence. Production IdP/MFA/session policy, hosted vault/storage/scanner,
role rollout, privacy/retention/key custody, legal source activation, Marine/Tug
expert decisions, pilot/DR/release remain distinct future operator/human gates.
They neither excuse current S1 software failures nor authorize fake sign-offs.

## Canonical status

S0 remains53/53 SP accepted in13 tasks and merged by
[PR #14](https://github.com/kadirvurgun/Poseidon/pull/14), base commit
`071fbc7c263d87186bde3c89cc7477d92bc63719`. S1 remains0/50 SP accepted; overall
53/970 SP (5.46%),13/121 accepted tasks and0/196 accepted screens. No forecast or
authored-code percentage is promoted to actual acceptance. The
[phase report](../phases/S1-FOUNDATION-20261103.md) and
[canonical status](../project-status.json) record this bounded checkpoint.
