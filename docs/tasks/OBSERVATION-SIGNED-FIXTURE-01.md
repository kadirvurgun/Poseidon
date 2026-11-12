# OBSERVATION-SIGNED-FIXTURE-01

State: NATIVE_BUNDLE_GATES_PASS_GENUINE_GATES_OPEN; acceptance=false. Source authoring is complete; execution is now authorized at the larger technical+17locale bundle gate. Earlier DEFERRED records remain historical.
Base:ddc4e4b; baseline poseidon-v46.3-20261030.
Root owns Python config helper/tests, bundle_fixture_plan.py, bundle_oidc_lab.py, bundle_fixture_provision.ts, status/report.
Separate author owns browser observation fixture loader/tests and technical-cases.ts on observation-fixture-20261112 branch; independent QA after source authoring.

## Controlled authority

Existing MAINTENANCE-OBSERVATION-OWNER-RECOVERY-01 and DEV-053/T-S8-002,18.01Backlog54;19.01 paragraphs26/29/30 actual tenant/asset/job and retry/duplicate/conflict/no-data-loss regression gates;15.01 actual tenant authority; security/seed/dependency baseline. No new entity, approved source catalog, operational source freshness/precision policy, signature, human appointment or credential. Current14.02meter_source mapping/precision remains REVIEW_REQUIRED. This only supplies a synthetic disposable software-test source under explicit local provider boundary.

## Acceptance criteria

1. Pure copy configuration for the existing fixed localhost fresh/disposable source-bundle provider, exact run/tenant/vessel/writer. Add only maintenance.observation.write to that writer and one run-bound synthetic Meter source on seed rootasset=vessel. Preserve all other actors/provider/credentials; reject ambiguous, existing operator or other-run source config; same-run exact repeated setup is idempotent.
2. Fresh source-bundle configuration and explicit selected-generation prepare path call the helper. Never modify production defaults or existing runtime/profile here. Immutable generation publication remains the only config write path.
3. Provisioner requires actual signed writer session and real scoped observation GET/capability/source tuple before publishing a private closed fixture descriptor. No record values, source response body, token or secret in fixture/report. Existing immutable publications are not overwritten.
4. Browser fixture loader validates bounded private regular-file boundary, exact schema, UUIDs, duplicate JSON keys, run-specific source/version, kind/unit/freshness/scale. Its reference grants no authority. Case uses explicit vessel/asset/source, rechecks actual server session and source tuple, never selects an arbitrary first vessel or fills/mocks policy.
5. Integrate the exact new environment key into existing source-bundle required fixture whitelist. Missing or old incomplete fixture fails closed and requires a fresh coherent preparation; no historical failed/successful artifacts silently upgraded or current preview reset.
6. Author meaningful config/loader/actual-context negative tests. Execution was initially DEFERRED and is now authorized at the larger PR-preparation bundle gate. Actual private fixture application, fresh provider/host, real login and final gates remain pending.

## Shared descriptor contract

schemaVersion=poseidon.observation.signed-fixture.v1; fields exactly schemaVersion,runId,tenantId,vesselId,assetId,actorId,source,policyVersion,kind,unit,freshnessSeconds,maxScale.
source=SYNTHETIC-OBS-{runId}; policyVersion=SYNTHETIC-OBS-v1-{runId}; kind=Meter; unit=hours; freshnessSeconds=3600; maxScale=4. These are test values, never production defaults. Scope IDs/actor must be observed through actual session/API.
Environment POSEIDON_BROWSER_OBSERVATION_FIXTURE; publication observation-fixture.json.
Browser module observation-fixture.ts exports observationFixtureFromActual(planSubset,user,projection), requireObservationFixture(environment=process.env), requireObservationContext(page,fixture); builder consumes runId,tenantId,vesselId,writerActorId. Loader errors are closed static codes, no raw parser/private paths.

## PR-preparation execution checkpoint — 13.11.2026

Current source target: 8a9462d. Actual 5e17850 frontend lint/typecheck, 1524/1524
components and production build PASS; backend Release, 1007 unit and 79 real
PostgreSQL persistence integration tests PASS, no failed/skipped tests in these
scopes. API integration837/837 and actual criticalHTTP/write coverage117/117 PASS,
zero failures/skips; four exact shards after diagnosing the two-shard Windows
command-length failure before execution. Original failure is preserved.
Node 54/54 PASS on66f613c; Python 56/56 PASS on3d7b5bf retained only with unchanged
input bindings. The final synthetic Python input change has a fresh8/8 PASS,
which overlaps the original56. Actual source/client rescans PASS on8a:2736 repository and20 client files,
zero findings. Original source finding remains recorded. Genuine fixture application NOT_APPLIED, browser/final
QA/required CI OPEN. No new publication, source waiver, push, PR or merge.

See [phase evidence and remaining gates](../phases/OBSERVATION-SIGNED-FIXTURE-20261112.md).
