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
    no SERIALIZABLE transaction, so no 40001 handling is needed). The window index, expiry and remaining time are computed
    from the **database clock** (`now()`), the single time source of all instances; a stored row of any other window
    restarts at 1. Count capped at limit+2. Retry-After is bounded to [1 s, policy window].
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

## QA fixes (independent security QA on fe53e398: ACCEPT-WITH-NOTES)
- M1: API-instance clocks no longer choose the window (a fast-clock node could push a partition into a future window and
  lock it out for hours). Window/expiry/remaining come from PostgreSQL `now()`; Retry-After is capped at the policy window.
  Tests: unit `Instance_clocks_ahead_or_behind_cannot_move_a_shared_partition_into_another_window`, PG
  `Instance_clocks_hours_ahead_or_behind_share_the_database_window_and_never_lock_out_beyond_it`.
- M2: the S1 raw-execution guard (`S1AuditedWriteArchitectureTests`) now also forbids `ExecuteReader*`/`ExecuteScalar*`
  and attributes every IL call site to its outermost declaring type. The only allow-list entry is
  `PostgresRateLimitStore` (`ExecuteReaderAsync`, `ExecuteNonQueryAsync`); every forbidden name referenced in assembly
  metadata must be explained by an allow-listed call. Negative test: a probe type in the test assembly is detected.
  The cleanup uses `ExecuteNonQueryAsync` openly again (the earlier scalar workaround is removed).
- L1: expiry cleanup judges `"ExpiresAt" < now()` (database clock), not an instance clock.
- L4: out-of-range `LocalCapacity`, `FallbackSeconds`, `CleanupIntervalSeconds`, `CleanupBatchSize` stop startup.
- N1: the throttling pool is registered as `RateLimitDataSource` (wrapper type), never a bare `NpgsqlDataSource`.
- L2 (local "limited-until" cache) not done: both endpoints are authenticated; noted for later hardening.
- I1 (migration id `20261124110001` also used by invite-email): left for the coordinator's integration renumbering.

## Operations notes
- Fallback and the breaker are **per process**: while the store is unavailable (or the 10-connection pool is exhausted),
  each of N instances enforces its own quota, so the effective limit is up to N × limit, and a flapping store can let both
  the local and the shared quota be used within one window. It is enforced (never fail-open) and metered
  (`poseidon.ratelimit.store.unavailable`, warning 4290). On a hanging store, in-flight requests wait up to 3 s (connect)
  + 2 s (command) before falling back.
- Counter correctness depends on the database clock only; API instance clock skew has no effect.

## Controlled raw-SQL design (AGENTS.md)
Rate-limit counters are technical throttling state, not business mutations, and contain no tenant/actor/PII. The two
statements in `PostgresRateLimitStore` (upsert, bounded expiry delete) are the only raw writers, run outside the audited
business SaveChanges path deliberately (an audit row per attempt would itself be an amplification vector; refused attempts are
still audited once per window by the existing SecurityEvent path). The S1 IL guard allow-lists exactly this store for raw ADO.NET
execution (see QA fixes M2). `RateLimitRawSqlBoundaryTests` pins both statements to `ratelimit."RateLimitCounters"` and asserts
no other backend source (migrations excepted) references that table.

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
| Persistence tests, non-Integration (after QA fixes) | 186/186 passed |
| Domain tests | 1650/1650 passed |
| API tests, non-Integration, non-Performance (after QA fixes) | 514/514 passed |
| `RateLimitPostgresTests` (two instances on one DB, 40 concurrent attempts → exactly 1 `Limited`, ±6 h instance clocks, DB-clock bounded cleanup, DB check constraint) | not run locally (no Docker/PostgreSQL by instruction); fe53e398 version passed in CI run 45036847472; QA-fix version: CI |
| API invitation integration test (Retry-After assertion added) | CI (fe53e398: passed) |
| Repository secret scan | CI (fe53e398: passed) |
| Independent QA | fe53e398: ACCEPT-WITH-NOTES; M1/M2/L1/L3/L4/N1 addressed in the follow-up commit |

Tests authored: `tests/Poseidon.Persistence.Tests/RateLimitingTests.cs`, `RateLimitPostgresTests.cs`,
`tests/Poseidon.Api.Tests/RateLimitingConfigurationTests.cs`, `RateLimitRawSqlBoundaryTests.cs`; updated
`InvitationSessionPolicyTests.cs`, `UserAdministrationInvitationTests.cs`.

## Next step
CI on the branch; independent QA; then fold into the next integration bundle. Operator: provision
`RateLimiting:PartitionKeyReference` secret (≥32 chars) and set `RateLimiting:Store=Postgres` for multi-instance deployments.
