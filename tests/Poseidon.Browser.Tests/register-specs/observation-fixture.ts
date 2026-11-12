import { closeSync, constants, fstatSync, lstatSync, openSync, readSync, realpathSync } from 'node:fs'
import type { Stats } from 'node:fs'
import { basename, isAbsolute, join, relative, resolve, sep } from 'node:path'

// OBSERVATION-SIGNED-FIXTURE-01: closed references to an actual disposable source.
// A descriptor supplies no permission, operational policy or human qualification.
const schema = 'poseidon.observation.signed-fixture.v1' as const
const keys = ['schemaVersion', 'runId', 'tenantId', 'vesselId', 'assetId', 'actorId', 'source',
  'policyVersion', 'kind', 'unit', 'freshnessSeconds', 'maxScale'] as const
const sourceKeys = ['tenantId', 'vesselId', 'assetId', 'source', 'version', 'kind', 'unit', 'freshnessSeconds', 'maxScale'] as const
export type ObservationSignedFixture = Readonly<{
  schemaVersion: typeof schema; runId: string; tenantId: string; vesselId: string; assetId: string; actorId: string
  source: string; policyVersion: string; kind: 'Meter'; unit: 'hours'; freshnessSeconds: 3600; maxScale: 4
}>
export type ObservationFixturePlan = Readonly<{ runId: string; tenantId: string; vesselId: string; writerActorId: string }>
type ContextPage = { request: { get(path: string): Promise<{ status(): number; json(): Promise<unknown> }> } }
const object = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value)
const exact = (value: unknown, names: readonly string[]): value is Record<string, unknown> => object(value) &&
  Object.keys(value).length === names.length && names.every(name => Object.hasOwn(value, name))
const guid = (value: unknown): value is string => typeof value === 'string' && value.length === 36 &&
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value) && value !== '00000000-0000-0000-0000-000000000000'
function invalid(): never { throw new Error('observation_signed_fixture_invalid') }
function contextInvalid(): never { throw new Error('observation_actual_context_invalid') }
const actualOwner = (user: unknown, expected: Pick<ObservationSignedFixture, 'actorId' | 'tenantId'>) =>
  object(user) && ['authenticated', 'setupRequired', 'canRead', 'actorId', 'tenantId'].every(key => Object.hasOwn(user, key)) &&
  user.authenticated === true && user.setupRequired === false && user.canRead === true &&
  user.actorId === expected.actorId && user.tenantId === expected.tenantId

function decode(value: unknown): ObservationSignedFixture {
  if (!exact(value, keys) || value.schemaVersion !== schema ||
      !['runId', 'tenantId', 'vesselId', 'assetId', 'actorId'].every(key => guid(value[key])) ||
      value.assetId !== value.vesselId || value.source !== `SYNTHETIC-OBS-${value.runId}` ||
      value.policyVersion !== `SYNTHETIC-OBS-v1-${value.runId}` || value.kind !== 'Meter' || value.unit !== 'hours' ||
      value.freshnessSeconds !== 3600 || value.maxScale !== 4) invalid()
  return Object.freeze(Object.fromEntries(keys.map(key => [key, value[key]]))) as ObservationSignedFixture
}

function parse(raw: string): ObservationSignedFixture {
  const value = decode(JSON.parse(raw))
  // The flat closed schema makes decoded property counts unambiguous. JSON.parse
  // alone accepts duplicates, including an escaped spelling of the same key.
  const counts = new Map<string, number>()
  for (const token of raw.matchAll(/"(?:\\.|[^"\\])*"(?=\s*:)/g)) {
    const name: unknown = JSON.parse(token[0])
    if (typeof name !== 'string') invalid()
    counts.set(name, (counts.get(name) ?? 0) + 1)
  }
  if (counts.size !== keys.length || keys.some(key => counts.get(key) !== 1)) invalid()
  return value
}

function samePath(first: string, second: string) {
  return process.platform === 'win32' ? first.toLowerCase() === second.toLowerCase() : first === second
}
function privateMetadata(metadata: Stats, directory: boolean) {
  if (metadata.isSymbolicLink() || !(directory ? metadata.isDirectory() : metadata.isFile()) || !directory && metadata.nlink !== 1) invalid()
  // NTFS permissions are enforced by the existing private-root publication ACL;
  // POSIX mode bits are not an ACL attestation on Windows. Links/path identities
  // are checked on both platforms, including junctions reported by lstat.
  if (process.platform !== 'win32' && ((metadata.mode & 0o077) !== 0 ||
      typeof process.geteuid === 'function' && metadata.uid !== process.geteuid())) invalid()
}
const unchanged = (first: Stats, second: Stats) => first.dev === second.dev && first.ino === second.ino &&
  first.size === second.size && first.mtimeMs === second.mtimeMs && first.ctimeMs === second.ctimeMs

