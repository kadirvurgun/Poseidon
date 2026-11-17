# VERIFICATION.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/tests/Backend/VERIFICATION.md
SHA256: 078838cf230d343189f2dcdf62f051e6b72a960aa0646af35c9214177587af7b

## lines:1-40

# Backend verification — 2026-10-27

Runtime: SDK 10.0.403 / ASP.NET Core 10.0.14, workspace-local installation.
Packages: JWTBearer and Mvc.Testing 10.0.14; Npgsql10.0.3; Test SDK18.11.0;
xUnit2.9.3 with runner4.0.1. Exact resolved graphs: both packages.lock.json files.

Actual result: **34 passed, 3 skipped, 0 failed**, build **0 warnings / 0 errors**.
Evidence: TestResults/backend-unit-auth.trx. The skipped tests are ALL the real
PostgreSQL persistence integration tests. They were not replaced by an in-memory or
SQLite provider. No assertion of PostgreSQL/API persistence success is made.

Passed coverage: strict metadata/duplicate/unknown/server-controlled field validation,
preservation and no implicit defaults, invalid dates/IMO syntax, draft-only state,
HTTP malformed JSON sanitization through TestServer, production synthetic startup
rejection, production bearer configuration, anonymous/forged tenant-header rejection, missing write-permission rejection, actual bearer
JWT signature/issuer/audience/lifetime enforcement using ephemeral RSA local test keys.
No real IdP or actual customer information used. OIDC metadata is static in JWT tests;
there is no test network request to an external issuer.

Authored but skipped pending a permitted PostgreSQL listener: cross-tenant,
cross-company same-subject and cross-vessel isolation;
certificate isolation/update; real simultaneous idempotent replay and optimistic
version contention; append-only audit and forced-audit-failure atomic rollback.

`dotnet list tests/Backend/Poseidon.Backend.Tests.csproj package --vulnerable
--include-transitive --format json` completed using NuGet.org, reporting no known
vulnerable packages (dependency-audit.json). This is an advisory feed check, not a
security certification.

Commands from product root:

```sh
source ../toolchains/env.sh
dotnet restore tests/Backend/Poseidon.Backend.Tests.csproj --locked-mode
dotnet test tests/Backend/Poseidon.Backend.Tests.csproj --no-restore --logger 'trx;LogFileName=backend-unit-auth.trx' --results-directory tests/Backend/TestResults
# Set POSEIDON_TEST_POSTGRES to a dedicated running synthetic PostgreSQL17+ DB
# to execute rather than skip the three persistence integration tests.
```

Known environment limitation: PostgreSQL18.6 binary/initdb installed by toolchain

## lines:41-78

worker, but creating its private Unix-domain socket is denied (EPERM). Ordinary
reviewed startup escalation then failed in environment wrapper before PostgreSQL
started. No listener is running and no transport workaround was attempted.
`dotnet format` was also blocked by named-pipe socket permission; this does not affect
the successful direct-project compilation and TestServer execution.

Real IdP onboarding, external security review, complete domain/retention rules,
production migration/backup/restore and deployment remain out of this verified slice.

Root-solution restore is valid using sequential execution supported by this sandbox:
`dotnet restore Poseidon.sln --disable-parallel -m:1 --locked-mode`.
The earlier default parallel solution restore failed without diagnostics; direct-project
restore and sequential solution restore succeed. This is not a solution-file defect.

Integration test safety: before any connection or schema initialization, POSEIDON_TEST_POSTGRES must name database `poseidon_test` or `poseidon_test_<lowercase letters/digits/underscores>`. Other database names cause an explicit failure, not a skip. These destructive fixture/trigger tests must never target customer databases.

## PostgreSQL single-user SQL smoke (separate evidence, NOT integration)

PostgreSQL18.6 was run without networking in a fresh disposable `pg-sql-smoke`
cluster. Inspected `TestResults/single-user-sql.log`: migration DDL applied and all six
named boolean checks are true:
- SMOKE_COMMIT: draft, audit and idempotency rows committed together.
- SMOKE_APPEND_ONLY_AND_ROLLBACK: audit UPDATE rejected and preceding draft update rolled back.
- SMOKE_FAILED_AUDIT_NO_PARTIAL_DRAFT: forced audit INSERT error leaves no partial draft.
- SMOKE_DRAFT_ONLY: database CHECK rejects COMPLETE persistence state.
- SMOKE_CROSS_TENANT_FK: composite foreign key rejects foreign-tenant vessel reference.
- SMOKE_CAS_STALE_PREDICATE: sequential stale-version predicate cannot overwrite version2.

Exactly four deliberately induced errors occurred (append-only trigger, synthetic
failed-audit trigger, DRAFT CHECK, foreign-tenant FK). No unexpected SQL error observed.
This validates SQL syntax/constraints and single-session transaction behavior only.
It does NOT validate Npgsql parameter bindings, HTTP persistence, service authorization
queries, service transaction composition, or true concurrent clients. The three real
PostgreSQL service integration suites remain skipped until a permitted listener exists.

Canonical wire correction verified after final rebuild: certificate shared fields are `vessel_id/type/issuer/issue/expiry` per controlled16.01. Four added tests cover canonical preservation and explicit rejection of all three former unpublished aliases. Final result34passed/3skipped/0failed (37total), solution build0warnings/0errors. Portable sanitized SQL smoke source is `single-user-smoke.sql`; it contains synthetic identifiers only, no host paths or credentials.

Raw TestResults logs are local execution outputs and are not bundled with the source. A portable result summary is included at ../../docs/TEST_RESULTS_SUMMARY.json; all tests and the SQL smoke source are included for reproduction.
