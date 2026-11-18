# README.md

Source: analiz/POSEIDON GUNCEL 30.10.2026/06_DEVELOPMENT/26.01_POSEIDON_Core_v0.1_Source/src/Backend/README.md
SHA256: a1923c008734c394e020e5fc1e6bf1251d5fc9ad0db3e8278202124ace3392f8

## lines:1-40

# POSEIDON metadata draft backend — development slice

Not a production-approved maritime system. No legal validity, readiness, approval,
verification, finalization or operational authority is conferred by saving a draft.
No SDK or dependency installation is performed by this source tree.

## Run and test

Requires .NET SDK 10 and PostgreSQL 17+. Packages are pinned, not yet security certified.
Use a dedicated synthetic database, not a customer database.

```sh
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__Poseidon='Host=localhost;Database=poseidon_dev;Username=...;Password=...'
export SyntheticIdentity__Enabled=true
export Database__Initialize=true
dotnet run --project src/Backend/Poseidon.Backend.csproj --urls http://127.0.0.1:5080
# in-process ASP.NET TestServer; database is actual PostgreSQL, never an emulation
export POSEIDON_TEST_POSTGRES='Host=localhost;Database=poseidon_test;Username=...;Password=...'
dotnet test tests/Backend/Poseidon.Backend.Tests.csproj
```

Without POSEIDON_TEST_POSTGRES, PostgreSQL tests are explicitly skipped, not passed.
Unit validation tests still run. No TCP HTTP listener is needed for TestServer tests.
Schema initialization is explicit and Development-only. Migration 001 is embedded;
it creates dedicated tables and append-only audit triggers. Production migrations
must be applied separately by an authorized migration role, after review. Runtime
role should receive only SELECT/INSERT/UPDATE on drafts, SELECT/INSERT on companies
and idempotency, and INSERT on audit_events, never DDL/TRUNCATE/trigger ownership.
Seed creates only synthetic companies. Authenticated subjects own new draft vessels;
other access comes only from signed vessel_id claims. Session fixture users are not admins.

## Authentication

Default path is OAuth2 access-token validation against OIDC authority using bearer
JWT signature, issuer, audience and lifetime validation. Configure
Authentication__Authority (HTTPS) and Authentication__Audience. Required signed claims:
sub, tenant_id UUID, company_id UUID, repeatable vessel_id UUID; writes additionally
require permission=draft:write. The real IdP and claim-governance contract remain unconfigured.
No X-Tenant header is trusted. All API routes require auth except opt-in synthetic login. Public /health/ready exposes only readiness status and PostgreSQL persistence type.

## lines:41-80

SyntheticIdentity__Enabled=true is accepted **only** in Development (startup fails in
all other environments), as is Database__Initialize. POST /api/v1/dev/session with
`{"fixture":"tenant-a"}` issues random in-process 1-hour bearer tokens and returns
`{accessToken,session:{subject,tenantId,companyId,vesselIds,synthetic:true}}`.
Fixtures: tenant-a, tenant-b, tenant-a-restricted (same company as A, different subject,
no vessel grants). No shared static password or token. GET /api/v1/session returns
session. Restart invalidates synthetic tokens; draft data remains in PostgreSQL.

## API contract

GET/POST /api/v1/vessels and /api/v1/certificates. GET/POST /{resource}/{draftId}.
POST /{resource}/{draftId}/submit always rejects (403). Candidate detail/update and
submit-rejection extensions are implementation decision DEV-API-001, not approved
controlled-source changes. No DELETE: append-only audit and retention/deletion policy
are unresolved; this slice supports create/read/update only, not full destructive CRUD.

Save envelope: `{ "saveMode":"DRAFT", "data":{...} }`.
Idempotency-Key must be UUID. Updates additionally need If-Match: `"1"`.
Save/detail: `{draftId,version,persistenceState:"DRAFT",submissionAllowed:false,data}`.
List: `{items:[...]}` (bounded to first 500; pagination is not yet implemented).
All saves return HTTP 200; save/detail emit ETag. Error `{code,message}`.
Data fields (snake_case):
- Vessel: name, company_id, imo_no, official_no, flag, vessel_type, notes.
- Certificate: vessel_id, type, certificate_no, issuer, issue, expiry, notes.
Omit missing values. Supplied null/blank strings, wrong types, unknown/duplicate fields,
forged server states, invalid UUID/date/IMO format are rejected. No implicit default is
invented. Original supplied strings are preserved. company_id if supplied must match
verified session and existing tenant/company. Certificate vessel_id is mandatory for
security scope; metadata may otherwise be incomplete. Dates are ISO calendar dates,
expiry cannot precede issue. IMO validation is seven-digit syntax only, not registry
verification. Classification text is stored as unverified metadata, not approved enums.
Updates replace the supplied metadata object; omitted values are removed, not patched.

