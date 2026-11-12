import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { chmodSync, linkSync, lstatSync, mkdirSync, mkdtempSync, realpathSync, rmdirSync, symlinkSync, unlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { observationFixtureFromActual, requireObservationContext, requireObservationFixture } from '../register-specs/observation-fixture.ts'

// Parser/boundary unit fixtures only. No provider, application host, observation
// policy publication or runtime acceptance is supplied by these deterministic doubles.
// OBSERVATION-SIGNED-FIXTURE-01; authored, execution DEFERRED.
const id = (number: number) => '10000000-0000-0000-0000-' + String(number).padStart(12, '0')
const plan = { runId: id(1), tenantId: id(2), vesselId: id(3), writerActorId: id(4) }
function document() {
  return { schemaVersion: 'poseidon.observation.signed-fixture.v1', runId: plan.runId, tenantId: plan.tenantId,
    vesselId: plan.vesselId, assetId: plan.vesselId, actorId: plan.writerActorId, source: `SYNTHETIC-OBS-${plan.runId}`,
    policyVersion: `SYNTHETIC-OBS-v1-${plan.runId}`, kind: 'Meter', unit: 'hours', freshnessSeconds: 3600, maxScale: 4 }
}
function actualUser() {
  return { authenticated: true, setupRequired: false, canRead: true, actorId: plan.writerActorId, tenantId: plan.tenantId,
    csrfToken: 'PRIVATE-UNIT-SENTINEL' }
}
function projection() {
  const value = document()
  return { state: 'ConfiguredDeclarationsOnly', canWrite: true, items: [{ value: 'PRIVATE-MEASUREMENT-SENTINEL' }], sources: [{
    tenantId: value.tenantId, vesselId: value.vesselId, assetId: value.assetId, source: value.source, version: value.policyVersion,
    kind: value.kind, unit: value.unit, freshnessSeconds: value.freshnessSeconds, maxScale: value.maxScale,
  }] }
}
const fixtureError = /^Error: observation_signed_fixture_invalid$/
const contextError = /^Error: observation_actual_context_invalid$/
type Owned = { directories: string[]; files: string[]; links: { path: string; directory: boolean }[] }
const owned: Owned[] = []
function metadata(path: string) {
  try { return lstatSync(path) } catch (error) { if ((error as NodeJS.ErrnoException).code === 'ENOENT') return null; throw error }
}
afterEach(() => {
  for (const paths of owned.splice(0)) {
    // Named synthetic files/links and empty owned directories only; never recurse.
    for (const link of paths.links.reverse()) if (metadata(link.path)) {
      if (process.platform === 'win32' && link.directory) rmdirSync(link.path)
      else unlinkSync(link.path)
    }
    for (const file of paths.files.reverse()) {
      const info = metadata(file)
      if (!info) continue
      if (info.isDirectory() && !info.isSymbolicLink()) rmdirSync(file)
      else unlinkSync(file)
    }
    for (const directory of paths.directories.reverse()) rmdirSync(directory)
  }
})
function fixture(raw: string | Buffer = JSON.stringify(document())) {
  const base = mkdtempSync(join(realpathSync(tmpdir()), 'poseidon-observation-unit-'))
  const directory = join(base, 'private'), file = join(directory, 'observation-fixture.json'), outside = join(base, 'outside.json')
  mkdirSync(directory, { mode: 0o700 }); writeFileSync(file, raw, { mode: 0o600 })
  const paths: Owned = { directories: [base, directory], files: [file], links: [] }; owned.push(paths)
  return { base, directory, file, outside, paths,
    environment: { POSEIDON_LAB_PRIVATE: directory, POSEIDON_BROWSER_OBSERVATION_FIXTURE: file } }
}

test('loads only the twelve run-bound references from a private regular descriptor', () => {
  const paths = fixture(), value = requireObservationFixture(paths.environment)
  assert.deepEqual(value, document()); assert.ok(Object.isFrozen(value))
  assert.equal(Object.keys(value).length, 12)
})

test('requires absolute private paths and rejects outside, missing, directory and alternate publication names', () => {
  const paths = fixture(); writeFileSync(paths.outside, JSON.stringify(document()), { mode: 0o600 }); paths.paths.files.push(paths.outside)
  const otherName = join(paths.directory, 'operator.json'); writeFileSync(otherName, JSON.stringify(document()), { mode: 0o600 }); paths.paths.files.push(otherName)
  for (const environment of [{}, { ...paths.environment, POSEIDON_LAB_PRIVATE: 'relative' },
    { ...paths.environment, POSEIDON_BROWSER_OBSERVATION_FIXTURE: 'observation-fixture.json' },
    { ...paths.environment, POSEIDON_BROWSER_OBSERVATION_FIXTURE: paths.outside },
    { ...paths.environment, POSEIDON_BROWSER_OBSERVATION_FIXTURE: paths.directory },
    { ...paths.environment, POSEIDON_BROWSER_OBSERVATION_FIXTURE: join(paths.directory, 'missing', 'observation-fixture.json') },
    { ...paths.environment, POSEIDON_BROWSER_OBSERVATION_FIXTURE: otherName }])
    assert.throws(() => requireObservationFixture(environment), /^Error: observation_signed_fixture_(?:required|invalid)$/)
  unlinkSync(paths.file); mkdirSync(paths.file, { mode: 0o700 })
  assert.throws(() => requireObservationFixture(paths.environment), fixtureError)
})

test('accepts the exact bounded UTF8 budget and closes malformed, oversized, BOM and invalid UTF8 errors', () => {
  const paths = fixture(), raw = JSON.stringify(document())
  writeFileSync(paths.file, raw.padEnd(8192, ' ')); assert.deepEqual(requireObservationFixture(paths.environment), document())
  for (const content of [raw.padEnd(8193, ' '), '', '{"PRIVATE-PARSER-SENTINEL":', '\ufeff' + raw, Buffer.from([0xff, 0xfe])]) {
    writeFileSync(paths.file, content)
    assert.throws(() => requireObservationFixture(paths.environment), fixtureError)
  }
})

test('rejects schema extensions, malformed UUIDs and every altered run/source/scope/policy tuple', () => {
  const paths = fixture(), value = document()
  const invalid: unknown[] = [null, [], { ...value, approved: true }, { ...value, schemaVersion: 'old' },
    { ...value, runId: '00000000-0000-0000-0000-000000000000' }, { ...value, runId: id(9) },
    { ...value, tenantId: 'not-a-uuid' }, { ...value, actorId: 'A0000000-0000-0000-0000-000000000001' },
    { ...value, assetId: id(9) }, { ...value, source: `SYNTHETIC-OBS-${id(9)}` },
    { ...value, policyVersion: `SYNTHETIC-OBS-v1-${id(9)}` }, { ...value, kind: 'Condition' },
    { ...value, unit: 'cycles' }, { ...value, freshnessSeconds: 0 }, { ...value, freshnessSeconds: 3601 },
    { ...value, maxScale: '4' }, { ...value, maxScale: 5 }]
  const missing: Record<string, unknown> = { ...value }; delete missing.actorId; invalid.push(missing)
  for (const input of invalid) {
    writeFileSync(paths.file, JSON.stringify(input)); assert.throws(() => requireObservationFixture(paths.environment), fixtureError)
  }
})

test('rejects duplicate and escaped duplicate keys rather than accepting the final value', () => {
  const raw = JSON.stringify(document()), paths = fixture()
  for (const repeated of [raw.replace('"runId":', `"runId":"${id(9)}","runId":`),
    raw.replace('"runId":', `"\\u0072unId":"${plan.runId}","runId":`),
    raw.replace('"maxScale":', '"maxScale":4,"maxScale":')]) {
    writeFileSync(paths.file, repeated); assert.throws(() => requireObservationFixture(paths.environment), fixtureError)
  }
})

test('rejects a final newline in every UUID even when the other descriptor bindings match', () => {
  const paths = fixture()
  for (const key of ['runId', 'tenantId', 'vesselId', 'assetId', 'actorId'] as const) {
    const value = document(), changed = { ...value, [key]: value[key] + '\n' }
    if (key === 'runId') {
      changed.source = `SYNTHETIC-OBS-${changed.runId}`; changed.policyVersion = `SYNTHETIC-OBS-v1-${changed.runId}`
    }
    if (key === 'vesselId' || key === 'assetId') changed.vesselId = changed.assetId = value.vesselId + '\n'
    writeFileSync(paths.file, JSON.stringify(changed))
    assert.throws(() => requireObservationFixture(paths.environment), fixtureError)
  }
})

test('rejects a hardlinked descriptor even when its contents satisfy the schema', () => {
  const paths = fixture(); linkSync(paths.file, paths.outside); paths.paths.files.push(paths.outside)
  assert.throws(() => requireObservationFixture(paths.environment), fixtureError)
})

test('rejects a symlink or Windows junction used as the declared private root', () => {
  const paths = fixture(), alias = join(paths.base, 'alias')
  symlinkSync(paths.directory, alias, process.platform === 'win32' ? 'junction' : 'dir')
  paths.paths.links.push({ path: alias, directory: true })
  assert.throws(() => requireObservationFixture({ ...paths.environment, POSEIDON_LAB_PRIVATE: alias,
    POSEIDON_BROWSER_OBSERVATION_FIXTURE: join(alias, 'observation-fixture.json') }), fixtureError)
})

test('rejects an intermediate symlink or Windows junction below the private root', () => {
  const paths = fixture(), alias = join(paths.directory, 'alias'), target = join(paths.base, 'target')
  mkdirSync(target, { mode: 0o700 }); paths.paths.directories.push(target)
  const targetFile = join(target, 'observation-fixture.json'); writeFileSync(targetFile, JSON.stringify(document()), { mode: 0o600 }); paths.paths.files.push(targetFile)
  symlinkSync(target, alias, process.platform === 'win32' ? 'junction' : 'dir'); paths.paths.links.push({ path: alias, directory: true })
  assert.throws(() => requireObservationFixture({ ...paths.environment,
    POSEIDON_BROWSER_OBSERVATION_FIXTURE: join(alias, 'observation-fixture.json') }), fixtureError)
})

if (process.platform !== 'win32') {
  test('rejects POSIX file symlinks and group/world-readable private roots or descriptors', () => {
    const paths = fixture(); unlinkSync(paths.file)
    writeFileSync(paths.outside, JSON.stringify(document()), { mode: 0o600 }); paths.paths.files.push(paths.outside)
    symlinkSync(paths.outside, paths.file); assert.throws(() => requireObservationFixture(paths.environment), fixtureError)
    unlinkSync(paths.file); writeFileSync(paths.file, JSON.stringify(document()), { mode: 0o600 })
    chmodSync(paths.file, 0o644); assert.throws(() => requireObservationFixture(paths.environment), fixtureError)
    chmodSync(paths.file, 0o600); chmodSync(paths.directory, 0o755)
    assert.throws(() => requireObservationFixture(paths.environment), fixtureError)
  })
}

test('pure builder requires actual owner/read/write/source evidence and publishes no token or record values', () => {
  const user = actualUser(), state = projection(), before = JSON.stringify({ plan, user, state })
  const value = observationFixtureFromActual(plan, user, state)
  assert.deepEqual(value, document()); assert.ok(Object.isFrozen(value))
  assert.equal(JSON.stringify(value).includes('PRIVATE-'), false)
  assert.equal(JSON.stringify({ plan, user, state }) === before, true)
})

test('builder rejects incomplete plans, unauthenticated sessions, wrong actor/tenant, missing read and inherited authority', () => {
  for (const user of [null, {}, { ...actualUser(), authenticated: false }, { ...actualUser(), canRead: false },
    { ...actualUser(), setupRequired: true }, { ...actualUser(), actorId: id(9) }, { ...actualUser(), tenantId: id(9) }, Object.create(actualUser())])
    assert.throws(() => observationFixtureFromActual(plan, user, projection()), contextError)
  for (const input of [null, { ...plan, writerActorId: 'invalid' }, { ...plan, runId: id(9) }, { ...plan, approved: true }])
    assert.throws(() => observationFixtureFromActual(input, actualUser(), projection()), contextError)
})

test('builder rejects denied reads, ambiguous sources, wrong scope, changed policy and source precision', () => {
  const state = projection(), rule = state.sources[0]
  const invalid = [null, { ...state, canWrite: false }, { ...state, state: 'PolicyUnavailable' },
    { ...state, sources: [] }, { ...state, sources: [rule, rule] }, { ...state, items: Array.from({ length: 101 }, () => ({})) },
    ...[{ tenantId: id(9) }, { vesselId: id(9) }, { assetId: id(9) }, { source: 'operator-source' },
      { version: 'old' }, { kind: 'Condition' }, { unit: 'cycles' }, { freshnessSeconds: 7200 }, { maxScale: 3 }, { approved: true }]
      .map(change => ({ ...state, sources: [{ ...rule, ...change }] }))]
  for (const input of invalid) assert.throws(() => observationFixtureFromActual(plan, actualUser(), input), contextError)
})

test('builder rejects a final newline in every planned UUID with otherwise matching session and source', () => {
  for (const key of ['runId', 'tenantId', 'vesselId', 'writerActorId'] as const) {
    const input = { ...plan, [key]: plan[key] + '\n' }, state = projection()
    const user = { ...actualUser(), actorId: input.writerActorId, tenantId: input.tenantId }
    state.sources[0] = { ...state.sources[0], tenantId: input.tenantId, vesselId: input.vesselId, assetId: input.vesselId,
      source: `SYNTHETIC-OBS-${input.runId}`, version: `SYNTHETIC-OBS-v1-${input.runId}` }
    assert.throws(() => observationFixtureFromActual(input, user, state), contextError)
  }
})

function pageWithResponses(responses: { status: number; value?: unknown; parseError?: boolean }[]) {
  const paths: string[] = []; let jsonReads = 0
  return { paths, jsonReads: () => jsonReads, page: { request: { get: async (path: string) => {
    paths.push(path); const response = responses.shift()
    if (!response) throw new Error('unexpected_unit_request')
    return { status: () => response.status, json: async () => {
      jsonReads++; if (response.parseError) throw new Error('PRIVATE-PARSER-SENTINEL'); return response.value
    } }
  } } } }
}

test('actual context requires session, exact scoped GET and a second matching session before returning closed references', async () => {
  const paths = fixture(), reference = requireObservationFixture(paths.environment)
  const transport = pageWithResponses([{ status: 200, value: actualUser() }, { status: 200, value: projection() }, { status: 200, value: actualUser() }])
  assert.deepEqual(await requireObservationContext(transport.page, reference), document())
  assert.deepEqual(transport.paths, ['/api/v1/auth/session', `/api/v1/equipment-hierarchy/${plan.vesselId}/${plan.vesselId}/observations`, '/api/v1/auth/session'])
})

test('a wrong or unauthenticated first session cannot even read the private source projection', async () => {
  const paths = fixture(), reference = requireObservationFixture(paths.environment)
  for (const user of [{ ...actualUser(), actorId: id(9) }, { ...actualUser(), tenantId: id(9) }, { ...actualUser(), authenticated: false }]) {
    const transport = pageWithResponses([{ status: 200, value: user }])
    await assert.rejects(requireObservationContext(transport.page, reference), contextError)
    assert.deepEqual(transport.paths, ['/api/v1/auth/session'])
  }
})

test('a replacement actor after the scoped GET cannot inherit the observed source grant', async () => {
  const paths = fixture(), reference = requireObservationFixture(paths.environment)
  for (const current of [{ ...actualUser(), actorId: id(9) }, { ...actualUser(), tenantId: id(9) },
    { ...actualUser(), authenticated: false }, { ...actualUser(), canRead: false }]) {
    const transport = pageWithResponses([{ status: 200, value: actualUser() }, { status: 200, value: projection() }, { status: 200, value: current }])
    await assert.rejects(requireObservationContext(transport.page, reference), contextError)
  }
})

test('actual context rejects wrong scoped or denied source projections before returning references', async () => {
  const paths = fixture(), reference = requireObservationFixture(paths.environment), state = projection()
  for (const value of [{ ...state, canWrite: false }, { ...state, sources: [] },
    { ...state, sources: [{ ...state.sources[0], assetId: id(9) }] },
    { ...state, sources: [{ ...state.sources[0], tenantId: id(9) }] },
    { ...state, sources: [{ ...state.sources[0], version: 'other-run' }] }]) {
    const transport = pageWithResponses([{ status: 200, value: actualUser() }, { status: 200, value }])
    await assert.rejects(requireObservationContext(transport.page, reference), contextError)
    assert.equal(transport.paths.length, 2)
  }
})

test('HTTP denial, source failure and parser failure never become fabricated 200 context', async () => {
  const paths = fixture(), reference = requireObservationFixture(paths.environment)
  for (const status of [401, 403, 404, 500]) {
    const denied = pageWithResponses([{ status, parseError: true }])
    await assert.rejects(requireObservationContext(denied.page, reference), contextError)
    assert.equal(denied.jsonReads(), 0)
    const source = pageWithResponses([{ status: 200, value: actualUser() }, { status, parseError: true }])
    await assert.rejects(requireObservationContext(source.page, reference), contextError)
    assert.equal(source.jsonReads(), 1)
  }
  const broken = pageWithResponses([{ status: 200, parseError: true }])
  await assert.rejects(requireObservationContext(broken.page, reference), contextError)
  const lost = { request: { get: async () => { throw new Error('PRIVATE-TRANSPORT-SENTINEL') } } }
  await assert.rejects(requireObservationContext(lost, reference), contextError)
})