export function requireObservationFixture(environment: Record<string, string | undefined> = process.env): ObservationSignedFixture {
  const name = environment.POSEIDON_BROWSER_OBSERVATION_FIXTURE, directory = environment.POSEIDON_LAB_PRIVATE
  if (!name || !directory) throw new Error('observation_signed_fixture_required')
  try {
    if (!isAbsolute(name) || !isAbsolute(directory) || name.length > 32768 || directory.length > 32768) invalid()
    const root = resolve(directory), path = resolve(name), child = relative(root, path)
    if (!child || isAbsolute(child) || child === '..' || child.startsWith('..' + sep) ||
        basename(path) !== 'observation-fixture.json' || child.split(sep).length > 16 ||
        !samePath(realpathSync(root), root) || !samePath(realpathSync(path), path)) invalid()
    const parents = [root]
    for (const part of child.split(sep).slice(0, -1)) parents.push(join(parents.at(-1)!, part))
    const parentMetadata = parents.map(parent => { const metadata = lstatSync(parent); privateMetadata(metadata, true); return metadata })
    const before = lstatSync(path); privateMetadata(before, false)
    if (before.size < 1 || before.size > 8192) invalid()
    const descriptor = openSync(path, constants.O_RDONLY | (constants.O_NOFOLLOW ?? 0) | (constants.O_NONBLOCK ?? 0))
    try {
      const opened = fstatSync(descriptor); privateMetadata(opened, false)
      if (!unchanged(before, opened)) invalid()
      const buffer = Buffer.alloc(8193)
      let length = 0, count: number
      while (length < buffer.length && (count = readSync(descriptor, buffer, length, buffer.length - length, length)) !== 0) length += count
      if (length > 8192 || length !== opened.size || !unchanged(opened, fstatSync(descriptor)) ||
          !samePath(realpathSync(path), path) || !samePath(realpathSync(root), root)) invalid()
      const after = lstatSync(path); privateMetadata(after, false)
      if (!unchanged(opened, after)) invalid()
      parents.forEach((parent, index) => {
        const metadata = lstatSync(parent); privateMetadata(metadata, true)
        if (metadata.dev !== parentMetadata[index].dev || metadata.ino !== parentMetadata[index].ino) invalid()
      })
      if (length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) invalid()
      return parse(new TextDecoder('utf-8', { fatal: true }).decode(buffer.subarray(0, length)))
    } finally { closeSync(descriptor) }
  } catch { invalid() } // Never retain parser diagnostics, private paths or payloads in errors.
}

export function observationFixtureFromActual(planSubset: unknown, user: unknown, projection: unknown): ObservationSignedFixture {
  try {
    if (!exact(planSubset, ['runId', 'tenantId', 'vesselId', 'writerActorId']) ||
        !['runId', 'tenantId', 'vesselId', 'writerActorId'].every(key => guid(planSubset[key]))) contextInvalid()
    const expected = decode({ schemaVersion: schema, runId: planSubset.runId, tenantId: planSubset.tenantId,
      vesselId: planSubset.vesselId, assetId: planSubset.vesselId, actorId: planSubset.writerActorId,
      source: `SYNTHETIC-OBS-${planSubset.runId}`, policyVersion: `SYNTHETIC-OBS-v1-${planSubset.runId}`,
      kind: 'Meter', unit: 'hours', freshnessSeconds: 3600, maxScale: 4 })
    if (!actualOwner(user, expected)) contextInvalid()
    if (!exact(projection, ['sources', 'items', 'canWrite', 'state']) || projection.canWrite !== true ||
        projection.state !== 'ConfiguredDeclarationsOnly' || !Array.isArray(projection.items) || projection.items.length > 100 ||
        !Array.isArray(projection.sources) || projection.sources.length !== 1) contextInvalid()
    const rule = projection.sources[0]
    if (!exact(rule, sourceKeys) || rule.tenantId !== expected.tenantId || rule.vesselId !== expected.vesselId || rule.assetId !== expected.assetId ||
        rule.source !== expected.source || rule.version !== expected.policyVersion || rule.kind !== expected.kind || rule.unit !== expected.unit ||
        rule.freshnessSeconds !== expected.freshnessSeconds || rule.maxScale !== expected.maxScale) contextInvalid()
    return expected
  } catch { contextInvalid() }
}

export async function requireObservationContext(page: ContextPage, fixture: ObservationSignedFixture): Promise<ObservationSignedFixture> {
  try {
    const reference = decode(fixture)
    const get = async (path: string) => {
      const response = await page.request.get(path)
      if (response.status() !== 200) contextInvalid()
      return response.json()
    }
    const user = await get('/api/v1/auth/session')
    if (!actualOwner(user, reference)) contextInvalid()
    const projection = await get(`/api/v1/equipment-hierarchy/${reference.vesselId}/${reference.assetId}/observations`)
    const plan = { runId: reference.runId, tenantId: reference.tenantId, vesselId: reference.vesselId, writerActorId: reference.actorId }
    observationFixtureFromActual(plan, user, projection)
    // A response read before a session replacement is not authority for the new actor.
    return observationFixtureFromActual(plan, await get('/api/v1/auth/session'), projection)
  } catch { contextInvalid() }
}
