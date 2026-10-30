# POSEIDON agent instructions

## Authority and delivery

The user authorized autonomous development, branch pushes and pull requests for https://github.com/kadirvurgun/Poseidon. Merge only after required checks and independent QA pass for the target commit. Keep normal Git history; never use force push as a routine retry. Development authority does not grant an AI authority to make ship/tug safety decisions or manufacture pilot/expert sign-offs.

Read docs/architecture.md, docs/automation.md and the assigned requirement source. Use repo skills under .agents/skills/ for backend, UI and independent QA tasks. Each task needs a source locator, acceptance criteria, tests and traceability IDs. Do not mark work complete with missing test projects or skipped checks.

Use EF Core and migrations for persistence. Add idempotent synthetic seed fixtures and relevant tests with each operational entity; never seed fake verified evidence, signed safety decisions or credentials. Follow docs/seed-data.md. Use current stable compatible dependencies, pinned/locked with tested updates (docs/dependency-policy.md). Follow docs/security-baseline.md: attributed audit for user/system actions, Identity password hashes, no secrets/PII in logs, tenant/role isolation and privacy/retention requirements. Do not describe EF write-audit alone as complete HTTP/user-action auditing or GDPR compliance.

Business mutations must pass the audited SaveChanges path. ExecuteUpdate/ExecuteDelete and raw SQL bypass this path; use them only in a controlled migration/import design with explicit equivalent audit and tenant enforcement. Never expose administrative seed context, IgnoreQueryFilters or actor construction to untrusted request inputs.

At each completed phase or reviewable delivery package, report the delivered behavior, exact verification results, unresolved blockers, current phase and next step to the user. Update docs/project-status.md, docs/project-status.json and a phase report under docs/phases/. Clearly distinguish a locally validated subpackage from a completed canonical sprint or release gate. Do not wait for the user to request a status report at phase boundaries.

## Current analysis baseline

Read docs/sources/active.json before selecting requirements. The user-supplied analiz/POSEIDON GUNCEL 30.10.2026 set supersedes changed prior documents; the prior sources/poseidon-v46.3 set remains immutable history. Use `py scripts/query_sources.py "exact phrase" --limit 4` for bounded local source retrieval, then read the cited locator. This makes no model calls. Do not read the whole chunks.jsonl into model context.

The 26.01 embedded runtime and past test/UAT archives are historical reference; they are not current repo implementation or current passing tests. Documents 49/50 contain proposals and explicit expert/lookup/permit gates; never turn these into approved production catalogs, human sign-offs or automatic permit activation. POL-DEFAULT-01 allows incomplete drafts and blocks submission with missing required data; UNKNOWN must not be silently converted to No, Ready or NotApplicable.

## Code discovery tools

This project uses codebase-memory-mcp when available. Prefer MCP tools over grep/glob for code discovery in this order: search_graph, trace_path, get_code_snippet, query_graph, get_architecture. If tools are unavailable or insufficient, use the local Graphify graph, then targeted source reads. String literals, config and non-code files may be searched directly. Never claim an MCP graph or hook is connected merely because instructions exist.

## graphify

The local knowledge graph is generated at graphify-out/. It is a derived artifact, not the canonical requirement source. The initial graph covers bootstrap scripts only; full document semantic extraction is separate.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
