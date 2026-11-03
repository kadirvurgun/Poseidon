# S0 engineering exit contract

Status: authored, not executed or accepted. Original source is active18.01 rows2–13 and117;13 tasks /53SP. The53SP S0 denominator is separate from the970SP product ledger. [Policy](exit-policy.v1.json), [candidate template](candidate-template.v1.json), [implementation](../../scripts/s0_release_gate.py), [tests](../../scripts/test_s0_release_gate.py), [source/task record](../../docs/tasks/s0-release-gate-20261102.md).

The evaluator reuses the [current DoR/DoD template](../tasks/task-template.v1.json), [authoring register](../tasks/current-authoring.v1.json) and its validator. It reads/hash-checks the actual active source workbook, derives all original S0 IDs/AC/roles/T-IDs/SP and expands `DEV-001:DEV-011` into all11 dependencies. It does not copy or redefine existing task contracts. DEV001/002/012 use their source definition; the current register must retain definitions for DEV003..011 and116. Existing accepted histories are not silently promoted to the current target.

## Candidate and target

Copy the candidate template into an ignored coordinator evidence location, or use the later `--prepare` command below after freezing a clean target. An empty template is not a blocker inventory or a completion claim. Fill the actual40-character commit and tree IDs, all13 task entries, genuine blockers and criterion-specific unresolved external bindings. Both IDs must match the clean current checkout. Ignored receipts do not mutate the target. Every authored artifact and review artifact must be an ordinary file in that exact git tree, with SHA256 of its original bytes. Symlinks, junctions, traversal, URLs and changed/missing artifacts fail closed.

Each task has:

```json
{
  "id": "DEV-010",
  "authored": {
    "state": "AUTHORED",
    "artifacts": [{"path": "relative/tracked/file", "sha256": "actual SHA256"}]
  },
  "evidence": {
    "execution": {"path": ".runtime/s0/actual-execution.json", "sha256": "actual SHA256"},
    "source-review": {"path": ".runtime/s0/actual-source-review.json", "sha256": "actual SHA256"},
    "engineering-review": {"path": ".runtime/s0/actual-engineering-review.json", "sha256": "actual SHA256"},
    "acceptance": {"path": ".runtime/s0/actual-acceptance.json", "sha256": "actual SHA256"}
  }
}
```

These are shapes, not real receipts. `blockers` require unique opaque IDs matching `BLK-[A-Z0-9-]+`, severity P0–P3 and OPEN/RESOLVED state. An OPEN P0 blocks exit. `externalBindings` record taskId, original criterion, responsible ownerRole and MISSING/PROVISIONED state. Missing required bindings remain visible. Whether a binding is required is established by the literal source assessment: a contract-only environment requirement is not converted into mandatory live UAT/PILOT/production rollout.

Known critical software also comes directly from the current authoring register for the original13 S0 rows. Each source-derived task retains `registeredCriticalSoftwareCount`; a positive count blocks that task and dependency closure even when the candidate omits blockers and every supplied receipt is trusted PASS. Candidate metadata or review labels cannot erase the register's explicit critical gap. Noncritical future adapters, generic pending review/execution markers and critical gaps belonging only to later foundation tasks do not expand the original S0 exit scope.

## Evidence and independence

Every receipt is bounded JSON with schemaVersion `poseidon.s0-receipt.v1`, kind, taskId, actual sessionId/principal, target commit/tree, baseline, original backlog sourceSha256, original traceId, issuedAt in UTC, outcome PASS, integer unresolvedP0=0 and exact-target artifacts. An optional expiresAt must still be current. Future, expired, stale, reused, missing or byte-modified evidence cannot pass. No password, token, health/identity data, raw request, secret configuration or private key belongs in a receipt. Diagnostics never echo receipt bodies or exception values.

Execution receipts include all11 existing task-template check assessments plus the four original DEV002 pipeline checks: backendBuild, frontendBuild, lint and artifact. Each PASS assessment contains positive integer runCount, equal passedCount, failedCount=0, skippedCount=0, integer exitCode=0 and actual nonsecret commandId. Counts describe actual discovered/executed cases or command checks for that category, never authored test counts. The actual command results must be authenticated by the trusted provider. DEFERRED, NOT_RUN, skipped cases and zero discovery cannot become PASS.

For a genuinely out-of-scope category, NOT_APPLICABLE has zero counts and a specific `excludedChecks` justification in the independently authenticated source-review receipt. Unit/build/lint/artifact source requirements cannot be excluded. This does not waive any applicable phase/security/QA check. For a coherent full phase run, task-specific receipts can refer to the same actual combined command/report artifacts; their task/criterion assessments remain distinct.

