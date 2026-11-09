# QA: isolated PostgreSQL transport pooling

- Scope: test infrastructure only; no product permissions, audit behavior, migrations, business assertions or required suite coverage changes.
- Requirement: PR/final-target validation in `docs/architecture.md`, `docs/automation.md`; actual PostgreSQL migration/tenant/audit isolation remains mandatory.
- Every lease retains a generated unique database name. Its transport pool is bounded (`MinPoolSize=0`, `MaxPoolSize=16`). No pool or backend may connect another lease's database.
- Under the existing ownership/lifecycle gate, the exact owned migration-template pool is cleared before `CREATE DATABASE ... TEMPLATE`; the exact released lease pool is cleared before its owned drop. `ClearAllPools` is never used. Unowned or forged leases still fail ownership validation before cleanup.
- Migration-only templates remain migration-only. Per-test migration/idempotent seed execution remains unchanged.

## Executed bounded evidence

Both measurements ran the same authenticated actual-host ten-read audit workload after one warmup. All ten responses, paging totals and unchanged business history were asserted. Counted commands and physical PostgreSQL backend IDs came from a test command interceptor, not estimated counters.

| Fixture mode | Requests | SQL statements | Distinct physical backends | Timed warmed reads |
|---|---:|---:|---:|---:|
| Original `Pooling=false` | 10 | 30 | 30 | 747 ms |
| Per-owned-database bounded pool | 10 | 30 | 1 | 137 ms |

These are single observations under concurrent local gate load, not a full-suite speedup guarantee. Cold assembly timings include unrelated container/model/template initialization and are not a controlled comparison.

Baseline command: repo-local `dotnet run --project tests/Poseidon.Api.Tests -- -method '*Owned_database_pool_profile_of_ten_actual_audit_reads_preserves_business_history' -noLogo`. Result: 1/1 passed, no failures/skips, assembly 32.991 s.

Optimized command: repo-local `dotnet run --project tests/Poseidon.Api.Tests -- -method '*Owned_database_pool_profile_of_ten_actual_audit_reads_preserves_business_history' -method '*OwnedPostgresPoolingTests.*' -method '*OwnedPostgresIsolationTests.*' -noLogo`. Result: 6/6 passed, no failures/skips, assembly 30.907 s.

The six cases cover pool reuse with session reset, distinct database transports, exact-lease cleanup with another lease's idle pool preserved, actual migration-template cloning, idempotent seeds, data/receipt/trigger/audit isolation, and forged/unowned cleanup rejection. Existing append-only audit protection assertions remain. Final integrated-target independent QA and the complete gate remain required; this bounded result does not mark the full suite passed.
