# OBSERVATION-SIGNED-FIXTURE-01

State: AUTHORING; acceptance=false. All new test/build/typecheck/lint/browser execution DEFERRED.
Base:ddc4e4b; baseline poseidon-v46.3-20261030.
Root owns Python config helper/tests, bundle_fixture_plan.py, bundle_oidc_lab.py, bundle_fixture_provision.ts, status/report.
Separate author owns browser observation fixture loader/tests and technical-cases.ts on observation-fixture-20261112 branch; independent QA after source authoring.

## Controlled authority

Existing MAINTENANCE-OBSERVATION-OWNER-RECOVERY-01 and DEV-053/T-S8-002,18.01Backlog54;19.01 actual API/tenant/role/idempotent regression gates;15.01 actual tenant authority; security/seed/dependency baseline. No new entity, approved source catalog, operational source freshness/precision policy, signature, human appointment or credential. Current14.02meter_source mapping/precision remains REVIEW_REQUIRED. This only supplies a synthetic disposable software-test source under explicit local provider boundary.

## Acceptance criteria

1. Pure copy configuration for the existing fixed localhost fresh/disposable source-bundle provider, exact run/tenant/vessel/writer. Add only maintenance.observation.write to that writer and one run-bound synthetic Meter source on seed rootasset=vessel. Preserve all other actors/provider/credentials; reject ambiguous, existing operator or other-run source config; same-run exact repeated setup is idempotent.
2. Fresh source-bundle configuration and explicit selected-generation prepare path call the helper. Never modify production defaults or existing runtime/profile here. Immutable generation publication remains the only config write path.
3. Provisioner requires actual signed writer session and real scoped observation GET/capability/source tuple before publishing a private closed fixture descriptor. No record values, source response body, token or secret in fixture/report. Existing immutable publications are not overwritten.
4. Browser fixture loader validates bounded private regular-file boundary, exact schema, UUIDs, duplicate JSON keys, run-specific source/version, kind/unit/freshness/scale. Its reference grants no authority. Case uses explicit vessel/asset/source, rechecks actual server session and source tuple, never selects an arbitrary first vessel or fills/mocks policy.
5. Integrate the exact new environment key into existing source-bundle required fixture whitelist. Missing or old incomplete fixture fails closed and requires a fresh coherent preparation; no historical failed/successful artifacts silently upgraded or current preview reset.
6. Author meaningful config/loader/actual-context negative tests. All execution DEFERRED; actual private fixture application, fresh provider/host, real login and full gates remain pending until bundle gate.

## Shared descriptor contract

schemaVersion=poseidon.observation.signed-fixture.v1; fields exactly schemaVersion,runId,tenantId,vesselId,assetId,actorId,source,policyVersion,kind,unit,freshnessSeconds,maxScale.
source=SYNTHETIC-OBS-{runId}; policyVersion=SYNTHETIC-OBS-v1-{runId}; kind=Meter; unit=hours; freshnessSeconds=3600; maxScale=4. These are test values, never production defaults. Scope IDs/actor must be observed through actual session/API.
Environment POSEIDON_BROWSER_OBSERVATION_FIXTURE; publication observation-fixture.json.
Browser module observation-fixture.ts exports observationFixtureFromActual(planSubset,user,projection), requireObservationFixture(environment=process.env), requireObservationContext(page,fixture); builder consumes runId,tenantId,vesselId,writerActorId. Loader errors are closed static codes, no raw parser/private paths.