Source-review, engineering-review and acceptance receipts also include the original literal acceptanceCriteria, complete expanded dependencies and unresolvedBindings=[] only when genuinely resolved. Acceptance binds `evidenceDigests` for execution, source-review and engineering-review, preventing substitution of a different report after approval. Dependency closure is evaluated transitively. DEV012 cannot pass with a missing middle dependency; DEV116 remains a required S0 task even though the original DEV012 cell names001..011.

`TrustedOrchestratorAuthority` is an explicit trust boundary. The host/coordinator must supply actual observed completed independent session results and author identities from the orchestration service. The receipt's display name, task label or hash cannot appoint or authenticate a reviewer. The provider binds actual sessionId/principal, parent coordinator session, provider instance, exact target/baseline and verified report digest/kind/task/outcome. Nonexecution review/acceptance principals and sessions must differ from every recorded author. A new label for the same author cannot self-approve.

The in-process API accepts a trusted provider object implementing `verify(receipt, raw_bytes) -> bool`; this supports the existing genuinely separate agent/QA workflow. The default has no trusted provider and returns missing_external_authority. It never manufactures an approval. For standalone use, a coordinator may export actual host observations outside the author-controlled repository and provide the SHA256 separately. The hash authenticates bytes only: the caller must genuinely trust the provider/export provenance and complete author inventory. Author-written observations with a supplied hash are not independent review. No new signing-key/crypto-provider deployment or human/Marine appointment is an S0 prerequisite.

Coordinator export shape:

```json
{
  "schemaVersion": "poseidon.s0-orchestrator-authority.v1",
  "provider": {"kind": "trusted-orchestrator", "instanceId": "actual host identity", "coordinatorSessionId": "actual coordinator session"},
  "authorPrincipals": ["actual author principal"],
  "authorSessionIds": ["actual author session"],
  "sessions": [{
    "sessionId": "actual completed reviewer session", "principal": "actual independent reviewer principal",
    "providerInstanceId": "actual host identity", "parentSessionId": "actual coordinator session",
    "state": "completed", "target": {"commit": "actual commit", "tree": "actual tree"},
    "baseline": "poseidon-v46.3-20261030",
    "verifiedReports": [{"sha256": "actual observed report SHA256", "kind": "engineering-review", "taskId": "DEV-010", "outcome": "PASS"}]
  }]
}
```

This example intentionally contains no real identities or approvals. A separate session can supply more than one kind only when it actually performed each review. The coordinator remains responsible for honest provenance, original scope assessment and genuine legal/role/source authorities. This evaluator is not an authority service, safety decision or source-activation mechanism.

## Later combined gate

No commands below have been executed in this package. After the parent coordinator freezes the larger S0 package and authorizes execution, run the meaningful unit cases from the repo root:

```powershell
py -m unittest discover -s scripts -p test_s0_release_gate.py
```

Generate a new draft only after committing/fixing the target and creating an ignored evidence directory:

```powershell
py scripts/s0_release_gate.py .runtime/s0/candidate.json --prepare
```

This writes a new file exclusively and refuses to overwrite an existing candidate. The generator includes all13 original S0 source definitions and current-register code/test/doc artifact hashes. DEV001/002/012 are derived from the original source even if absent from the current authoring register; their artifact assessments remain visibly empty until the coordinator supplies actual references. Every generated task stays NOT_ASSESSED, has no execution/review evidence and retains an OPEN `BLK-S0-ASSESSMENT-PENDING`. Presence of a file cannot establish literal-AC completion. Preparation exit0 means only draft generation, with accepted=false; evaluation exit0 has the stronger acceptance meaning below.

The coordinator must assess actual source completion, populate those original task references, reconcile genuine P0/bindings, obtain actual receipts and separately authenticated observations, and then evaluate:

```powershell
py scripts/s0_release_gate.py .runtime/s0/candidate.json --authority C:/trusted-coordinator/s0-observations.json --authority-sha256 ACTUAL_SHA256 --output .runtime/s0/result.json
```

The external path is illustrative, not an existing provisioning claim. Evaluation exit0 requires actual complete S0 acceptance. Any pending or invalid evidence returns exit2. The result separates authored/executed/sourceReviewed/engineeringReviewed/accepted per task,53SP S0 denominator, pending gates and diagnostics. It never edits canonical acceptance/SP, publishes, opens a PR, merges, starts a provider or activates any permit.
