# RATE-LIMIT-01 — shared PostgreSQL-backed rate limiting (22.11.2026)

State: AUTHORED, locally unit-verified; PostgreSQL integration tests DEFERRED to CI; independent QA PENDING. Not a sprint/release gate.
Branch `feature/rate-limit-20261122` (base main 36d5d183). Canonical count unchanged (114/121 tasks, 932/970 SP).

## Source / trigger
- USER-ADMIN-02 open item (`docs/phases/USER-ADMIN-02.md` line 73, `docs/security-baseline.md` USER-ADMIN-02 section):
  invitation attempts were rate-limited per identity **process-local**, reset on restart and not shared across API instances.
- Security baseline "Girişte brute-force/rate limit … gerekir"; no secrets/PII in logs.

## Delivered behavior
- `backend/Poseidon.Persistence/RateLimiting.cs`
  - `RateLimitPolicy` (closed name `[a-z0-9.-]`, permit limit, whole-second fixed window, Unix-epoch aligned).
  - `RateLimitPartitionKeys`: HMAC-SHA256 over length-prefixed policy + parts, key = HKDF(operator secret,
    `poseidon.ratelimit.partition.v1`). Issuer/subject/tenant/actor ids are never stored; only a 64-hex hash.
  - `PostgresRateLimitStore`: one-statement `INSERT … ON CONFLICT DO UPDATE … RETURNING` (row lock under READ COMMITTED;
    no SERIALIZABLE transaction, so no 40001 handling is needed). Older window ⇒ restart at 1; a newer stored window (another
    instance's clock ahead) keeps counting, so a lagging clock never resets a counter. Count capped at limit+2.
  - `LocalRateLimitCounters`: bounded in-process counters (USER-ADMIN-02 QA N4 semantics kept: fail closed when full of
    current-window partitions, only expired entries evicted).
  - `SharedRateLimiter`: shared store when configured; on store failure (Npgsql/DbException/timeout/socket/IO) it falls back
    to the local counters for a breaker interval (default 10 s), logs warning EventId 4290 with policy name + exception type
    only (never the message, which can carry host names) and increments `poseidon.ratelimit.store.unavailable`. Caller
    cancellation and programming errors propagate. Never fails open. Closed-label metric `poseidon.ratelimit.decisions`.
  - `Limited` is observed by exactly one attempt per window/partition (atomic increment), so "audit only the first refusal"
    also holds across instances.
- Entity `RateLimitCounter` → migration `20261124110001_RateLimitCounters`: schema `ratelimit`, PK (Policy, PartitionHash),
  check constraints on hash/policy shape and count, index on ExpiresAt. Tenant-agnostic by design (invitation identities
  have no tenant yet; pilot partitions hash the tenant id in). No seed rows (technical state only, nothing to seed).
- `PoseidonDbContext` refuses tracked `RateLimitCounter` changes: counters never enter the audited business SaveChanges path.
- `backend/Poseidon.Api/RateLimitingRuntime.cs`: `RateLimiting` settings section (strict binding):
  `Store` = `InMemory` (default) | `Postgres`; `PartitionKeyReference` (operator env reference, resolved via
  `OperatorSecretReferences.Resolve`); literal `PartitionKey` only in Development/Test; `LocalCapacity`, `FallbackSeconds`,
  `CleanupIntervalSeconds`, `CleanupBatchSize`. Postgres without key material, ambiguous/literal-in-production keys or an
  unknown store fail closed at startup. Dedicated small Npgsql data source (max pool 10). A non-Development host without the
  shared store logs EventId 4291 once at startup. Background `RateLimitCounterCleanup`: bounded batches (default 500,
  ≤20 batches/tick, every 300 s), `FOR UPDATE SKIP LOCKED`, failures logged by type only.
- Consumers moved to the shared limiter: invitation preview/accept (`InvitationAttemptLimiter`, 10 per 15 min per identity)
  and pilot telemetry ingest (`PilotTelemetryRateLimiter`, 30/min per tenant+actor; its old process-local limiter cleared all
  counters at 10 000 entries, now bounded fail-closed). Every 429 now carries `Retry-After` (delta-seconds to window end);
  problem bodies, codes and the OpenAPI contract are unchanged.

## Controlled raw-SQL design (AGENTS.md)
Rate-limit counters are technical throttling state, not business mutations, and contain no tenant/actor/PII. The two
statements in `PostgresRateLimitStore` (upsert, bounded expiry delete) are the only raw writers, run outside the audited
business SaveChanges path deliberately (an audit row per attempt would itself be an amplification vector; refused attempts are
still audited once per window by the existing SecurityEvent path). The S1 metadata guard (no `ExecuteNonQuery*` in business
assemblies) is unchanged; the cleanup returns its count as a scalar. `RateLimitRawSqlBoundaryTests` pins both statements to
`ratelimit."RateLimitCounters"` and asserts no other backend source (migrations excepted) references that table.

## Not in scope / deferred decisions
- Invitation **create/reissue** and the OIDC `/login` challenge were not rate limited before; adding a 429 there changes the
  public contract (OpenAPI + generated client). Left for a separate contract package.
- The shared store is opt-in (`RateLimiting:Store=Postgres` + operator key reference); production rollout and the key
  secret provisioning/rotation procedure are operator gates. Rotating the key restarts counters (acceptable: ≤15 min window).
- Sliding window not implemented (fixed window kept to preserve the audited "first refusal" semantics).

## Verification (exact, local, repo-local `.tools/dotnet`)
| Check | Result |
|---|---|
| `dotnet build Poseidon.slnx -c Release` | 0 warnings, 0 errors |
| `dotnet ef migrations has-pending-model-changes` | No changes since the last migration |
| Persistence tests, non-Integration | 185/185 passed (incl. `RateLimitingTests` 15/15) |
| Domain tests | 1650/1650 passed |
| API tests, non-Integration, non-Performance | 506/506 passed (incl. RATE-LIMIT-01 unit 11/11) |
| `RateLimitPostgresTests` (two instances on one DB, 40 concurrent attempts → exactly 1 `Limited`, lagging clock, bounded cleanup, DB check constraint) | DEFERRED to CI (no local Docker/PostgreSQL by instruction) |
| API invitation integration test (Retry-After assertion added) | DEFERRED to CI |
| Repository secret scan | DEFERRED to CI (needs the review trust variable) |
| Independent QA | PENDING |

Tests authored: `tests/Poseidon.Persistence.Tests/RateLimitingTests.cs`, `RateLimitPostgresTests.cs`,
`tests/Poseidon.Api.Tests/RateLimitingConfigurationTests.cs`, `RateLimitRawSqlBoundaryTests.cs`; updated
`InvitationSessionPolicyTests.cs`, `UserAdministrationInvitationTests.cs`.

## Next step
CI on the branch; independent QA; then fold into the next integration bundle. Operator: provision
`RateLimiting:PartitionKeyReference` secret (≥32 chars) and set `RateLimiting:Store=Postgres` for multi-instance deployments.