403 forbidden submission/company; 404 inaccessible draft/vessel; 409 idempotency
conflict; 412 stale version; 422 malformed metadata; 428 missing If-Match; 503 database
failure. Retry an ambiguous save using its identical idempotency key and payload.
Idempotency is per tenant+subject; replay also rechecks current entity access.
Draft write, append-only audit and idempotency result commit in one PostgreSQL
transaction. Before/after snapshots, actor, company, tenant, vessel, server time and

## lines:81-120

correlation are recorded; optimistic update predicate prevents lost updates.

## Source traceability / decisions

Read-only candidate sources: 23.03 environment runbook and 05.01 master specification
(.NET10/React18/PostgreSQL17+, modular monolith, server-scoped authorization,
idempotency/versioning, append-only audit); 14.02 Data Dictionary 2316 (fleet-vessels,
certificate metadata and reviewed logical entity recovery), 16.01 candidate API routes.
Physical tables are a new development implementation, not recovered approved schema.
DEV-DRAFT-001: minimal metadata only; unsupported fields rejected rather than silently
lost. DEV-AUTH-001: explicit synthetic Development sessions for local UI proof; no
production bypass. DEV-API-001: draft detail/update and explicit submit rejection.
No status=COMPLETE or inferred validity. No file upload, certificate evidence,
regulatory applicability, operational approval, retention deletion, offline sync,
production IdP provisioning, migration rollout, or deployment is included.

Additional negative-test fixtures: tenant-a-other-company uses the same synthetic-a subject in a different company of tenant A; tenant-a-readonly has no draft:write permission. These exist only in explicit Development synthetic mode.

Integration test safety: before any connection or schema initialization, POSEIDON_TEST_POSTGRES must name database `poseidon_test` or `poseidon_test_<lowercase letters/digits/underscores>`. Other database names cause an explicit failure, not a skip. These destructive fixture/trigger tests must never target customer databases.

### Controlled wire-name alignment / implementation delta

The shared certificate fields exactly follow controlled 16.01 `CertificateDraftFields`:
`vessel_id`, `type`, `issuer`, `issue`, `expiry`. Former unpublished implementation
aliases `certificate_type`, `issue_date`, `expiry_date` are rejected as unknown fields,
not silently translated. `certificate_no` and `notes` are explicitly development-only
metadata extensions. Certificate detail/update endpoints and explicit submit rejection
are development extensions (DEV-API-001), not claims that the controlled API was changed.

PostgreSQL18.6 single-user smoke in a fresh disposable local cluster passed all six
SQL checks: atomic commit, immutable audit/rollback, forced audit failure/no partial
draft, DRAFT-only check, tenant-scoped vessel FK, sequential stale-version predicate.
Four intentionally induced SQL errors were expected; no unexpected error occurred.
This is NOT Npgsql/HTTP persistence or true concurrent-client verification. The three
real PostgreSQL service integration suites remain skipped due to prohibited socket
creation. Sanitized portable test source: `tests/Backend/single-user-smoke.sql` from
product root. Full verification boundaries are in `tests/Backend/VERIFICATION.md`.

## Opt-in identity boundary / BFF foundation (2026-10-27)

## lines:121-138

See [IDENTITY_BOUNDARY.md](../../docs/IDENTITY_BOUNDARY.md) for the current complete
contract and explicit NOT_IMPLEMENTED / NOT_RUN stages. BFF is disabled by default
and cannot start without explicit server ticket storage, data protection and grant
resolver implementations. There is no production implementation or ephemeral
runtime fallback. Cookie authorization does not extend to the bearer API. Tokens
are not saved to cookies; refresh and upstream logout are not implemented.
Real identity actor keys now include issuer+subject; existing real-subject data
would require a separately reviewed transition. Synthetic actor keys are unchanged.
Latest actual verification: 86 passed, 0 failed, 0 skipped, including the original
37 tests and real PostgreSQL integration suites; build has 0 warnings/errors. This
supersedes the older skipped-service result above for this execution only.

### BFF draft routes and React session integration

The next slice adds explicit cookie-only `/bff/api/v1/{vessels|certificates}` routes,
mandatory CSRF for mutations and a server-verified `X-Poseidon-Scope` precondition.
Bearer endpoints remain separate. See [BFF_UI_BOUNDARY.md](../../docs/BFF_UI_BOUNDARY.md)
for opt-in UI behavior, expiry/dirty-edit handling and the isolated browser test boundary.
