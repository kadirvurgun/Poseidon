# FOUNDATION-ACCEPTANCE-BUNDLE-01 — readiness precondition checkpoint F

Status: **SOFTWARE PRECONDITION INCOMPLETE / NOT RUN / NOT ACCEPTED**. Current phase is foundation authoring. The combined gate has not started. This is a preserved local working-tree checkpoint, not a clean validated candidate, canonical sprint, phase acceptance or release gate.

## Reconciliation and actual base

Receiving HEAD was `70e578d5ca63581411ff0ed88868992c787c47b8`; initial `git status --short` and `git diff --stat` returned no changes. Existing checkpoints, normal support-origin receipts, context/session/privacy commits and every meaningful existing test were preserved. No other workspace was modified. Current operator `.runtime/product-batch/config.json` contains base_commit **80b057a6d1830eb6c5fe5e33fc0adfc7ca132c44**; only that metadata value was surfaced. The older user-test-deferral config has93de and is historical. `git merge-base --is-ancestor <current-base> HEAD` exited0; `git diff --numstat <current-base> HEAD` inventories294 changed files before this continuation. This inventories the unverified branch; it does not validate those294 files or claim full integration reconciliation finished.

Read AGENTS, backend/UI and coordination skills, architecture/automation/security/seed/dependency policies, active sources, final Domain/S0 tasks and reports, Today/REG tasks and reports and the actual40-task/272SP ledger. MCP code graph tools were unavailable; local Graphify queries and targeted source reads were used. No agent was delegated. Current source confirms the precondition is false: `CrewStorage.cs` still has legacy Person identity columns; offline cipher/store/conflict helpers have no complete product/server replay consumer; `TugEndpoints.cs` still returns RequestedWindowOnly/UnavailableControlledAssignmentAdapter and no available BP or operational-conflict adapters. This is SOFTWARE work, not a policy-approval excuse.

## Newly authored photo software

DEV-047/T-S7-001, FR46/TC46-crew-profile-photo_ref. Active14.02 Dictionary2316 row1609, SHA3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018; active11.01 Trace2316 row1609, SHAbc3afb0cf721c6cebe7a755602a1adc0cfb8db17d16514a96459403daed6b3d9. Bounded source retrieval: `py -3.13 scripts/query_sources.py "photo_ref" --limit 4`; the actual contract is retained in `docs/tasks/crew-roster-01.fields.json`. Lookup341 explicitly remains UNRESOLVED. Supported configured behavior does not approve a production catalog, legal classification or human identity verification.

* Explicit GET/PUT `/api/v1/vessels/{vessel}/crew/{assignment}/photo` and private `/history` bind a selected immutable photo version to the existing person-owned protected profile. Uploading any version never selects it implicitly. Clear retains immutable selection lineage and means Missing/unknown, never verified identity.
* Current server-owned vessel/person/identity/document/file rights and closed configured file policy precede lookup, history projection/bounding and receipt recovery. Wrong person/vessel/kind, pending/unscanned/missing storage and old policy deny selection. A saved reference unavailable under current context/policy is withheld, not replaced by a newer upload. Current exact-version review uses existing authenticated private transport and download reauthorization. No inline cached private image/browser storage is added.
* Shared profile xmin/base version prevents overwriting either concurrent photo selections or unrelated profile changes. Serializable transaction contains attributed SaveChanges, immutable revision and pinned receipt; replay returns the original revision/xmin without reapplying an old selection. Unknown authority/body fields deny. Security audit uses existing closed action/result vocabulary; generic write summaries omit photo IDs/purpose/private data.
* EF columns/FKs/indexes/DB same-person/photo/clean-storage guard plus immutable history and controlled rollback refusal are AUTHORED. Migration090000/designer/snapshot are hand-authored, not generated/compiled/applied or parity-verified. Existing synthetic profiles retain null photo fields; reseed never manufactures an active photo, scan, object, binding or appointment. Seed audit expectations78/79 remain expectations, unchanged and unobserved.
* Reachable TR/EN UI selects or clears a specific scanned version, displays Missing/Unavailable/SelectedUnverified separately and opens the current exact selected version in private transport. Parent navigation/upload guards include pending photo selection. An uncertain attempt freezes UUID/body; conflict requires explicit discard and fresh data. Denial/context change withdraws private metadata and ignores late responses. No runtime authentication/provisioning or production activation was changed.

## Authored checks, not executed evidence

Six new actual-host/PostgreSQL cases in `CrewPhotoTests.cs`: no implicit/latest/pending reference; select/clear/pinned replay/reseed/history/audit privacy; current membership/policy revocation before recovery; one-effect version race; independent DB wrong-person and immutable revision guards; default-deny policy/CSRF/unknown-authority/cross-tenant behavior. Scanner protocol peers are test fixtures, not real scanner/provider acceptance.

