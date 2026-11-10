import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { existsSync, mkdirSync, mkdtempSync, rmdirSync, unlinkSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { loadRegisterLaneFixture, registerLaneEnvironment, registerLaneKeys } from './register-lane-fixture.ts'

const owned: { root: string; directory: string; file: string; outside: string }[] = []
afterEach(() => {
  for (const paths of owned.splice(0)) {
    // Only the individually named synthetic files and empty owned directories.
    for (const file of [paths.file, paths.outside]) if (existsSync(file)) unlinkSync(file)
    rmdirSync(paths.directory); rmdirSync(paths.root)
  }
})
const id = (number: number) => '10000000-0000-0000-0000-' + String(number).padStart(12, '0')
function document() {
  const desktop = Object.fromEntries(registerLaneKeys.map((key, index) => [key, id(index + 1)]))
  const narrow = Object.fromEntries(registerLaneKeys.map((key, index) => [key, id(index + 11)]))
  narrow.POSEIDON_BROWSER_LEGACY_CREW_VESSEL = desktop.POSEIDON_BROWSER_LEGACY_CREW_VESSEL
  return { schemaVersion: 'poseidon.bundle.signed-lane-fixtures.v1', runId: id(99), lanes: { desktop, narrow } }
}
function fixture(raw = JSON.stringify(document())) {
  const root = mkdtempSync(join(tmpdir(), 'poseidon-lane-unit-')), directory = join(root, 'private')
  mkdirSync(directory)
  const file = join(directory, 'source-bundle-lane-environment.json'), outside = join(root, 'outside.json')
  owned.push({ root, directory, file, outside }); writeFileSync(file, raw)
  return { file, outside, directory, environment: { POSEIDON_LAB_PRIVATE: directory, POSEIDON_BROWSER_LANE_FIXTURE: file } }
}

test('selects only the five whitelisted observed references for each actual viewport lane', () => {
  const paths = fixture(), value = loadRegisterLaneFixture(paths.environment)
  assert.deepEqual(Object.keys(registerLaneEnvironment(value, 1280)), [...registerLaneKeys])
  assert.deepEqual(registerLaneEnvironment(value, 1280), document().lanes.desktop)
  for (const width of [320, 390, 420]) assert.deepEqual(registerLaneEnvironment(value, width), document().lanes.narrow)
  assert.deepEqual(registerLaneEnvironment(value, 421), document().lanes.desktop)
  assert.equal(value.lanes.desktop.POSEIDON_BROWSER_LEGACY_CREW_VESSEL, value.lanes.narrow.POSEIDON_BROWSER_LEGACY_CREW_VESSEL)
  assert.ok(Object.isFrozen(value)); assert.ok(Object.isFrozen(value.lanes.desktop))
})

for (const width of [undefined, null, '390', 319, 0, -1, 390.5, NaN, Infinity]) {
  test('rejects missing or invalid viewport without choosing an invented lane: ' + String(width), () => {
    const paths = fixture(), value = loadRegisterLaneFixture(paths.environment)
    assert.throws(() => registerLaneEnvironment(value, width), /^Error: signed_lane_viewport_required$/)
  })
}

test('fails closed for missing paths, relative paths, outside files and directories', () => {
  const paths = fixture(); writeFileSync(paths.outside, JSON.stringify(document()))
  for (const environment of [{}, { ...paths.environment, POSEIDON_BROWSER_LANE_FIXTURE: 'relative.json' },
    { ...paths.environment, POSEIDON_BROWSER_LANE_FIXTURE: paths.outside },
    { ...paths.environment, POSEIDON_BROWSER_LANE_FIXTURE: paths.directory },
    { ...paths.environment, POSEIDON_BROWSER_LANE_FIXTURE: join(paths.directory, 'absent.json') }]) {
    assert.throws(() => loadRegisterLaneFixture(environment), /^Error: signed_lane_fixture_(?:required|invalid)$/)
  }
})

test('accepts the exact 8192-byte budget and rejects larger, malformed or non-UTF8 content', () => {
  const paths = fixture(), raw = JSON.stringify(document())
  writeFileSync(paths.file, raw.padEnd(8192, ' ')); assert.equal(loadRegisterLaneFixture(paths.environment).runId, id(99))
  for (const content of [raw.padEnd(8193, ' '), '{', Buffer.from([0xff, 0xfe, 0xfd])]) {
    writeFileSync(paths.file, content)
    assert.throws(() => loadRegisterLaneFixture(paths.environment), /^Error: signed_lane_fixture_invalid$/)
  }
})

test('rejects extra keys, missing members, invalid GUIDs, wrong schema and incomplete lane maps', () => {
  const paths = fixture()
  const invalid: unknown[] = [null, [], { ...document(), schemaVersion: 'unknown' },
    { ...document(), runId: '00000000-0000-0000-0000-000000000000' }, { ...document(), runId: 'not-a-guid' },
    { ...document(), extra: true }, { ...document(), lanes: { desktop: document().lanes.desktop } },
    { ...document(), lanes: { ...document().lanes, extra: document().lanes.desktop } },
    { ...document(), lanes: { ...document().lanes, narrow: { ...document().lanes.narrow, PATH: 'forbidden' } } },
    { ...document(), lanes: { ...document().lanes, narrow: {} } }]
  // An uppercase hexadecimal digit must fail canonical casing, even if the UUID is otherwise valid.
  invalid.push({ ...document(), runId: 'a0000000-0000-0000-0000-000000000001'.toUpperCase() })
  for (const value of invalid) {
    writeFileSync(paths.file, JSON.stringify(value))
    assert.throws(() => loadRegisterLaneFixture(paths.environment), /^Error: signed_lane_fixture_invalid$/)
  }
})

for (const key of registerLaneKeys.filter(key => key !== 'POSEIDON_BROWSER_LEGACY_CREW_VESSEL')) {
  test('rejects sharing a mutable source fixture between viewport lanes: ' + key, () => {
    const value = document(); value.lanes.narrow[key] = value.lanes.desktop[key]
    const paths = fixture(JSON.stringify(value))
    assert.throws(() => loadRegisterLaneFixture(paths.environment), /^Error: signed_lane_fixture_invalid$/)
  })
}

test('rejects duplicate JSON properties instead of accepting their final value', () => {
  const raw = JSON.stringify(document()), paths = fixture(raw.replace('"runId":', '"runId":"' + id(98) + '","runId":'))
  assert.throws(() => loadRegisterLaneFixture(paths.environment), /^Error: signed_lane_fixture_invalid$/)
  writeFileSync(paths.file, raw.replace('"POSEIDON_BROWSER_HUMAN_EVIDENCE":', '"POSEIDON_BROWSER_HUMAN_EVIDENCE":"' + id(97) + '","POSEIDON_BROWSER_HUMAN_EVIDENCE":'))
  assert.throws(() => loadRegisterLaneFixture(paths.environment), /^Error: signed_lane_fixture_invalid$/)
})
