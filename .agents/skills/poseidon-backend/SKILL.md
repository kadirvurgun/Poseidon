---
name: poseidon-backend
description: Implement POSEIDON .NET domain and API tasks with authorization, evidence lineage, offline command handling, and backend tests.
---

Read the assigned requirement and its canonical source locator, then the relevant contract. Use the knowledge graph to locate affected aggregates; use [architecture](../../../docs/architecture.md) for domain constraints.

Implement within the owning module. Enforce tenant/vessel/job scope server-side. Keep Completed/Verified/Effective separate and preserve authorized human safety decisions. Store evidence binaries in object storage and immutable metadata in PostgreSQL. Mutable aggregates require explicit concurrency handling; offline commands require idempotent receipt and material conflict handling.

Use EF Core migrations and add idempotent synthetic seed coverage with each operational entity; preserve operator edits and tenant-correct relationships. Do not fabricate verified evidence/credentials or signed decisions as seed. Follow [seed rules](../../../docs/seed-data.md), [dependency policy](../../../docs/dependency-policy.md) and [security baseline](../../../docs/security-baseline.md). User/system actions require attributed audit; passwords use Identity hashes; secrets and sensitive PII stay out of logs. EF write audit does not cover HTTP reads or failed logins by itself.

Write unit tests for domain rules and state transitions. Write integration tests against real PostgreSQL for persistence, transactions and cross-tenant/role denial; use the actual API host for endpoint behavior. Preserve OpenAPI compatibility or update consumers in the task scope. Provide changed behavior, requirement IDs and executed test results to QA. Missing test infrastructure is unfinished work, never a passing check.