Six new React cases in `CrewPhotoSelection.test.tsx`: explicit selection/exact review/TR-EN; frozen uncertain retry; same-session denial withdrawal; late context response; conflict discard; strict wire scope/state rejection. Mocked component coverage is separate from real integration. One new real signed-host/PostgreSQL/storage/clamd photo select/reload/exact-review/clear case is registered in `register-specs/person-file-cases.ts`. These **13 authored logical cases** are static source inventory; discovered/executed/passed/skipped counts are **unavailable**, not zero-discovered acceptance. Existing tests are preserved.

## Exact verification disposition

| Work | Result for this target |
|---|---|
| Full build/locked restore/generation/model tooling | NOT RUN — software precondition incomplete |
| Domain/all persistence/API/real PostgreSQL/migrations/seeds/audit/races | NOT RUN / DEFERRED |
| React lint/typecheck/component/build | NOT RUN / DEFERRED |
| Ordinary desktop/mobile browser suites | NOT RUN / DEFERRED |
| Separate register config/direct real-provider runners | NOT RUN / DEFERRED |
| Python313/Windows/source-byte/schema/trace/task/ERP/redaction validators | NOT RUN / DEFERRED |
| Independent exact-target QA, source acceptance and required remote CI | PENDING |
| Source-only `git diff --check` | exit0; whitespace inspection only |
| `graphify update .` | final exit0;5478 nodes/11011 edges/382 communities;27 zero-node warnings, HTML omitted above5000 nodes. AST maintenance only. |

No elapsed suite durations or test result hashes exist for this target because no suites ran. The companion evidence JSON contains code hashes and static inventory, not passing check evidence. [Complete40-task candidate inventory with literal source criteria/dependencies and explicit pending assessment](FOUNDATION-ACCEPTANCE-BUNDLE-01.candidates.json). Historical2 tasks/8SP remain unchanged; all new target assessments are pending. Canonical **8/970SP,2/121tasks,0/196accepted screens** and40-task/272SP candidate scope remain unchanged.

## Remaining SOFTWARE before the single combined gate

Protected legacy Person identity storage and existing-person name/employee import update parity; complete controlled Crew rank/department contracts; full durable application offline capture/read/restart/reauth/reconnect/recovery/material conflict/history/atomic domain+offline replay APIs/UI with real-host journeys; actual Tug scoped type/context catalogs, editable versioned plan/assignment/history, real board consumers, BP/operational conflict adapters and complete localization/journeys. The authored photo subpackage still needs generation/model reconciliation and the later combined verification. No claim of full DEV-047 or DOMAIN completion.

S0 remains incomplete: environment freeze/validator, reconciled independently unapproved ADRs, normalized schema plan/validator,10-capability trace/orphan validator, trusted DoR/DoD register/validator, actual observability/redaction, typed integration-only ERP boundaries/negative tests, DEV-008 source owner/version/validation/reviewer criteria and actual configured assignments, and DEV-009 exact source-grounded deterministic golden dataset/consumer tests. Missing source dependencies remain acceptance gaps even if downstream software later passes. No assignment or validation is manufactured.

External source/legal/deployment/human gates remain separately open: source-approved catalogs and lawful-purpose/classification/retention; key custody/storage/scanner/device/time enrollment configuration/qualification; real owner/reviewer bindings where literal source requires; production import reconciliation/backup/freeze/rollback/owner sign-off; verified evidence and authorized safety/legal/pilot decisions. Neither helper software nor a synthetic configuration supplies these decisions.

## Later gate discipline retained

After required software is actually authored: use pinned root .NET10/Node26 and explicit Python313; locked tool/solution/npm restores; reconcile EF model/designer/locks; build actual API and export `/openapi/v1.json`; regenerate TypeScript with locked `npm run generate:api --prefix frontend` BEFORE strict consumer/snapshot checks; release build; actual MTP `dotnet test --project <each actual backend project> --configuration Release --no-build` with real PostgreSQL; EF pending-model check; frontend lint/typecheck/component/build; ordinary browser configs AND the separate register config's identical direct-runner manifest; Python/Windows/source-byte and all source/schema/task/ERP/redaction validators, AST. Inventory every project/config and require nonzero discovery; execute once coherently and rerun only evidence affected by repairs. Prior passing reports supply command examples, never current results.

The safe real OIDC lab driver already performs genuine signed login and transient private sessions through `lab/register-run.ts`; authenticated test-runner DOM/error artifacts remain prohibited. At the real gate, operator setup must provision actual disposable role/capability labels and explicit current synthetic field/device/key/storage/scanner policies. Missing policies/membership FAIL rather than skip. No provider, browser, host or lab was started here. No tokens/cookies/configuration bodies were output or copied. Only exact task-owned processes may later be stopped; volumes/operator data remain preserved.

Next: continue required software in the preserved branch; complete source/criteria/dependency reconciliation and S0; then the single combined gate, independent exact-commit QA/source review and required CI under coordinator control. No local candidate commit before gate evidence, no push/PR/merge/self-approval. This checkpoint deliberately remains an uncommitted reviewable worktree while preconditions remain incomplete.
