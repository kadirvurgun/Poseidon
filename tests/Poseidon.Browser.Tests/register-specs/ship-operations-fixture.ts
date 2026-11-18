import { createHash } from 'node:crypto'
import { readFileSync, realpathSync, statSync } from 'node:fs'
import { isAbsolute, relative, sep } from 'node:path'
import { expect, type Page } from '@playwright/test'
export type ShipOpsSignedFixture = { schemaVersion: 'poseidon.ship-operations.signed-fixture.v1'; runId: string; tenantId: string; vesselId: string; actorId: string; origin: { kind: 'Noon'; id: string; revision: number; version: number; hash: string; sourceVersion: string } }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(key => Object.hasOwn(v, key))
const guid = (v: unknown): v is string => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(v) && v !== '00000000-0000-0000-0000-000000000000'
export function requiredShipOpsFixture(): ShipOpsSignedFixture {
  const name = process.env.POSEIDON_BROWSER_SHIP_OPERATIONS_FIXTURE, privateRoot = process.env.POSEIDON_LAB_PRIVATE
  if (!name || !privateRoot) throw new Error('ship_operations_signed_fixture_required')
  const root = realpathSync(privateRoot), path = realpathSync(name), child = relative(root, path)
  if (!child || isAbsolute(child) || child === '..' || child.startsWith('..' + sep) || !statSync(path).isFile() || statSync(path).size > 8192) throw new Error('ship_operations_fixture_boundary')
  let value: unknown
  try { value = JSON.parse(readFileSync(path, 'utf8')) } catch { throw new Error('ship_operations_fixture_invalid') }
  if (!exact(value, ['schemaVersion', 'runId', 'tenantId', 'vesselId', 'actorId', 'origin']) || value.schemaVersion !== 'poseidon.ship-operations.signed-fixture.v1' || !['runId', 'tenantId', 'vesselId', 'actorId'].every(key => guid(value[key])) || !exact(value.origin, ['kind', 'id', 'revision', 'version', 'hash', 'sourceVersion'])) throw new Error('ship_operations_fixture_invalid')
  const origin = value.origin
  if (origin.kind !== 'Noon' || !guid(origin.id) || typeof origin.revision !== 'number' || !Number.isSafeInteger(origin.revision) || origin.revision < 1 || typeof origin.version !== 'number' || !Number.isSafeInteger(origin.version) || origin.version < 1 || origin.version > 4294967295 || typeof origin.hash !== 'string' || !/^[a-fA-F0-9]{64}$/.test(origin.hash) || origin.sourceVersion !== '14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2128-2185:v46.3') throw new Error('ship_operations_fixture_invalid')
  return value as ShipOpsSignedFixture
}
export async function requireShipOpsSession(page: Page, fixture: ShipOpsSignedFixture) {
  const result = await page.request.get('/api/v1/auth/session'); expect(result.status()).toBe(200)
  const user = await result.json()
  expect(user.authenticated === true && user.canRead === true && user.actorId === fixture.actorId && user.tenantId === fixture.tenantId).toBe(true)
  expect(typeof user.csrfToken === 'string' && user.csrfToken.length > 16).toBe(true)
  return user as { csrfToken: string }
}
export function shipOpsLane(page: Page) { const viewport = page.viewportSize(); if (!viewport || viewport.width < 320) throw new Error('ship_operations_lane_required'); return viewport.width <= 420 ? 'narrow' : 'desktop' }
export function shipOpsSignedId(fixture: ShipOpsSignedFixture, lane: string, purpose: string) { const h = createHash('sha256').update(fixture.schemaVersion + ':' + fixture.runId + ':' + lane + ':' + purpose).digest('hex'); return h.slice(0, 8) + '-' + h.slice(8, 12) + '-' + h.slice(12, 16) + '-' + h.slice(16, 20) + '-' + h.slice(20, 32) }
export async function requireShipOpsCaps(page: Page, fixture: ShipOpsSignedFixture, kind: 'Fuel' | 'Port') {
  const root = '/api/v1/' + (kind === 'Fuel' ? 'fuel-bunker-logs' : 'port-activity-sof'), result = await page.request.get(root + '/capabilities?vesselId=' + fixture.vesselId); expect(result.status()).toBe(200)
  const caps = await result.json()
  expect(caps.vesselId === fixture.vesselId && caps.kind === kind && caps.canRead === true && caps.canWrite === true && caps.canSubmit === false && caps.canReview === false).toBe(true)
  expect(caps.offline).toEqual({ supported: false, state: 'UNAVAILABLE', operation: 'ShipOpsDraft' })
  const mutable = caps.schema.filter((field: { readOnly: boolean }) => !field.readOnly)
  expect(mutable).toHaveLength(kind === 'Fuel' ? 19 : 17)
  const fields: Record<string, { state: 'Known' | 'Missing' | 'Unknown'; value: string | null }> = Object.fromEntries(mutable.map((field: { key: string }) => [field.key, { state: 'Missing', value: null }]))
  return { root, fields }
}
