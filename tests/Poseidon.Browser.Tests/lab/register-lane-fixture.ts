import { closeSync, constants, fstatSync, openSync, readSync, realpathSync } from 'node:fs'
import { isAbsolute, relative, sep } from 'node:path'

// DEV-OIDC-LAB-01 bundle gate: isolate actual disposable fixture mutations by
// viewport lane. Loading these references grants no application authority.
export const registerLaneKeys = [
  'POSEIDON_BROWSER_NO_EVALUATION_VESSEL', 'POSEIDON_BROWSER_HUMAN_EVIDENCE',
  'POSEIDON_BROWSER_QUALIFICATION_RULE', 'POSEIDON_BROWSER_LEGACY_CREW_BATCH',
  'POSEIDON_BROWSER_LEGACY_CREW_VESSEL',
] as const
type LaneValues = Readonly<Record<typeof registerLaneKeys[number], string>>
export type RegisterLaneFixture = Readonly<{ runId: string; lanes: Readonly<{ desktop: LaneValues; narrow: LaneValues }> }>
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const exact = (value: unknown, keys: readonly string[]): value is Record<string, unknown> => object(value) && Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key))
const guid = (value: unknown): value is string => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value) && value !== '00000000-0000-0000-0000-000000000000'
function invalid(): never { throw new Error('signed_lane_fixture_invalid') }

function decode(raw: string): RegisterLaneFixture {
  const value: unknown = JSON.parse(raw)
  if (!exact(value, ['schemaVersion', 'runId', 'lanes']) || value.schemaVersion !== 'poseidon.bundle.signed-lane-fixtures.v1' || !guid(value.runId) || !exact(value.lanes, ['desktop', 'narrow'])) invalid()
  const lane = (input: unknown): LaneValues => {
    if (!exact(input, registerLaneKeys) || !registerLaneKeys.every(key => guid(input[key]))) invalid()
    return Object.freeze(Object.fromEntries(registerLaneKeys.map(key => [key, input[key]]))) as LaneValues
  }
  const desktop = lane(value.lanes.desktop), narrow = lane(value.lanes.narrow)
  // The attributed legacy importer owns one fixed synthetic vessel; its batches,
  // and all other mutable fixture roots, must be distinct across the two lanes.
  if (registerLaneKeys.some(key => key !== 'POSEIDON_BROWSER_LEGACY_CREW_VESSEL' && desktop[key] === narrow[key])) invalid()
  // JSON.parse otherwise accepts repeated properties. The closed, UUID-only
  // schema makes these property occurrence counts unambiguous, including escapes.
  const counts = new Map<string, number>()
  for (const token of raw.matchAll(/"(?:\\.|[^"\\])*"(?=\s*:)/g)) {
    const key: unknown = JSON.parse(token[0])
    if (typeof key !== 'string') invalid()
    counts.set(key, (counts.get(key) ?? 0) + 1)
  }
  if (['schemaVersion', 'runId', 'lanes', 'desktop', 'narrow'].some(key => counts.get(key) !== 1) || registerLaneKeys.some(key => counts.get(key) !== 2)) invalid()
  return Object.freeze({ runId: value.runId, lanes: Object.freeze({ desktop, narrow }) })
}

export function loadRegisterLaneFixture(environment: Record<string, string | undefined>): RegisterLaneFixture {
  const name = environment.POSEIDON_BROWSER_LANE_FIXTURE, directory = environment.POSEIDON_LAB_PRIVATE
  if (!name || !directory || !isAbsolute(name) || !isAbsolute(directory)) throw new Error('signed_lane_fixture_required')
  try {
    const root = realpathSync(directory), path = realpathSync(name), child = relative(root, path)
    if (!child || isAbsolute(child) || child === '..' || child.startsWith('..' + sep)) invalid()
    const descriptor = openSync(path, constants.O_RDONLY | (constants.O_NOFOLLOW ?? 0))
    try {
      const metadata = fstatSync(descriptor)
      if (!metadata.isFile() || metadata.size > 8192) invalid()
      const buffer = Buffer.alloc(8193)
      let length = 0, count: number
      while (length < buffer.length && (count = readSync(descriptor, buffer, length, buffer.length - length, length)) !== 0) length += count
      if (length > 8192) invalid()
      return decode(new TextDecoder('utf-8', { fatal: true }).decode(buffer.subarray(0, length)))
    } finally { closeSync(descriptor) }
  } catch { invalid() } // Never serialize private paths, UUIDs or parser diagnostics.
}

export function registerLaneEnvironment(fixture: RegisterLaneFixture, width: unknown): LaneValues {
  if (typeof width !== 'number' || !Number.isSafeInteger(width) || width < 320) throw new Error('signed_lane_viewport_required')
  return fixture.lanes[width <= 420 ? 'narrow' : 'desktop']
}
