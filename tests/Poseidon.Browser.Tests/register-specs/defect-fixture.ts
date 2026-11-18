import { createHash } from 'node:crypto'
import { readFileSync, realpathSync, statSync } from 'node:fs'
import { isAbsolute, relative, sep } from 'node:path'
import { expect, type Page } from '@playwright/test'
import { defectSourceVersion } from '../../../frontend/src/product/defect-fields.ts'
export type DefectSignedFixture = { schemaVersion: 'poseidon.defect.signed-fixture.v1'; runId: string; tenantId: string; vesselId: string; actorId: string; assetId: string; assetRevision: number }
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const exact = (v: unknown, keys: string[]): v is Record<string, unknown> => object(v) && Object.keys(v).length === keys.length && keys.every(key => Object.hasOwn(v, key))
const guid = (v: unknown): v is string => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(v) && v !== '00000000-0000-0000-0000-000000000000'
export function requireDefectFixture(): DefectSignedFixture {
  const name = process.env.POSEIDON_BROWSER_DEFECT_FIXTURE, privateRoot = process.env.POSEIDON_LAB_PRIVATE
  if (!name || !privateRoot) throw new Error('defect_signed_fixture_required')
  const root = realpathSync(privateRoot), path = realpathSync(name), child = relative(root, path)
  if (!child || isAbsolute(child) || child === '..' || child.startsWith('..' + sep) || !statSync(path).isFile() || statSync(path).size > 8192) throw new Error('defect_fixture_boundary')
  let value: unknown
  try { value = JSON.parse(readFileSync(path, 'utf8')) } catch { throw new Error('defect_fixture_invalid') }
  if (!exact(value, ['schemaVersion', 'runId', 'tenantId', 'vesselId', 'actorId', 'assetId', 'assetRevision']) || value.schemaVersion !== 'poseidon.defect.signed-fixture.v1' || !['runId', 'tenantId', 'vesselId', 'actorId', 'assetId'].every(key => guid(value[key])) || typeof value.assetRevision !== 'number' || !Number.isSafeInteger(value.assetRevision) || value.assetRevision < 1) throw new Error('defect_fixture_invalid')
  return value as DefectSignedFixture
}
export function defectSignedId(fixture: DefectSignedFixture, page: Page, purpose: string) { const viewport = page.viewportSize(); if (!viewport || viewport.width < 320) throw new Error('defect_fixture_lane_required'); const h = createHash('sha256').update(fixture.schemaVersion + ':' + fixture.runId + ':' + (viewport.width <= 420 ? 'narrow' : 'desktop') + ':' + purpose).digest('hex'); return h.slice(0, 8) + '-' + h.slice(8, 12) + '-' + h.slice(12, 16) + '-' + h.slice(16, 20) + '-' + h.slice(20, 32) }
export async function requireDefectContext(page: Page, fixture: DefectSignedFixture) {
  const session = await page.request.get('/api/v1/auth/session'); expect(session.status()).toBe(200); const user = await session.json()
  expect(user.authenticated === true && user.canRead === true && user.actorId === fixture.actorId && user.tenantId === fixture.tenantId).toBe(true); expect(typeof user.csrfToken === 'string' && user.csrfToken.length > 16).toBe(true)
  const capsResponse = await page.request.get('/api/v1/defects/capabilities?vessel=' + fixture.vesselId); expect(capsResponse.status()).toBe(200); const caps = await capsResponse.json()
  expect(caps.vesselId === fixture.vesselId && caps.canRead === true && caps.canWrite === true && caps.canClose === false && caps.canVerify === false && caps.canAssessEffectiveness === false && caps.sourceVersion === defectSourceVersion).toBe(true); expect(caps.schema).toHaveLength(11)
  const assetResponse = await page.request.get('/api/v1/vessels/' + fixture.vesselId + '/defect-asset-context/' + fixture.assetId + '?revision=' + fixture.assetRevision); expect(assetResponse.status()).toBe(200); const asset = await assetResponse.json()
  expect(asset.vesselId === fixture.vesselId && asset.current === true && asset.qualification === 'REVIEW_REQUIRED' && asset.pin.id === fixture.assetId && asset.pin.revision === fixture.assetRevision && asset.pin.version > 0 && /^[A-F0-9]{64}$/.test(asset.pin.hash)).toBe(true)
  const fields: Record<string, { state: 'Known' | 'Missing' | 'Unknown'; value: string | null }> = Object.fromEntries(caps.schema.map((field: { key: string }) => [field.key, { state: 'Missing', value: null }]))
  return { user: user as { csrfToken: string }, fields, asset: asset.pin as { id: string; revision: number; version: number; hash: string }, sourceVersion: defectSourceVersion }
}
