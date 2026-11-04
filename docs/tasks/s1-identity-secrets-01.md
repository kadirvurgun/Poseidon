# S1-IAM-SECRET-01 — identity admission and trusted secret references

Status: **AUTHORED; verification DEFERRED; acceptance false**. This is part of the
larger S1 foundation bundle. No test, build, restore, browser journey, scan or
provider operation has been executed for this package. Prior S0 results are
historical evidence, not acceptance of these changes.

## Controlling source and dependencies

Active baseline: `poseidon-v46.3-20261030`, declared in
[the active source register](../sources/active.json). Exact original source:
[18.01 development backlog](../../analiz/POSEIDON%20GUNCEL%2030.10.2026/06_DEVELOPMENT/18.01_POSEIDON_v46.3_Development_Backlog_TR.xlsx),
SHA256 `09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`.
Bounded source retrieval and original workbook rows were read before authoring.

| Task | Locator / trace | Literal criterion | Dependencies |
| --- | --- | --- | --- |
| DEV-013, 8 SP | `sheet:Backlog/row:14`; `T-S1-001` | OIDC login/logout çalışır; token validation server-side; expired/invalid token state mutate etmez; correlation ID üretilir. | DEV-002, DEV-006 |
| DEV-017, 5 SP | `sheet:Backlog/row:18`; `T-S1-005` | DB/OIDC/object storage secrets vault/reference ile sağlanır; repository ve client bundle secret scan PASS. | DEV-003, DEV-006 |

The original source status cells remain `Not Started` historical source data.
S0 dependencies were accepted separately. This authoring does not accept either
S1 task or increase canonical accepted story points.

## Delivered behavior

[OperatorSecretReferences](../../backend/Poseidon.Api/OperatorSecretReferences.cs)
now owns OIDC secret admission as well as DB reference resolution.
[FoundationIdentity](../../backend/Poseidon.Api/FoundationIdentity.cs) consumes
that admission before registering authentication services.

* Outside `Development` and the isolated `Test` environment, a configured OIDC
  client secret requires a trusted operator reference. A literal is rejected even
  when the remaining federation configuration is incomplete.
* A public OIDC code+PKCE client with no secret remains supported. It never reads
  an implicit credential or ambient fallback.
* An explicit reference must use the existing bounded uppercase environment-name
  format. An empty, malformed, missing or whitespace-valued reference fails
  closed. An OIDC literal plus reference is ambiguous and rejected in every
  environment before reading the provider.
* Existing isolated Development/Test OIDC literals remain supported only without
  a reference. Database connection-string fixture fallback likewise requires
  that no explicit DB reference exists. An explicit missing DB reference returns
  unavailable even in Development/Test; request owners retain their existing
  unavailable response path.
* DB resolution without an explicit reference still reads the existing operator
  `POSEIDON_DATABASE` environment reference. Production never falls back to a
  literal application configuration value. The S3 provider continues to require
  its explicit access and secret references and has no ambient SDK credential
  chain; its existing tests and scans remain required at the bundle gate.
* Resolution errors are fixed strings. Provider exception messages and inner
  exceptions are discarded because they may contain reference names or values.
  No secret value or reference is logged or placed in an error message.
* Registered OIDC options take a startup snapshot. A deliberate new registration
  consumes a replaced operator value; deleting a required reference prevents the
  new registration. This does not claim live refresh of existing provider tokens
  or application cookies.

The existing code+PKCE, signature, issuer, audience, lifetime, nonce, state,
correlation, secure cookie and trusted server membership checks remain in place.
There is no new authentication bypass, password store, identity provider, vault
vendor, production appointment or private preview change. No persistence schema,
seed fixture or migration is needed for configuration admission.

## Meaningful authored checks — not executed

[OperatorSecretReferenceTests](../../tests/Poseidon.Api.Tests/OperatorSecretReferenceTests.cs)
cover environment isolation, malformed and missing explicit references, forbidden
fallback, OIDC literal/reference ambiguity, public clients, explicit-reference
selection, replacement and provider-error redaction. Pure resolution tests use
an injected operator reader, with no process-wide environment mutation.

[FoundationIdentitySecretTests](../../tests/Poseidon.Api.Tests/FoundationIdentitySecretTests.cs)
exercise the real `AddFoundationIdentity` registration and resolved
`OpenIdConnectOptions`, including preserved server token validation, rotation on
new registration, removed references, public-client registration and absence of
an OIDC scheme when federation is unconfigured. Process environment fixtures use
unique per-test reference names and restore their original values on disposal.
Data protection is ephemeral; no network metadata request or provider is used.

[OidcProtocolTests](../../tests/Poseidon.Api.Tests/OidcProtocolTests.cs) retain the
real-host signed protocol matrix: valid, expired, wrong signature, wrong issuer,
wrong audience, nonce, state and correlation. Each rejected callback now submits
an ordinary company command and checks 401, unchanged exact company rows and
versions, unchanged business-audit IDs and unchanged command-receipt count. The
valid-login branch also applies these invariants to an expired protected cookie
and an independently tampered protected cookie. Intended unauthenticated
security observations remain durable and explicitly separate. Server-generated
correlation IDs must be valid, distinct per request and unrelated to the caller's
supplied value; the denied command's persisted observation binds its actual ID.

At the complete bundle gate, execute the real API unit/integration suites,
repository and actual built-client secret scans, OIDC signed journey evidence,
required CI and independent target-commit QA. No deferred check is a PASS.

## Remaining acceptance and production boundaries

DEV-013 needs actual native protocol and signed login/logout/session evidence for
the final S1 target. DEV-017 additionally needs real repository/client scans and
existing DB/S3 tests bound to that target. These changes alone do not establish
complete S1 acceptance, production IdP/MFA/reset/session-revocation rollout,
commercial vault deployment, full HTTP auditing, GDPR/KVKK compliance, human
safety authority or pilot acceptance. All canonical status changes belong to the
integrated phase gate and independent QA.
